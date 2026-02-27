using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Numerics;

namespace Artisan.UI
{
    internal class MacroEditor : Window
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        private MacroSolverSettings.Macro SelectedMacro;
        private bool renameMode = false;
        private string renameMacro = "";
        private int selectedStepIndex = -1;
        private bool Raweditor = false;
        private static string _rawMacro = string.Empty;
        private bool raphael_cache = false;

        public MacroEditor(MacroSolverSettings.Macro macro, bool raphael_cache = false) : base($"{L10n.Tr("Macro Editor")}###{macro.ID}", ImGuiWindowFlags.None)
        {
            this.raphael_cache = raphael_cache;
            SelectedMacro = macro;
            selectedStepIndex = macro.Steps.Count - 1;
            this.IsOpen = true;
            P.ws.AddWindow(this);
            this.Size = new Vector2(600, 600);
            this.SizeCondition = ImGuiCond.Appearing;
            ShowCloseButton = true;

            Crafting.CraftStarted += OnCraftStarted;
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public override void OnClose()
        {
            Crafting.CraftStarted -= OnCraftStarted;
            base.OnClose();
            P.ws.RemoveWindow(this);
        }

        public override void Draw()
        {
            try
            {
                if (SelectedMacro.ID != 0)
                {
                    if (!renameMode)
                    {
                        ImGui.TextUnformatted(T("Selected Macro: {0}", SelectedMacro.Name ?? string.Empty));
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                        {
                            renameMode = true;
                        }
                    }
                    else
                    {
                        renameMacro = SelectedMacro.Name!;
                        if (ImGui.InputText("", ref renameMacro, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            SelectedMacro.Name = renameMacro;
                            P.Config.Save();

                            renameMode = false;
                            renameMacro = String.Empty;
                        }
                    }
                    if (ImGui.Button(T("Delete Macro (Hold Ctrl)")) && ImGui.GetIO().KeyCtrl)
                    {
                        if (raphael_cache)
                        {
                            var copy = P.Config.RaphaelSolverCacheV5.Where(kv => kv.Value == SelectedMacro);
                            //really should be just one but is it for sure??
                            foreach (var kv in copy)
                            {
                                P.Config.RaphaelSolverCacheV5.TryRemove(kv);
                            }
                        }
                        else
                        {
                            P.Config.MacroSolverConfig.Macros.Remove(SelectedMacro);
                            foreach (var e in P.Config.RecipeConfigs)
                                if (e.Value.SolverType == typeof(MacroSolverDefinition).FullName && e.Value.SolverFlavour == SelectedMacro.ID)
                                    P.Config.RecipeConfigs.Remove(e.Key); // TODO: do we want to preserve other configs?..
                        }
                        P.Config.Save();
                        SelectedMacro = new();
                        selectedStepIndex = -1;

                        this.IsOpen = false;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button(T("Raw Editor")))
                    {
                        _rawMacro = string.Join("\r\n", SelectedMacro.Steps.Select(x => $"{x.Action.NameOfAction()}"));
                        Raweditor = !Raweditor;
                    }

                    ImGui.SameLine();
                    var exportButton = ImGuiHelpers.GetButtonSize(T("Export Macro"));
                    ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - exportButton.X);

                    if (ImGui.Button($"{T("Export Macro")}###ExportButton"))
                    {
                        ImGui.SetClipboardText(JsonConvert.SerializeObject(SelectedMacro));
                        Notify.Success(T("Macro Copied to Clipboard."));
                    }

                    ImGui.Spacing();
                    if (ImGui.Checkbox(T("Skip quality actions if at 100%"), ref SelectedMacro.Options.SkipQualityIfMet))
                    {
                        P.Config.Save();
                    }
                    ImGuiComponents.HelpMarker(T("Once you're at 100% quality, the macro will skip over all actions relating to quality, including buffs."));
                    ImGui.SameLine();
                    if (ImGui.Checkbox(T("Skip Observes If Not Poor"), ref SelectedMacro.Options.SkipObservesIfNotPoor))
                    {
                        P.Config.Save();
                    }


                    if (ImGui.Checkbox(T("Upgrade Quality Actions"), ref SelectedMacro.Options.UpgradeQualityActions))
                        P.Config.Save();
                    ImGuiComponents.HelpMarker(T("If you get a Good or Excellent condition and your macro is on a step that increases quality (not including Byregot's Blessing) then it will upgrade the action to Precise Touch."));
                    ImGui.SameLine();

                    if (ImGui.Checkbox(T("Upgrade Progress Actions"), ref SelectedMacro.Options.UpgradeProgressActions))
                        P.Config.Save();
                    ImGuiComponents.HelpMarker(T("If you get a Good or Excellent condition and your macro is on a step that increases progress then it will upgrade the action to Intensive Synthesis."));

                    ImGui.PushItemWidth(150f);
                    if (ImGui.InputInt(T("Minimum Craftsmanship"), ref SelectedMacro.Options.MinCraftsmanship))
                        P.Config.Save();
                    ImGuiComponents.HelpMarker(T("Artisan will not start crafting if you do not meet this minimum craftsmanship with this macro selected."));

                    ImGui.PushItemWidth(150f);
                    if (ImGui.InputInt(T("Minimum Control"), ref SelectedMacro.Options.MinControl))
                        P.Config.Save();
                    ImGuiComponents.HelpMarker(T("Artisan will not start crafting if you do not meet this minimum control with this macro selected."));

                    ImGui.PushItemWidth(150f);
                    if (ImGui.InputInt(T("Minimum CP"), ref SelectedMacro.Options.MinCP))
                        P.Config.Save();
                    ImGuiComponents.HelpMarker(T("Artisan will not start crafting if you do not meet this minimum CP with this macro selected."));

                    if (!Raweditor)
                    {
                        if (ImGui.Button(T("Insert New Action ({0})", Skills.BasicSynthesis.NameOfAction())))
                        {
                            SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = Skills.BasicSynthesis });
                            ++selectedStepIndex;
                            P.Config.Save();
                        }

                        if (selectedStepIndex >= 0)
                        {
                            if (ImGui.Button(T("Insert New Action - Same As Previous ({0})", SelectedMacro.Steps[selectedStepIndex].Action.NameOfAction())))
                            {
                                SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = SelectedMacro.Steps[selectedStepIndex].Action });
                                ++selectedStepIndex;
                                P.Config.Save();
                            }
                        }


                        ImGui.Columns(2, "actionColumns", true);
                        ImGui.SetColumnWidth(0, 220f.Scale());
                        ImGuiEx.LineCentered("###MacroActions", () => ImGuiEx.TextUnderlined(T("Macro Actions")));
                        ImGui.Indent();
                        for (int i = 0; i < SelectedMacro.Steps.Count; i++)
                        {
                            var step = SelectedMacro.Steps[i];
                            var selectedAction = ImGui.Selectable($"{i + 1}. {(step.Action == Skills.None ? T("Artisan Recommendation") : step.Action.NameOfAction())}{(step.HasExcludeCondition ? " | " : "")}{(step.HasExcludeCondition && step.ReplaceOnExclude ? step.ReplacementAction.NameOfAction() : step.HasExcludeCondition ? T("Skip") : "")}###selectedAction{i}", i == selectedStepIndex);
                            if (selectedAction)
                                selectedStepIndex = i;
                        }
                        ImGui.Unindent();
                        if (selectedStepIndex >= 0)
                        {
                            var step = SelectedMacro.Steps[selectedStepIndex];

                            ImGui.NextColumn();
                            ImGuiEx.CenterColumnText(T("Selected Action: {0}", step.Action == Skills.None ? T("Artisan Recommendation") : step.Action.NameOfAction()), true);
                            if (selectedStepIndex > 0)
                            {
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
                                {
                                    selectedStepIndex--;
                                }
                            }

                            if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                            {
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight))
                                {
                                    selectedStepIndex++;
                                }
                            }

                            ImGui.Dummy(new Vector2(0, 0));
                            ImGui.SameLine();
                            if (ImGui.Checkbox(T("Skip Upgrades For This Action"), ref step.ExcludeFromUpgrade))
                                P.Config.Save();

                            ImGui.Spacing();
                            ImGuiEx.CenterColumnText(T("Skip on these conditions"), true);

                            ImGui.BeginChild("ConditionalExcludes", new Vector2(ImGui.GetContentRegionAvail().X, step.HasExcludeCondition ? 200f : 100f), false, ImGuiWindowFlags.AlwaysAutoResize);
                            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                            ImGui.Columns(3, border: false);
                            if (ImGui.Checkbox(T("Normal"), ref step.ExcludeNormal))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Poor"), ref step.ExcludePoor))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Good"), ref step.ExcludeGood))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Excellent"), ref step.ExcludeExcellent))
                                P.Config.Save();

                            ImGui.NextColumn();

                            if (ImGui.Checkbox(T("Centered"), ref step.ExcludeCentered))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Sturdy"), ref step.ExcludeSturdy))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Pliant"), ref step.ExcludePliant))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Malleable"), ref step.ExcludeMalleable))
                                P.Config.Save();

                            ImGui.NextColumn();

                            if (ImGui.Checkbox(T("Primed"), ref step.ExcludePrimed))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Good Omen"), ref step.ExcludeGoodOmen))
                                P.Config.Save();
                            if (ImGui.Checkbox(T("Robust"), ref step.ExcludeRobust))
                                P.Config.Save();

                            ImGui.Columns(1);
                            ImGui.PopStyleVar();

                            if (step.HasExcludeCondition)
                            {
                                ImGuiEx.CenterColumnText(T("Exclude options"), true);
                                if (ImGui.Checkbox(T("Instead of skipping replace with:"), ref step.ReplaceOnExclude))
                                    P.Config.Save();

                                if (step.ReplaceOnExclude)
                                {
                                    if (ImGui.BeginCombo("###Select Replacement", step.ReplacementAction.NameOfAction()))
                                    {
                                        if (ImGui.Selectable(T("Artisan Recommendation")))
                                        {
                                            step.ReplacementAction = Skills.None;
                                            P.Config.Save();
                                        }

                                        ImGuiComponents.HelpMarker(T("Uses a recommendation from the appropriate default solver, i.e Standard Recipe Solver for regular recipes, Expert Recipe Solver for expert recipes."));

                                        if (ImGui.Selectable(T("Touch Combo")))
                                        {
                                            step.ReplacementAction = Skills.TouchCombo;
                                            P.Config.Save();
                                        }

                                        ImGuiComponents.HelpMarker(T("This will use the appropriate step of the 3-step touch combo, depending on the last action actually used. Useful if upgrading quality actions or skipping on conditions."));

                                        if (ImGui.Selectable(T("Touch Combo (Refined Touch Route)")))
                                        {
                                            step.ReplacementAction = Skills.TouchComboRefined;
                                            P.Config.Save();
                                        }

                                        ImGuiComponents.HelpMarker(T("Similar to the other touch combo, this will alternate between Basic Touch & Refined Touch depending on the previous action used."));

                                        ImGui.Separator();

                                        foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>().OrderBy(y => y.NameOfAction()))
                                        {
                                            if (IsSpecialMacroAliasAction(opt))
                                                continue;

                                            if (ImGui.Selectable(opt.NameOfAction()))
                                            {
                                                step.ReplacementAction = opt;
                                                P.Config.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }
                            }
                            ImGui.EndChild();

                            if (ImGui.Button(T("Delete Action (Hold Ctrl)")) && ImGui.GetIO().KeyCtrl)
                            {
                                SelectedMacro.Steps.RemoveAt(selectedStepIndex);
                                P.Config.Save();
                                if (selectedStepIndex == SelectedMacro.Steps.Count)
                                    selectedStepIndex--;
                            }

                            if (ImGui.BeginCombo("###ReplaceAction", T("Replace Action")))
                            {
                                if (ImGui.Selectable(T("Artisan Recommendation")))
                                {
                                    step.Action = Skills.None;
                                    P.Config.Save();
                                }

                                ImGuiComponents.HelpMarker(T("Uses a recommendation from the appropriate default solver, i.e Standard Recipe Solver for regular recipes, Expert Recipe Solver for expert recipes."));

                                if (ImGui.Selectable(T("Touch Combo")))
                                {
                                    step.Action = Skills.TouchCombo;
                                    P.Config.Save();
                                }

                                ImGuiComponents.HelpMarker(T("This will use the appropriate step of the 3-step touch combo, depending on the last action actually used. Useful if upgrading quality actions or skipping on conditions."));

                                if (ImGui.Selectable(T("Touch Combo (Refined Touch Route)")))
                                {
                                    step.Action = Skills.TouchComboRefined;
                                    P.Config.Save();
                                }

                                ImGuiComponents.HelpMarker(T("Similar to the other touch combo, this will alternate between Basic Touch & Refined Touch depending on the previous action used."));

                                ImGui.Separator();

                                foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>().OrderBy(y => y.NameOfAction()))
                                {
                                    if (IsSpecialMacroAliasAction(opt))
                                        continue;

                                    if (ImGui.Selectable(opt.NameOfAction()))
                                    {
                                        step.Action = opt;
                                        P.Config.Save();
                                    }
                                }

                                ImGui.EndCombo();
                            }

                            ImGui.Text(T("Re-order Action"));
                            if (selectedStepIndex > 0)
                            {
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                                {
                                    SelectedMacro.Steps.Reverse(selectedStepIndex - 1, 2);
                                    selectedStepIndex--;
                                    P.Config.Save();
                                }
                            }

                            if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                            {
                                ImGui.SameLine();
                                if (selectedStepIndex == 0)
                                {
                                    ImGui.Dummy(new Vector2(22));
                                    ImGui.SameLine();
                                }

                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                                {
                                    SelectedMacro.Steps.Reverse(selectedStepIndex, 2);
                                    selectedStepIndex++;
                                    P.Config.Save();
                                }
                            }

                        }
                        ImGui.Columns(1);
                    }
                    else
                    {
                        ImGui.Text(T("Macro Actions (line per action)"));
                        ImGuiComponents.HelpMarker(T("You can either copy/paste macros directly as you would a normal game macro, or list each action on its own per line.\nFor example:\n/ac Muscle Memory\n\nis the same as\n\nMuscle Memory\n\nYou can also use * (asterisk) or 'Artisan Recommendation' to insert Artisan's recommendation as a step."));
                        ImGui.InputTextMultiline("###MacroEditor", ref _rawMacro, 10000000, new Vector2(ImGui.GetContentRegionAvail().X - 30f, ImGui.GetContentRegionAvail().Y - 30f));
                        if (ImGui.Button(T("Save")))
                        {
                            var steps = MacroUI.ParseMacro(_rawMacro);
                            if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                            {
                                selectedStepIndex = steps.Count - 1;
                                SelectedMacro.Steps = steps;
                                P.Config.Save();
                                DuoLog.Information(T("Macro Updated"));
                            }
                        }
                        ImGui.SameLine();
                        if (ImGui.Button(T("Save and Close")))
                        {
                            var steps = MacroUI.ParseMacro(_rawMacro);
                            if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                            {
                                selectedStepIndex = steps.Count - 1;
                                SelectedMacro.Steps = steps;
                                P.Config.Save();
                                DuoLog.Information(T("Macro Updated"));
                            }

                            Raweditor = !Raweditor;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button(T("Close")))
                        {
                            Raweditor = !Raweditor;
                        }
                    }


                    ImGuiEx.LineCentered("MTimeHead", delegate
                    {
                        ImGuiEx.TextUnderlined(T("Estimated Macro Length"));
                    });
                    ImGuiEx.LineCentered("MTimeArtisan", delegate
                    {
                        ImGuiEx.Text(T("Artisan: {0} seconds", MacroUI.GetMacroLength(SelectedMacro)));
                    });
                    ImGuiEx.LineCentered("MTimeTeamcraft", delegate
                    {
                        ImGuiEx.Text(T("Normal Macro: {0} seconds", MacroUI.GetTeamcraftMacroLength(SelectedMacro)));
                    });
                }
                else
                {
                    selectedStepIndex = -1;
                }
            }
            catch { }
        }

        private static bool IsSpecialMacroAliasAction(Skills skill)
            => skill is Skills.None or Skills.TouchCombo or Skills.TouchComboRefined;

        private void OnCraftStarted(Lumina.Excel.Sheets.Recipe recipe, CraftState craft, StepState initialStep, bool trial) => IsOpen = false;
    }
}
