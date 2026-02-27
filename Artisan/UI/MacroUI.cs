using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Artisan.UI
{
    internal static class MacroUI
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        private static string _newMacroName = string.Empty;
        private static bool _keyboardFocus;
        private const string MacroNamePopupLabel = "Macro Name";
        private static bool reorderMode = false;
        private static MacroSolverSettings.Macro? selectedAssignMacro;

        private static int quickAssignLevel = 1;
        private static int quickAssignDifficulty = 9;
        private static int quickAssignQuality = 80;

        private static List<int> quickAssignPossibleDifficulties = new();
        private static int quickAssignMaxDifficulty => quickAssignPossibleDifficulties.LastOrDefault();
        private static int quickAssignMinDifficulty => quickAssignPossibleDifficulties.FirstOrDefault();

        private static List<int> quickAssignPossibleQualities = new();
        private static int quickAssignMaxQuality => quickAssignPossibleQualities.LastOrDefault();
        private static int quickAssignMinQuality => quickAssignPossibleQualities.FirstOrDefault();

        private static bool[] quickAssignJobs = new bool[8];
        private static Dictionary<int, bool> quickAssignDurabilities = new();
        private static bool quickAssignCannotHQ = false;

        internal static void Draw()
        {
            try
            {
                ImGui.TextWrapped(T("This tab will allow you to add macros that Artisan can use instead of its own decisions. Once you create a new macro, click on it from the list below to open up the macro editor window for your macro."));
                ImGui.Separator();

                if (Svc.ClientState.IsLoggedIn && Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween)
                {
                    ImGui.Text(T("Crafting in progress. Macro settings will be unavailable until you stop crafting."));
                    return;
                }
                ImGui.Spacing();
                if (ImGui.Button(T("Import Macro From Clipboard")))
                    OpenMacroNamePopup(MacroNameUse.FromClipboard);

                if (ImGui.Button(T("Import Macro From Clipboard (Artisan Export)")))
                {
                    try
                    {
                        var import = JsonConvert.DeserializeObject<MacroSolverSettings.Macro>(ImGui.GetClipboardText());
                        if (import != null)
                        {
                            P.Config.MacroSolverConfig.AddNewMacro(import);
                            P.Config.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Log();
                        Notify.Error(T("Unable to import."));
                    }
                }

                if (ImGui.Button(T("New Macro")))
                    OpenMacroNamePopup(MacroNameUse.NewMacro);

                DrawMacroNamePopup(MacroNameUse.FromClipboard);
                DrawMacroNamePopup(MacroNameUse.NewMacro);

                if (P.Config.MacroSolverConfig.Macros.Count > 0)
                {
                    if (P.Config.MacroSolverConfig.Macros.Count > 1)
                        ImGui.Checkbox(T("Reorder Mode (Click and Drag to Reorder)"), ref reorderMode);
                    else
                        reorderMode = false;

                    if (reorderMode)
                        ImGuiEx.CenterColumnText(T("Reorder Mode"));
                    else
                        ImGuiEx.CenterColumnText(T("Macro Editor Select"));

                    if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true))
                    {
                        for (int i = 0; i < P.Config.MacroSolverConfig.Macros.Count; i++)
                        {
                            var m = P.Config.MacroSolverConfig.Macros[i];
                            int cpCost = GetCPCost(m);
                            var selected = ImGui.Selectable($"{T("{0} (CP Cost: {1}) (ID: {2})", m.Name, cpCost, m.ID)}###{m.ID}");

                            if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && reorderMode)
                            {
                                int i_next = i + (ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).Y < 0f ? -1 : 1);
                                if (i_next >= 0 && i_next < P.Config.MacroSolverConfig.Macros.Count)
                                {
                                    P.Config.MacroSolverConfig.Macros[i] = P.Config.MacroSolverConfig.Macros[i_next];
                                    P.Config.MacroSolverConfig.Macros[i_next] = m;
                                    P.Config.Save();
                                    ImGui.ResetMouseDragDelta();
                                }
                            }

                            if (selected && !reorderMode && !P.ws.Windows.Any(x => x.WindowName.Contains(m.ID.ToString())))
                            {
                                new MacroEditor(m);
                            }
                        }

                    }
                    ImGui.EndChild();
                }
                else
                {
                    selectedAssignMacro = null;
                }
            }
            catch { }
        }

        public static int GetCPCost(MacroSolverSettings.Macro m)
        {
            Skills previousAction = Skills.None;
            int output = 0;
            int tcr = 0;
            foreach (var step in m.Steps)
            {
                if (step.Action == Skills.TouchCombo)
                {
                    output += 18;
                }
                if (step.Action == Skills.TouchComboRefined)
                {
                    if (tcr % 2 == 1)
                        output += 18;
                    else
                        output += 24;

                    tcr++;

                }
                output += Simulator.GetBaseCPCost(step.Action, previousAction);
                previousAction = step.Action;
            }
            return output;
        }

        public static double GetMacroLength(MacroSolverSettings.Macro m)
        {
            double output = 0;
            var delay = (double)P.Config.AutoDelay + (P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0);
            var delaySeconds = delay / 1000;

            foreach (var step in m.Steps)
            {
                if (step.Action.ActionIsLengthyAnimation())
                {
                    output += 2.5 + delaySeconds;
                }
                else
                {
                    output += 1.25 + delaySeconds;
                }
            }

            return Math.Round(output, 2);

        }

        public static float GetTeamcraftMacroLength(MacroSolverSettings.Macro m)
        {
            float output = 0;
            foreach (var step in m.Steps)
            {
                if (step.Action.ActionIsLengthyAnimation())
                {
                    output += 3f;
                }
                else
                {
                    output += 2f;
                }
            }

            return output;

        }

        private static void DrawMacroNamePopup(MacroNameUse use)
        {
            if (ImGui.BeginPopup($"{MacroNamePopupLabel}{use}"))
            {
                if (_keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    _keyboardFocus = false;
                }

                if (ImGui.InputText($"{T("Macro Name")}##macroName", ref _newMacroName, 64, ImGuiInputTextFlags.EnterReturnsTrue) && _newMacroName.Any())
                {
                    switch (use)
                    {
                        case MacroNameUse.NewMacro:
                            MacroSolverSettings.Macro newMacro = new();
                            newMacro.Name = _newMacroName;
                            P.Config.MacroSolverConfig.AddNewMacro(newMacro);
                            P.Config.Save();
                            new MacroEditor(newMacro);
                            break;
                        case MacroNameUse.FromClipboard:
                            try
                            {
                                var steps = ParseMacro(ImGui.GetClipboardText(), false);
                                if (steps.Count > 0)
                                {
                                    var macro = new MacroSolverSettings.Macro();
                                    macro.Name = _newMacroName;
                                    macro.Steps = steps;
                                    P.Config.MacroSolverConfig.AddNewMacro(macro);
                                    P.Config.Save();
                                    DuoLog.Information(T("{0} has been saved.", macro.Name));
                                }
                                else
                                {
                                    DuoLog.Error(T("Unable to parse clipboard. Please check your clipboard contains a working macro with actions."));
                                }
                            }
                            catch (Exception e)
                            {
                                Svc.Log.Information($"Could not save new Macro from Clipboard:\n{e}");
                            }

                            break;
                    }

                    _newMacroName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        public static List<MacroSolverSettings.MacroStep> ParseMacro(string text, bool raphParseEN = false)
        {
            var res = new List<MacroSolverSettings.MacroStep>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return res;
            }

            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1) continue;

                    var iStart = 0;
                    if (parts[0].Equals("/ac", StringComparison.CurrentCultureIgnoreCase) || parts[0].Equals("/action", StringComparison.CurrentCultureIgnoreCase))
                        ++iStart;
                    else if (parts[0].Contains("/", StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    var builder = new StringBuilder();
                    for (int i = iStart; i < parts.Length; i++)
                    {
                        if (parts[i].Contains("<")) continue;
                        builder.Append(parts[i]);
                        builder.Append(" ");
                    }
                    var action = builder.ToString().Trim();
                    action = action.Replace("\"", "");
                    if (string.IsNullOrEmpty(action)) continue;

                    if (TryParseMacroAlias(action, out var aliasAction))
                    {
                        res.Add(new() { Action = aliasAction });
                        continue;
                    }

                    var act = Enum.GetValues(typeof(Skills)).Cast<Skills>().FirstOrDefault(s => s.NameOfAction(raphParseEN).Equals(action, StringComparison.CurrentCultureIgnoreCase));
                    if (act == default)
                    {
                        act = Enum.GetValues(typeof(Skills)).Cast<Skills>().FirstOrDefault(s => s.NameOfAction(raphParseEN).Replace(" ", "").Replace("'", "").Equals(action, StringComparison.CurrentCultureIgnoreCase));
                        if (act == default)
                        {
                            DuoLog.Error(T("Unable to parse action: {0}", action));
                            continue;
                        }
                    }
                    res.Add(new() { Action = act });
                }
            }
            return res;
        }

        private static bool TryParseMacroAlias(string action, out Skills skill)
        {
            if (action.Equals("*", StringComparison.Ordinal))
            {
                skill = Skills.None;
                return true;
            }

            if (IsAlias(action, "Artisan Recommendation", "Artisan 推荐", "自动推荐动作", L10n.Tr("Artisan Recommendation")))
            {
                skill = Skills.None;
                return true;
            }

            if (IsAlias(action, "Touch Combo", "Touch 连段", "加工连段", L10n.Tr("Touch Combo")))
            {
                skill = Skills.TouchCombo;
                return true;
            }

            if (IsAlias(action, "Touch Combo (Refined Touch Route)", "Touch 连段（Refined Touch 路线）", "加工连段（精修路线）", L10n.Tr("Touch Combo (Refined Touch Route)")))
            {
                skill = Skills.TouchComboRefined;
                return true;
            }

            skill = default;
            return false;
        }

        private static bool IsAlias(string action, params string[] aliases)
            => aliases.Any(alias => action.Equals(alias, StringComparison.CurrentCultureIgnoreCase));

        private static void OpenMacroNamePopup(MacroNameUse use)
        {
            _newMacroName = string.Empty;
            _keyboardFocus = true;
            ImGui.OpenPopup($"{MacroNamePopupLabel}{use}");
        }

        internal static List<MacroSolverSettings.MacroStep> ParseMacro(IEnumerable<int> skillIds)
        {
            var res = new List<MacroSolverSettings.MacroStep>();
            if (skillIds.Count() == 0)
            {
                return res;
            }

            foreach (var item in skillIds)
            {
                var act  = (Skills)item;
                res.Add(new() { Action = act });
            }

            return res;
        }

        internal enum MacroNameUse
        {
            SaveCurrent,
            NewMacro,
            DuplicateMacro,
            FromClipboard,
        }
    }
}
