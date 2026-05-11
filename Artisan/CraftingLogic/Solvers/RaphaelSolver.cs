using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Artisan.CraftingLogic.Solvers.MacroSolverSettings;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            if (craft.StatLevel < 7)
                return new StandardSolver();

            if (RaphaelCache.HasSolution(craft, out var output))
            {
                return new MacroSolver(output!, craft);
            }
            return craft.CraftExpert ? new ExpertSolver() : new StandardSolver();
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            yield return new(this, 3, 2, $"Raphael Recipe Solver", craft.StatLevel < 7 ? $"Does not work before unlocking {Skills.MastersMend.NameOfAction()}. Please use Standard Recipe Solver" : "");
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours()
        {
            yield return new(this, 3, 2, $"Raphael Recipe Solver");
        }
    }

    internal static class RaphaelCache
    {
        internal static readonly ConcurrentDictionary<RaphaelOptions, RaphaelTaskInfo> Tasks = [];
        [NonSerialized]
        public static List<RaphaelSolutionConfig> TempConfigs = [];

        [NonSerialized]
        public const string RaphaelFileName = "RaphaelCache.dat";

        internal sealed class RaphaelTaskInfo(CancellationTokenSource cts, Task task, bool fromStartCraft)
        {
            public CancellationTokenSource Cancellation { get; set; } = cts;
            public Task Task { get; set; } = task;
            public volatile bool FromStartCraft = fromStartCraft;
            public volatile bool Succeeded;
        }

        public static void Build(CraftState craft, RaphaelSolutionConfig config, bool fromStartCraft = false)
        {
            if (craft.StatLevel < 7) return; // can't run raphael without Master's Mend

            // don't build if a task is already running with these options
            var key = GetOptions(craft, config);
            if (!CLIExists() || Tasks.ContainsKey(key)) return;

            // nuke the old macro if one exists
            P.Config.RaphaelSolverCacheV6.TryRemove(key, out _);

            var manipulation = config.HasManipulation ? "--manipulation" : "";
            var itemText = $"--custom-recipe {craft.LevelTable.RowId} {craft.CraftProgress} {(craft.CraftCollectible && !craft.IsCosmic ? craft.CraftQualityMin3 : craft.CraftQualityMax)} {craft.CraftDurability} {(craft.CraftExpert ? "1" : "0")} --stellar-steady-hand {Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand)}";

            var argsList = new List<string>
            {
                $"--initial {craft.InitialQuality}"
            };

            if (config.EnsureReliability) argsList.Add("--adversarial");
            if (config.BackloadProgress) argsList.Add("--backload-progress");
            if (config.UseHeartAndSoul) argsList.Add("--heart-and-soul");
            if (config.UseQuickInno) argsList.Add("--quick-innovation");
            if (P.Config.RaphaelSolverConfig.MaximumThreads > 0)
                argsList.Add($"--threads {P.Config.RaphaelSolverConfig.MaximumThreads}");

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(P.Config.RaphaelSolverConfig.TimeOutMins));
            var info = new RaphaelTaskInfo(cts, null!, fromStartCraft);

            info.Task = Task.Run(async () =>
            {
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.bin"),
                            Arguments = $"solve {itemText} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} {string.Join(' ', argsList)} --output-variables action_ids",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };

                    Svc.Log.Debug($"Spawning Raphael process with args: {process.StartInfo.Arguments}");
                    if (process.StartInfo.Arguments.Contains("adversarial"))
                        Svc.Log.Warning("Adversial enabled. Support will not be provided.");
                    if (process.StartInfo.Arguments.Contains("heart-and-soul") || process.StartInfo.Arguments.Contains("quick-innovation"))
                        Svc.Log.Warning("Specialist actions enabled. This may take a long time.");

                    process.Start();

                    using (cts.Token.Register(() =>
                    {
                        try { if (!process.HasExited) process.Kill(); }
                        catch (Exception ex) { ex.Log(); }
                        finally
                        {
                            if (Tasks.TryRemove(key, out var t) && t.FromStartCraft && Crafting.CurState is Crafting.State.WaitStart)
                            {
                                DuoLog.Error(L10n.Tr("Raphael has timed out or cancelled before a solution could be generated. Crafting will not start, please restart this craft."));
                                Crafting.CurState = Crafting.State.InvalidState;
                            }
                        }
                    }))
                    {
                        var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                        var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);

                        await Task.WhenAll(
                            stdOutTask,
                            stdErrTask,
                            process.WaitForExitAsync(cts.Token)
                        ).ConfigureAwait(false);

                        var output = stdOutTask.Result;
                        var error = stdErrTask.Result.Trim();

                        if (process.ExitCode != 0)
                        {
                            if (!string.IsNullOrWhiteSpace(error))
                                DuoLog.Error(L10n.Tr("Raphael error: {0}", error));

                            info.Succeeded = false;
                            cts.Cancel();
                            return;
                        }

                        var cleansedOutput = output.Replace("[", "").Replace("]", "").Replace("\"", "")
                                                   .Split(", ")
                                                   .Select(x => int.TryParse(x, out int n) ? n : 0);

                        P.Config.RaphaelSolverCacheV6[key] = new MacroSolverSettings.Macro
                        {
                            ID = GetNewID(),
                            Name = GetTextKey(craft, config),
                            Steps = MacroUI.ParseMacro(cleansedOutput),
                            Options = new RaphaelOptions()
                            {
                                SkipQualityIfMet = false,
                                UpgradeProgressActions = false,
                                UpgradeQualityActions = false,
                                MinCP = craft.StatCP,
                                MinControl = craft.StatControl,
                                MinCraftsmanship = craft.StatCraftsmanship,
                                Level = craft.CraftLevel,
                                Progress = craft.CraftProgress,
                                QualityMax = craft.CraftQualityMax,
                                Durability = craft.CraftDurability,
                                IsExpert = craft.CraftExpert,
                                InitialQuality = craft.InitialQuality,
                                IsSpecialist = craft.Specialist,
                                SteadyHandUses = Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand),
                                SolutionConfig = config
                            }
                        };

                        Svc.Log.Debug($"Saved new macro to Raphael cache, ID: {P.Config.RaphaelSolverCacheV6[key].ID}");

                        info.Succeeded = P.Config.RaphaelSolverCacheV6[key]?.Steps.Count > 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    info.Succeeded = false;
                }
                catch (Exception ex)
                {
                    ex.Log("Something went wrong with Raphael task.");
                    info.Succeeded = false;
                }
                finally
                {
                    if (info.Succeeded)
                    {
                        AutoSwitch(craft, key);
                        P.Config.Save();
                    }
                    Tasks.TryRemove(key, out _);
                }
            }, cts.Token);

            Tasks.TryAdd(key, info);
        }

        private static void AutoSwitch(CraftState craft, RaphaelOptions key)
        {
            static bool autoSwitchOk(uint recipeId)
            {
                if (P.Config.RaphaelSolverConfig.AutoSwitchOverManual)
                    return true;

                if (P.Config.RecipeConfigs.TryGetValue(recipeId, out var cfg))
                    // flavours: 0 = standard, expert; 3 = raphael; otherwise = macro/script
                    return cfg.SolverFlavour is 0 or 3;

                return true;
            }

            if (P.Config.RaphaelSolverConfig.AutoSwitch)
            {
                Svc.Log.Information("Auto-switch is enabled, switching solver for recipe if applicable.");
                if (!P.Config.RaphaelSolverConfig.AutoSwitchOnAll)
                {
                    Svc.Log.Debug("Switching to Raphael solver - Single");
                    if (craft.StatLevel < 7)
                    {
                        Svc.Log.Debug($"Skipping auto-switch for recipe {craft.Recipe.RowId} - Raphael solver not unlocked");
                        return;
                    }
                    var nopt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                    if (nopt is { } opt)
                    {
                        if (autoSwitchOk(craft.Recipe.RowId))
                        {
                            Svc.Log.Information("AutoSwitchOk, setting");
                            var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.Recipe.RowId) ?? new();
                            config.SolverType = opt.Def.GetType().FullName!;
                            config.SolverFlavour = opt.Flavour;
                            P.Config.RecipeConfigs[craft.Recipe.RowId] = config;
                        }
                        else
                            Svc.Log.Information("Never mind, recipe already has a macro assigned");
                    }
                }
                else
                {
                    var crafts = AllValidCrafts(key).ToList();
                    Svc.Log.Information($"Applying solver to {crafts.Count} recipes.");
                    var nopt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                    if (nopt is { } opt)
                    {
                        var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.Recipe.RowId) ?? new();
                        config.SolverType = opt.Def.GetType().FullName!;
                        config.SolverFlavour = opt.Flavour;
                        foreach (var c in crafts)
                        {
                            if (c.StatLevel < 7)
                            {
                                Svc.Log.Debug($"Skipping {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) - Raphael solver not unlocked");
                                continue;
                            }
                            if (autoSwitchOk(c.Recipe.RowId))
                            {
                                //Svc.Log.Information($"Switching {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) to Raphael solver");
                                var switchConfig = P.Config.RecipeConfigs.GetValueOrDefault(c.Recipe.RowId) ?? new();
                                switchConfig.SolverType = opt.Def.GetType().FullName!;
                                switchConfig.SolverFlavour = opt.Flavour;
                                P.Config.RecipeConfigs[c.Recipe.RowId] = switchConfig;
                            }
                            else
                                Svc.Log.Information($"Skipping {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) because it already has a macro assigned");
                        }
                    }
                }
            }
        }

        public static RaphaelOptions GetOptions(CraftState craft, RaphaelSolutionConfig? config)
        {
            config ??= GetRaphConfig(craft);

            return new RaphaelOptions()
            {
                MinCraftsmanship = craft.StatCraftsmanship,
                MinControl = craft.StatControl,
                MinCP = craft.StatCP,
                Level = craft.CraftLevel,
                Progress = craft.CraftProgress,
                QualityMax = craft.CraftQualityMax,
                Durability = craft.CraftDurability,
                IsExpert = craft.CraftExpert,
                InitialQuality = craft.InitialQuality,
                IsSpecialist = craft.Specialist,
                SteadyHandUses = Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand),
                SolutionConfig = config
            };
        }

        // this key is for identifying solutions while debugging; code should look up solutions by their RaphaelOptions
        public static string GetTextKey(CraftState craft, RaphaelSolutionConfig config)
        {
            return $"{craft.CraftLevel}/{craft.CraftProgress}/{craft.CraftQualityMax}/{craft.CraftDurability}-{craft.StatCraftsmanship}/{craft.StatControl}/{craft.StatCP}-{(craft.CraftExpert ? "Ex" : "St")}/{craft.InitialQuality}/{(craft.Specialist ? "Sp" : "Re")}/Steady{Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand)}-{(config.UseHeartAndSoul ? "1" : "0")}/{(config.UseQuickInno ? "1" : "0")}/{(config.HasManipulation ? "1" : "0")}/{(config.EnsureReliability ? "1" : "0")}/{(config.BackloadProgress ? "1" : "0")}";
        }

        public static string GetKeyForLookups(RaphaelOptions opt)
        {
            return $"{opt.Level}/{opt.Progress}/{opt.QualityMax}/{opt.Durability}-{opt.MinCraftsmanship}/{opt.MinControl}/{opt.MinCP}-{(opt.IsExpert ? "Ex" : "St")}/{opt.InitialQuality}/{(opt.IsSpecialist ? "Sp" : "Re")}/Steady{opt.SteadyHandUses}-{(opt.SolutionConfig.UseHeartAndSoul ? "1" : "0")}/{(opt.SolutionConfig.UseQuickInno ? "1" : "0")}/{(opt.SolutionConfig.HasManipulation ? "1" : "0")}/{(opt.SolutionConfig.EnsureReliability ? "1" : "0")}/{(opt.SolutionConfig.BackloadProgress ? "1" : "0")}";
        }

        public static IEnumerable<CraftState> AllValidCrafts(RaphaelOptions key)
        {
            var recipes = LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == key.Level);
            foreach (var recipe in recipes)
            {
                var state = Crafting.BuildCraftStateForRecipe(default, (Job)((uint)Job.CRP + recipe.CraftType.RowId), recipe);
                if (state.StatLevel < 7) continue;

                if (key.Progress == state.CraftProgress &&
                    key.QualityMax == state.CraftQualityMax &&
                    key.Durability == state.CraftDurability)
                    yield return state;
            }
        }

        public static RaphaelSolutionConfig GetRaphConfig(CraftState craft, bool checkDelins = false)
        {
            var globalRaph = P.Config.RaphaelSolverConfig;
            var hasDelins = Crafting.DelineationCount() > 0;
            return new RaphaelSolutionConfig()
            {
                HasManipulation = craft.UnlockedManipulation,
                EnsureReliability = globalRaph.AllowEnsureReliability && globalRaph.EnsureReliability && !craft.CraftExpert,
                BackloadProgress = globalRaph.AllowBackloadProgress && globalRaph.BackloadProgress,
                UseHeartAndSoul = globalRaph.ShowSpecialistSettings && globalRaph.UseHeartAndSoul && craft.Specialist && (!checkDelins || hasDelins),
                UseQuickInno = globalRaph.ShowSpecialistSettings && globalRaph.UseQuickInno && craft.Specialist && (!checkDelins || hasDelins),
            };
        }

        public static bool HasSolution(CraftState craft, out Macro? raphaelSolution) => HasSolution(craft, null, out raphaelSolution);

        public static bool HasSolution(CraftState craft, RaphaelSolutionConfig? config, out Macro? raphaelSolution)
        {
            config ??= GetRaphConfig(craft);

            var key = GetOptions(craft, config);
            raphaelSolution = null;
            var hasKey = P.Config.RaphaelSolverCacheV6.ContainsKey(key);
            if (hasKey)
            {
                raphaelSolution = P.Config.RaphaelSolverCacheV6[key];
                return true;
            }
            else
                return false;
        }

        public static bool InProgress(CraftState craft, RaphaelSolutionConfig config) => Tasks.TryGetValue(GetOptions(craft, config), out var _);

        public static bool InProgressAny() => !Tasks.IsEmpty;

        internal static bool CLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.bin"));
        }

        public static void DrawRaphaelDropdown(CraftState craft, bool liveStats = true)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.RecipeId) ?? new();
            if (CLIExists())
            {
                // snapshot the current generation settings in case they change before the next generate
                var curConfig = GetRaphConfig(craft).JSONClone();
                var hasSolution = HasSolution(craft, curConfig, out var solution);
                var opts = GetOptions(craft, curConfig);
                var keyStr = GetTextKey(craft, curConfig);

                var solverIsRaph = config.SolverIsRaph;
                if (!hasSolution)
                {
                    if (solverIsRaph)
                        ImGuiEx.TextCentered(ImGuiColors.DalamudRed, L10n.Tr("No Raphael Solution Generated."));
                    if (P.Config.RaphaelSolverConfig.AutoGenerate && CraftingProcessor.GetAvailableSolversForRecipe(craft, true).Any() && (!craft.CraftExpert || (craft.CraftExpert && P.Config.RaphaelSolverConfig.GenerateOnExperts)))
                    {
                        Build(craft, curConfig);
                    }
                }

                ImGui.Separator();

                var inProgress = InProgress(craft, curConfig);

                if (inProgress)
                    ImGui.BeginDisabled();

                var showReliability = P.Config.RaphaelSolverConfig.AllowEnsureReliability && !craft.CraftExpert;
                var showBackload = P.Config.RaphaelSolverConfig.AllowBackloadProgress;
                var showSpecialist = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;

                if (showReliability || showBackload || showSpecialist)
                {
                    ImGuiEx.Text(ImGuiColors.DalamudGrey, "Raphael 求解器设置");
                }
                else
                {
                    ImGui.Dummy(new Vector2(0, 2f));
                }

                if (showReliability)
                {
                    ImGui.Checkbox($"确保 100% 可靠性##{keyStr}Reliability", ref P.Config.RaphaelSolverConfig.EnsureReliability);
                    ImGuiComponents.HelpMarker("尝试寻找对每一步制作条件任意排列都可用的解法。极度消耗内存与 CPU，开启后不提供支持。");
                }
                if (showBackload)
                {
                    ImGui.Checkbox($"回填进度##{keyStr}Progress", ref P.Config.RaphaelSolverConfig.BackloadProgress);
                    ImGuiComponents.HelpMarker($"寻找一种会先完成品质再开始进度的解法。适用于简单专家配方，否则 Malleable 状态可能过早结束。" );
                }
                if (showSpecialist)
                {
                    P.PluginUi.ExpertSettingsUI.CheckboxWithIcons($"{keyStr}HS", ref P.Config.RaphaelSolverConfig.UseHeartAndSoul, "允许 [s!HeartAndSoul]");
                    ImGuiComponents.HelpMarker($"生成的宏每次制作需要消耗 1 张命题卡。");

                    P.PluginUi.ExpertSettingsUI.CheckboxWithIcons($"{keyStr}QI", ref P.Config.RaphaelSolverConfig.UseQuickInno, "允许 [s!QuickInnovation]");
                    ImGuiComponents.HelpMarker($"生成的宏每次制作需要消耗 1 张命题卡。");
                }

                if (showReliability || showBackload || showSpecialist)
                {
                    ImGui.Dummy(new Vector2(0, 5f));
                }

                if (inProgress)
                    ImGui.EndDisabled();

                if (craft.StatLevel >= 7) // can't run the raphael generator without Master's Mend
                {
                    if (!inProgress)
                    {
                        string verb = hasSolution ? "重建" : "生成";
                        ImGuiEx.LineCentered(() =>
                        {
                            if (ImGui.Button($"{verb} Raphael 解法", new Vector2(config.GetLargestName(), 25f.Scale())))
                            {
                                Build(craft, curConfig);
                            }
                        });
                    }
                    else
                    {
                        ImGuiEx.LineCentered(() =>
                        {
                            if (ImGui.Button("取消 Raphael 生成", new Vector2(config.GetLargestName(), 25f.Scale())))
                            {
                                Tasks.TryRemove(opts, out var task);
                                task.Cancellation.Cancel();
                            }
                        });
                    }
                }

                if (curConfig.EnsureReliability && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("已启用‘确保 100% 可靠性’，这会非常消耗 CPU 和内存。\n因此若出现问题将不提供支持。");
                    ImGui.EndTooltip();
                }

                if (curConfig.UseHeartAndSoul || curConfig.UseQuickInno)
                {
                    ImGuiEx.Text(ImGuiColors.DalamudYellow, "已启用专家动作，这会明显降低求解速度。");
                }

                if (inProgress)
                {
                    ImGuiEx.TextCentered("生成中...");
                }

                ImGui.Dummy(new Vector2(0, 2f));
            }
        }

        public static int GetNewID(Configuration? config = null)
        {
            config ??= P.Config;

            var rng = new Random();
            var id = rng.Next(50001, 10000000);
            while (config.RaphaelSolverCacheV6.Values.FirstOrDefault(m => m.ID == id) != null)
                id = rng.Next(50001, 10000000);
            return id;
        }

        // deprecated (string keys are now RaphaelOptions objects), should only be used when converting from v5
        public static (int Level, int Prog, int Qual, int Dur, int Initial, int Crafts, int Control, int CP, bool SP, bool IsEx, int Hands) V5KeyParts(string key)
        {
            var parts = key.Split('/');

            int.TryParse(parts[0], out var lvl);
            int.TryParse(parts[1], out var prog);
            int.TryParse(parts[2], out var qual);
            int.TryParse(parts[3].Split('-')[0], out var dur);
            int.TryParse(parts[3].Split('-')[1], out var crafts);
            int.TryParse(parts[4], out var ctrl);
            int.TryParse(parts[5].Split('-')[0], out var cp);
            int.TryParse(parts[6], out var initial);
            var sp = parts[7] == "Sp";
            var isEx = parts[5].Split('-')[1] == "Ex";
            var hands = 0;
            if (parts.Length >= 9)
                hands = parts[8].Substring(6).ParseInt() ?? 0;

            return (lvl, prog, qual, dur, initial, crafts, ctrl, cp, sp, isEx, hands);
        }

        public static void LoadRaphaelCache(Configuration config, bool deleteV5)
        {
            // keeping "deletev5" as a param, currently false, so we can delete it in the future to reduce config file size
            // v5 cache won't be loaded after being converted if a v6 cache exists, just keeping it as a backup right now

            // either load the current cache or, if missing, try to convert a v5 cache
            var v6cache = LoadRaphaelCacheFromFile(config);
            if (!v6cache.IsEmpty)
            {
                Svc.Log.Info($"Loaded existing Raphael cache from file ({v6cache.Keys.Count} entries)");
                config.RaphaelSolverCacheV6 = v6cache;
            }
            else if (!config.RaphaelSolverCacheV5.IsEmpty && !config.RaphaelV5Converted)
            {
                Svc.Log.Info($"Updating Raphael cache from v5 to v6 ({config.RaphaelSolverCacheV5.Keys.Count} entries)");
                ConvertV5ToV6(config);
            }

            if (deleteV5)
                config.RaphaelSolverCacheV5.Clear();
        }

        private static ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> LoadRaphaelCacheFromFile(Configuration config)
        {
            var file = new FileInfo(Path.Combine(config.ConfigDirectory.FullName, RaphaelFileName));
            if (!file.Exists)
                return [];

            try
            {
                Svc.Log.Information("Loading Raphael cache from file...");
                var stringCache = RaphaelCache.ReadCacheFile(file);
                if (stringCache.Count > 0)
                    return RaphaelCache.ConvertFromStringCache(stringCache);
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error reading raphael cache file \"{file.FullName}\":\n{e}");
            }

            return [];
        }

        public static void WriteRaphaelCache(Configuration config)
        {
            var file = new FileInfo(Path.Combine(config.ConfigDirectory.FullName, RaphaelFileName));
            try
            {
                var stringCache = RaphaelCache.ConvertToStringCache(config.RaphaelSolverCacheV6);
                var json = JObject.FromObject(stringCache).ToString();
                File.WriteAllText(file.FullName, json);
                P.PluginUi.RaphaelCacheUI.Table = null;
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error saving raphael cache file \"{file.FullName}\":\n{e}");
            }
        }
        public static void ConvertV5ToV6(Configuration config)
        {
            ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> v6Cache = [];

            // check if the current character has Manipulation on every job as a guess for v5 solves
            bool hasAllManip = true;
            List<Job> allJobs = [Job.CRP, Job.BSM, Job.ARM, Job.GSM, Job.LTW, Job.WVR, Job.ALC, Job.CUL];
            foreach (Job j in allJobs)
            {
                hasAllManip &= CharacterInfo.IsManipulationUnlocked(j);
            }

            foreach (var (key, macro) in config.RaphaelSolverCacheV5)
            {
                var stats = V5KeyParts(key);

                // try to guess some raph generation settings based on macro steps
                bool v6HS = false;
                bool v6QI = false;
                bool v6Manip = hasAllManip;
                bool v6Backload = true;

                bool hasProgress = false;
                foreach (MacroStep step in macro.Steps)
                {
                    if (step.Action == Skills.HeartAndSoul)
                        v6HS = true;
                    if (step.Action == Skills.QuickInnovation)
                        v6QI = true;
                    if (step.Action == Skills.Manipulation)
                        v6Manip = true;

                    // not backloading progress if any quality action happens after any progress action
                    if (MacroSolver.ActionIsProgress(step.Action))
                        hasProgress = true;
                    if (hasProgress && MacroSolver.ActionIsQuality(step.Action))
                        v6Backload = false;
                }

                string newKey = $"{key}-{(v6HS ? "1" : "0")}/{(v6QI ? "1" : "0")}/{(v6Manip ? "1" : "0")}/0/{(v6Backload ? "1" : "0")}";
                RaphaelOptions newOpts = new()
                {
                    SkipQualityIfMet = macro.Options.SkipQualityIfMet,
                    UpgradeProgressActions = macro.Options.UpgradeProgressActions,
                    UpgradeQualityActions = macro.Options.UpgradeQualityActions,
                    MinCP = stats.CP,
                    MinControl = stats.Control,
                    MinCraftsmanship = stats.Crafts,
                    Level = stats.Level,
                    Progress = stats.Prog,
                    QualityMax = stats.Qual,
                    Durability = stats.Dur,
                    IsExpert = stats.IsEx,
                    InitialQuality = stats.Initial,
                    IsSpecialist = stats.SP,
                    SteadyHandUses = stats.Hands,
                    SolutionConfig = new RaphaelSolutionConfig()
                    {
                        UseHeartAndSoul = v6HS,
                        UseQuickInno = v6QI,
                        HasManipulation = v6Manip,
                        EnsureReliability = false,  // better to regenerate than to assume
                        BackloadProgress = v6Backload
                    }
                };

                Macro v6Macro = new()
                {
                    ID = GetNewID(config),  // v5 didn't enforce ID uniqueness so we'll assign new ones just in case
                    Name = newKey,
                    Steps = macro.Steps,
                    Options = newOpts
                };

                v6Cache[newOpts] = v6Macro;
            }

            config.RaphaelSolverCacheV6 = v6Cache;
            config.RaphaelV5Converted = true;
        }

        public static Dictionary<string, MacroSolverSettings.Macro> ConvertToStringCache(ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> cache)
        {
            Dictionary<string, MacroSolverSettings.Macro> stringCache = [];

            foreach (var (key, macro) in cache)
            {
                JsonSerializerOptions options = new() { IncludeFields = true };
                string jsonKey = JsonSerializer.Serialize(key, options);
                stringCache[jsonKey] = macro;
            }

            return stringCache;
        }

        public static ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> ConvertFromStringCache(Dictionary<string, MacroSolverSettings.Macro> stringCache)
        {
            ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> cache = [];

            foreach (var (key, macro) in stringCache)
            {
                var json = JObject.Parse(key);
                var parsedKey = json.ToObject<RaphaelOptions>() ?? new();
                if (parsedKey.Level > 0)
                    cache[parsedKey] = macro;
            }

            return cache;
        }

        public static Dictionary<string, MacroSolverSettings.Macro> ReadCacheFile(FileInfo file)
        {
            if (!file.Exists)
                return [];

            try
            {
                var raw = File.ReadAllText(file.FullName);
                var json = JObject.Parse(raw);
                var parsed = json.ToObject<Dictionary<string, MacroSolverSettings.Macro>>() ?? [];
                return parsed;
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error reading raphael cache file \"{file.FullName}\":\n{e}");
                return [];
            }
        }
    }

    public class RaphaelSolverSettings
    {
        // these enable the relevant checkboxes on the crafting log mini-menu
        public bool AllowEnsureReliability = false;
        public bool AllowBackloadProgress = true;
        public bool ShowSpecialistSettings = false;
        // these track the actual values of those checkboxes
        public bool EnsureReliability = false;
        public bool BackloadProgress = true;
        public bool UseHeartAndSoul = false;
        public bool UseQuickInno = false;

        public bool ExactCraftsmanship = false;
        public bool AutoGenerate = false;
        public bool AutoSwitch = false;
        public bool AutoSwitchOnAll = false;
        public bool AutoSwitchOverManual = true;
        public int MaximumThreads = 0;
        public bool GenerateOnExperts = false;
        public int TimeOutMins = 1;
        public int MaxStellarHand = 2;
        public bool DefaultRaphSolver = false;
        public bool FallbackToSolverIfRaphaelLocked = true;
        public string FallbackSolverType = typeof(StandardSolverDefinition).FullName!;
        public int FallbackSolverFlavour = 0;
        public bool Draw()
        {
            bool changed = false;
            try
            {
                string ProgressString = LuminaSheets.AddonSheet[213].Text.ToString();
                string QualityString = LuminaSheets.AddonSheet[216].Text.ToString();
                string ConditionString = LuminaSheets.AddonSheet[215].Text.ToString();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, L10n.Tr("Raphael settings can change the performance and system memory consumption. If you have low amounts of RAM try not to change settings, recommended minimum amount of RAM free is 2GB"));

                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped(L10n.Tr("性能"));
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderInt(L10n.Tr("最大线程数"), ref MaximumThreads, 0, Environment.ProcessorCount);
                ImGuiComponents.HelpMarker(L10n.Tr("默认会尽可能使用全部资源，但在低端机器上你可能需要减少 CPU 使用量以换取速度。（0 = 全部）"));

                changed |= ImGui.Checkbox(L10n.Tr("在宏生成中确保 100% 可靠性"), ref AllowEnsureReliability);
                //ImGui.PushTextWrapPos(0);
                ImGuiEx.TextWrapped(new System.Numerics.Vector4(255, 0, 0, 1), L10n.Tr("确保可靠性并不一定总能成功，而且会非常消耗 CPU 和内存，建议至少预留 16GB 以上内存。开启后不提供支持。"));
                //ImGui.PopTextWrapPos();

                ImGui.Dummy(new Vector2(0, 2f));
                changed |= ImGui.SliderInt(L10n.Tr("解法生成超时"), ref TimeOutMins, 1, 15);
                ImGuiComponents.HelpMarker(L10n.Tr("如果解法生成超过该分钟数，将取消生成任务。"));

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped(L10n.Tr("自动使用"));
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox(L10n.Tr("如果尚未创建有效解法则自动生成。"), ref AutoGenerate);
                if (AutoGenerate)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox(L10n.Tr("对专家配方也生成"), ref GenerateOnExperts);
                    ImGui.Unindent();
                }

                changed |= ImGui.Checkbox(L10n.Tr("一旦生成解法就自动切换到 Raphael 求解器。"), ref AutoSwitch);
                if (AutoSwitch)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox(L10n.Tr("应用到所有有效制作"), ref AutoSwitchOnAll);
                    changed |= ImGui.Checkbox(L10n.Tr("覆盖已有宏的制作"), ref AutoSwitchOverManual);
                    ImGui.Unindent();
                }

                changed |= ImGui.Checkbox(L10n.Tr("将 Raphael 作为默认求解器"), ref DefaultRaphSolver);
                ImGuiComponents.HelpMarker(L10n.Tr("重要说明：\r\n\r\n• 在更改此设置前已打开的配方仍会使用原来的求解器。\r\n• 若禁用此设置，已设为 Raphael 的配方会一直保持，直到手动更改。\r\n• 禁用时默认使用标准求解器。"));

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped(L10n.Tr("宏生成"));
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.TextWrapped(L10n.Tr("这些设置会为每个配方显示新的宏生成选项。"));

                ImGui.Dummy(new Vector2(0, 2f));
                changed |= ImGui.Checkbox(L10n.Tr("允许回填 {0}", ProgressString.ToLower()), ref AllowBackloadProgress);
                ImGuiComponents.HelpMarker(L10n.Tr("启用后会确保先完成 {0} 再开始 {1}。适用于简单专家配方，否则 Malleable 状态可能导致过早结束。", QualityString.ToLower(), ProgressString.ToLower()));

                changed |= ImGui.Checkbox(L10n.Tr("可用时允许专家动作"), ref ShowSpecialistSettings);

                changed |= P.PluginUi.ExpertSettingsUI.SliderIntWithIcons("MaxStellarHand", ref MaxStellarHand, 0, 2, L10n.Tr("每次制作最多使用 [s!SteadyHand] 次数"));
                P.PluginUi.ExpertSettingsUI.HelpMarkerWithIcons([L10n.Tr("此设置仅适用于在带有 [s!SteadyHand] 的任务中的宇宙探索配方。"), L10n.Tr("Raphael 求解器会根据配方难度最多使用这么多次数。")]);

                changed |= ImGui.Checkbox(L10n.Tr("若 Raphael 被锁定则回退到其他求解器。"), ref FallbackToSolverIfRaphaelLocked);

                ImGuiComponents.HelpMarker(L10n.Tr("如果 Raphael 当前被锁定，则不会使用它，而会自动改用其他求解器。"));

                if (FallbackToSolverIfRaphaelLocked)
                {
                    ImGui.Indent();

                    var currentFallbackName = L10n.Tr(CraftingProcessor.GetSolverDefinitions().FirstOrDefault(x => x.Def.GetType().FullName == FallbackSolverType && x.Flavour == FallbackSolverFlavour).Name ?? "Unknown");

                    if (ImGui.BeginCombo("##fallbackSolver", currentFallbackName))
                    {
                        foreach (var opt in CraftingProcessor.GetSolverDefinitions().OrderBy(x => x.Priority))
                        {
                            if (opt == default) continue;
                            if (opt.Def.GetType() == typeof(RaphaelSolverDefintion)) continue;
                            if (opt.Def.GetType() == typeof(ExpertSolverDefinition)) continue;
                            if (opt.UnsupportedReason.Length > 0)
                            {
                                ImGui.Text(L10n.Tr("{0} is unsupported - {1}", L10n.Tr(opt.Name), L10n.Tr(opt.UnsupportedReason)));
                            }
                            else
                            {
                                bool selected = opt.Def.GetType().FullName == FallbackSolverType;
                                if (ImGui.Selectable(L10n.Tr(opt.Name), selected))
                                {
                                    FallbackSolverType = opt.Def.GetType().FullName!;
                                    FallbackSolverFlavour = opt.Flavour;
                                    changed = true;
                                }
                            }
                        }

                        ImGui.EndCombo();
                    }

                    ImGui.Unindent();
                }

                if (ImGui.Button(L10n.Tr("清空 Raphael 宏缓存（当前已存储 {0} 个）", P.Config.RaphaelSolverCacheV6.Count)))
                {
                    P.Config.RaphaelSolverCacheV6.Clear();
                    changed |= true;
                }

                ImGui.Unindent();
                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));

                return changed;
            }
            catch { }
            return changed;
        }
    }

    public class RaphaelSolutionConfig
    {
        public bool HasManipulation = false;
        public bool EnsureReliability = false;
        public bool BackloadProgress = false;
        public bool UseHeartAndSoul = false;
        public bool UseQuickInno = false;
    }

    public class RaphaelOptions : MacroOptions
    {
        public int Level = 0;
        public int Progress = 0;
        public int QualityMax = 0;
        public int Durability = 0;
        public bool IsExpert = false;
        public int InitialQuality = 0;
        public bool IsSpecialist = false;
        public int SteadyHandUses = 0;
        public RaphaelSolutionConfig SolutionConfig = new();

        public override int GetHashCode() => RaphaelCache.GetKeyForLookups(this).GetHashCode();
        public override bool Equals(object? obj) => obj != null && obj.GetHashCode() == this.GetHashCode();
    }
}
