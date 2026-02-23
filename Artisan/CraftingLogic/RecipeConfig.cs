using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System;
using System.Linq;

namespace Artisan.CraftingLogic;

[Serializable]
public class RecipeConfig
{
    private static string T(string key) => L10n.Tr(key);
    private static string T(string key, params object[] args) => L10n.Tr(key, args);

    public const uint Default = 0;
    public const uint Disabled = 1;

    [NonSerialized]
    public string TempSolverType = "";
    [NonSerialized]
    public int TempSolverFlavour = -1;

    public string CurrentSolverType => TempSolverType != "" ? TempSolverType : SolverType;
    public int CurrentSolverFlavour => TempSolverFlavour != -1 ? TempSolverFlavour : SolverFlavour;

    public string CurrentSolverName
    {
        get
        {
            foreach (var def in CraftingProcessor.GetSolverDefinitions())
            {
                if (def.Def.GetType().FullName == CurrentSolverType && def.Flavour == CurrentSolverFlavour)
                    return T(def.Name);
            }
            return "";
        }
    }

    public string SolverType = ""; // TODO: ideally it should be a Type?, but that causes problems for serialization
    public int SolverFlavour;
    public uint requiredFood = Default;
    public uint requiredPotion = Default;
    public uint requiredManual = Default;
    public uint requiredSquadronManual = Default;
    public bool requiredFoodHQ = true;
    public bool requiredPotionHQ = true;


    public bool FoodEnabled => RequiredFood != Disabled;
    public bool PotionEnabled => RequiredPotion != Disabled;
    public bool ManualEnabled => RequiredManual != Disabled;
    public bool SquadronManualEnabled => RequiredSquadronManual != Disabled;


    public uint RequiredFood => requiredFood == Default ? P.Config.DefaultConsumables.requiredFood : requiredFood;
    public uint RequiredPotion => requiredPotion == Default ? P.Config.DefaultConsumables.requiredPotion : requiredPotion;
    public uint RequiredManual => requiredManual == Default ? P.Config.DefaultConsumables.requiredManual : requiredManual;
    public uint RequiredSquadronManual => requiredSquadronManual == Default ? P.Config.DefaultConsumables.requiredSquadronManual : requiredSquadronManual;
    public bool RequiredFoodHQ => requiredFood == Default ? P.Config.DefaultConsumables.requiredFoodHQ : requiredFoodHQ;
    public bool RequiredPotionHQ => requiredPotion == Default ? P.Config.DefaultConsumables.requiredPotionHQ : requiredPotionHQ;


    public string FoodName => requiredFood == Default
        ? T("{0} (Default)", P.Config.DefaultConsumables.FoodName)
        : RequiredFood == Disabled
            ? T("Disabled")
            : $"{(RequiredFoodHQ ? " " : "")}{ConsumableChecker.Food.FirstOrDefault(x => x.Id == RequiredFood).Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(RequiredFood, RequiredFoodHQ))}";
    public string PotionName => requiredPotion == Default
        ? T("{0} (Default)", P.Config.DefaultConsumables.PotionName)
        : RequiredPotion == Disabled
            ? T("Disabled")
            : $"{(RequiredPotionHQ ? " " : "")}{ConsumableChecker.Pots.FirstOrDefault(x => x.Id == RequiredPotion).Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(RequiredPotion, RequiredPotionHQ))}";
    public string ManualName => requiredManual == Default
        ? T("{0} (Default)", P.Config.DefaultConsumables.ManualName)
        : RequiredManual == Disabled
            ? T("Disabled")
            : $"{ConsumableChecker.Manuals.FirstOrDefault(x => x.Id == RequiredManual).Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(RequiredManual, false))}";
    public string SquadronManualName => requiredSquadronManual == Default
        ? T("{0} (Default)", P.Config.DefaultConsumables.SquadronManualName)
        : RequiredSquadronManual == Disabled
            ? T("Disabled")
            : $"{ConsumableChecker.SquadronManuals.FirstOrDefault(x => x.Id == RequiredSquadronManual).Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(RequiredManual, false))}";

    public float LargestName => (Math.Max(Math.Max(Math.Max(Math.Max(ImGui.CalcTextSize(FoodName).X, ImGui.CalcTextSize(PotionName).X), ImGui.CalcTextSize(ManualName).X), ImGui.CalcTextSize(SquadronManualName).X), ImGui.CalcTextSize(CurrentSolverName).X) + 32f);

    public bool SolverIsRaph => CurrentSolverType == typeof(RaphaelSolverDefintion).FullName!;
    public bool SolverIsStandard => CurrentSolverType == typeof(StandardSolverDefinition).FullName!;
    public bool SolverIsExpert => CurrentSolverType == typeof(ExpertSolverDefinition).FullName!;

    public bool Draw(uint recipeId)
    {
        var recipe = LuminaSheets.RecipeSheet[recipeId];
        ImGuiEx.LineCentered($"###RecipeName{recipeId}", () => { ImGuiEx.TextUnderlined($"{recipe.ItemResult.Value.Name.ToDalamudString().ToString()}"); });
        var config = this;
        var stats = CharacterStats.GetBaseStatsForClassHeuristic((Job)((uint)Job.CRP + recipe.CraftType.RowId));
        stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
        var craft = Crafting.BuildCraftStateForRecipe(stats, (Job)((uint)Job.CRP + recipe.CraftType.RowId), recipe);
        if (craft.InitialQuality == 0)
            craft.InitialQuality = Simulator.GetStartingQuality(recipe, false, craft.StatLevel);
        bool changed = false;
        changed |= DrawFood();
        changed |= DrawPotion();
        changed |= DrawManual();
        changed |= DrawSquadronManual();
        changed |= DrawSolver(craft, liveStats: Player.ClassJob.RowId == craft.Recipe.CraftType.RowId + 8);
        DrawSimulator(craft);
        return changed;
    }

    public bool DrawFood(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV(T("Food Usage:"));
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        else ImGui.PushItemWidth(LargestName);
        if (ImGui.BeginCombo("##foodBuff", FoodName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable(T("{0} (Default)", P.Config.DefaultConsumables.FoodName)))
                {
                    requiredFood = Default;
                    requiredFoodHQ = false;
                    changed = true;
                }
            }
            if (ImGui.Selectable(T("Disable")))
            {
                requiredFood = Disabled;
                requiredFoodHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetFood(true))
            {
                if (ImGui.Selectable($"{x.Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(x.Id, false))}"))
                {
                    requiredFood = x.Id;
                    requiredFoodHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetFood(true, true))
            {
                if (ImGui.Selectable($" {x.Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(x.Id, true))}"))
                {
                    requiredFood = x.Id;
                    requiredFoodHQ = true;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawPotion(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV(T("Medicine Usage:"));
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        else ImGui.PushItemWidth(LargestName);
        if (ImGui.BeginCombo("##potBuff", PotionName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable(T("{0} (Default)", P.Config.DefaultConsumables.PotionName)))
                {
                    requiredPotion = Default;
                    requiredPotionHQ = false;
                    changed = true;
                }
            }
            if (ImGui.Selectable(T("Disable")))
            {
                requiredPotion = Disabled;
                requiredPotionHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetPots(true))
            {
                if (ImGui.Selectable($"{x.Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(x.Id, false))}"))
                {
                    requiredPotion = x.Id;
                    requiredPotionHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetPots(true, true))
            {
                if (ImGui.Selectable($" {x.Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(x.Id, true))}"))
                {
                    requiredPotion = x.Id;
                    requiredPotionHQ = true;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawManual(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV(T("Manual Usage:"));
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        else ImGui.PushItemWidth(LargestName);
        if (ImGui.BeginCombo("##manualBuff", ManualName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable(T("{0} (Default)", P.Config.DefaultConsumables.ManualName)))
                {
                    requiredManual = Default;
                    changed = true;
                }
            }
            if (ImGui.Selectable(T("Disable")))
            {
                requiredManual = Disabled;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetManuals(true))
            {
                if (ImGui.Selectable($"{x.Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(x.Id, false))}"))
                {
                    requiredManual = x.Id;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }



    public bool DrawSquadronManual(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV(T("Squadron Manual:"));
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        else ImGui.PushItemWidth(LargestName);
        if (ImGui.BeginCombo("##squadronManualBuff", SquadronManualName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable(T("{0} (Default)", P.Config.DefaultConsumables.SquadronManualName)))
                {
                    requiredSquadronManual = Default;
                    changed = true;
                }
            }
            if (ImGui.Selectable(T("Disable")))
            {
                requiredSquadronManual = Disabled;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetSquadronManuals(true))
            {
                if (ImGui.Selectable($"{x.Name} {T("(Qty: {0})", ConsumableChecker.NumberOfConsumable(x.Id, false))}"))
                {
                    requiredSquadronManual = x.Id;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawSolver(CraftState craft, bool hasButton = false, bool liveStats = true)
    {
        bool changed = false;
        var solver = CraftingProcessor.GetSolverForRecipe(this, craft);
        if (string.IsNullOrEmpty(solver.Name))
        {
            ImGuiEx.Text(ImGuiColors.DalamudRed, T("Unable to select default solver. Please select from dropdown."));
        }
        ImGuiEx.TextV(T("Solver:"));
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);

        if (ImGui.BeginCombo("##solver", T(solver.Name)))
        {
            foreach (var opt in CraftingProcessor.GetAvailableSolversForRecipe(craft, true).OrderByDescending(x => x.Priority))
            {
                if (opt == default) continue;
                if (opt.UnsupportedReason.Length > 0)
                {
                    ImGui.Text(T("{0} is unsupported - {1}", T(opt.Name), T(opt.UnsupportedReason)));
                }
                else
                {
                    bool selected = opt.Name == solver.Name;
                    if (ImGui.Selectable(T(opt.Name), selected))
                    {
                        IPC.IPC.SetTempSolverBackToNormal(craft.RecipeId);
                        SolverType = opt.Def.GetType().FullName!;
                        SolverFlavour = opt.Flavour;
                        changed = true;
                    }
                }
            }

            ImGui.EndCombo();
        }

        if (!Crafting.EnoughDelinsForCraft(this, craft, out var req))
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, T("You do not have enough {0} for this solver ({1} required).", Svc.Data.GetExcelSheet<Item>().GetRow(28724).Name, req));
            if (this.CurrentSolverType.Contains("Raphael"))
            {
                ImGuiEx.TextCentered(ImGuiColors.DalamudYellow, T("An alternative solution will be used/generated when you start crafting."));
            }
        }

        if (ConsumableChecker.SkippingConsumablesByConfig(craft.Recipe))
            ImGuiEx.Text(ImGuiColors.DalamudRed, T("Consumables will not be used due to level difference setting."));

        if (!hasButton)
            RaphaelCache.DrawRaphaelDropdown(craft, liveStats);

        return changed;
    }

    public unsafe void DrawSimulator(CraftState craft)
    {

        if (!P.Config.HideRecipeWindowSimulator)
        {
            var recipe = craft.Recipe;
            var config = this;
            var solverHint = Simulator.SimulatorResult(recipe, config, craft, out var hintColor);
            var solver = CraftingProcessor.GetSolverForRecipe(config, craft);

            if (solver.Name != "Expert Recipe Solver")
            {
                if (craft.MissionHasMaterialMiracle && solver.Name == "Standard Recipe Solver" && P.Config.UseMaterialMiracle)
                    ImGuiEx.TextCentered(T("This would use Material Miracle, which is not compatible with the simulator."));
                else
                    if (solver.Name == "Raphael Recipe Solver" && !RaphaelCache.HasSolution(craft, out _))
                        ImGuiEx.TextCentered(T("Unable to generate a simulator without a Raphael solution generated."));
                    else
                        ImGuiEx.TextCentered(hintColor, solverHint);
            }
            else
                ImGuiEx.TextCentered(T("Please run this recipe in the simulator for results."));

            if (ImGui.IsItemClicked())
            {
                P.PluginUi.OpenWindow = UI.OpenWindow.Simulator;
                P.PluginUi.IsOpen = true;
                SimulatorUI.SelectedRecipe = recipe;
                SimulatorUI.ResetSim();
                if (config.PotionEnabled)
                {
                    SimulatorUI.SimMedicine ??= new();
                    SimulatorUI.SimMedicine.Id = config.RequiredPotion;
                    SimulatorUI.SimMedicine.ConsumableHQ = config.RequiredPotionHQ;
                    SimulatorUI.SimMedicine.Stats = new ConsumableStats(config.RequiredPotion, config.RequiredPotionHQ);
                }
                if (config.FoodEnabled)
                {
                    SimulatorUI.SimFood ??= new();
                    SimulatorUI.SimFood.Id = config.RequiredFood;
                    SimulatorUI.SimFood.ConsumableHQ = config.RequiredFoodHQ;
                    SimulatorUI.SimFood.Stats = new ConsumableStats(config.RequiredFood, config.RequiredFoodHQ);
                }

                foreach (ref var gs in RaptureGearsetModule.Instance()->Entries)
                {
                    if ((Job)gs.ClassJob == (Job)((uint)Job.CRP + recipe.CraftType.RowId))
                    {
                        if (SimulatorUI.SimGS is null || (Job)SimulatorUI.SimGS.Value.ClassJob != (Job)((uint)Job.CRP + recipe.CraftType.RowId))
                        {
                            SimulatorUI.SimGS = gs;
                        }

                        if (SimulatorUI.SimGS.Value.ItemLevel < gs.ItemLevel)
                            SimulatorUI.SimGS = gs;
                    }
                }

                var rawSolver = CraftingProcessor.GetSolverForRecipe(config, craft);
                SimulatorUI._selectedSolver = new(rawSolver.Name, rawSolver.Def.Create(craft, rawSolver.Flavour));
            }

            if (ImGui.IsItemHovered())
            {
                ImGuiEx.Tooltip(T("Click to open in simulator"));
            }


        }
    }
}
