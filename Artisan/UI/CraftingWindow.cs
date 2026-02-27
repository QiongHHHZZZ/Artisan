using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using Dalamud.Bindings.ImGui;
using System;
using ECommons;

namespace Artisan.UI
{
    internal class CraftingWindow : Window, IDisposable
    {
        public bool RepeatTrial;
        private DateTime _estimatedCraftEnd;

        public CraftingWindow() : base($"{L10n.Tr("Artisan Crafting Window")}###MainCraftWindow", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new System.Numerics.Vector2(150f, 0f),
                MaximumSize = new System.Numerics.Vector2(310f, 500f)
            };

            CraftingProcessor.SolverStarted += OnSolverStarted;
            CraftingProcessor.SolverFailed += OnSolverFailed;
            CraftingProcessor.SolverFinished += OnSolverFinished;
            CraftingProcessor.RecommendationReady += OnRecommendationReady;

            this.TitleBarButtons.Add(new()
            {
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGuiEx.SetTooltip(L10n.Tr("Open Config")),
                Click = (x) => P.PluginUi.IsOpen = true,
            });
        }

        public void Dispose()
        {
            CraftingProcessor.SolverStarted -= OnSolverStarted;
            CraftingProcessor.SolverFailed -= OnSolverFailed;
            CraftingProcessor.SolverFinished -= OnSolverFinished;
            CraftingProcessor.RecommendationReady -= OnRecommendationReady;
        }

        public override bool DrawConditions()
        {
            bool crafting = Crafting.CurState is Crafting.State.InProgress or Crafting.State.QuickCraft or Crafting.State.WaitAction;
            bool waitingForRaph = RaphaelCache.InProgressAny() && Crafting.CurState is Crafting.State.WaitStart;
            return crafting || waitingForRaph;
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

        public override void Draw()
        {
            try
            {
                if (RaphaelCache.InProgressAny())
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, L10n.Tr("Raphael is generating. Please wait..."));
                    if (ImGui.Button(L10n.Tr("Emergency Cancel Button")))
                    {
                        foreach (var t in RaphaelCache.Tasks)
                        {
                            try
                            {
                                t.Value.Cancellation.Cancel();
                            }
                            catch (Exception e)
                            {
                                e.Log("Emergency button pushed but couldn't cancel?");
                            }
                        }
                        RaphaelCache.Tasks.Clear();
                    }
                    return;
                }

                if (!P.Config.DisableHighlightedAction)
                    Hotbars.MakeButtonsGlow(CraftingProcessor.NextRec.Action);

                if (Crafting.CurCraft != null && !Crafting.CurCraft.CraftExpert && Crafting.CurRecipe?.SecretRecipeBook.RowId > 0 && Crafting.CurCraft?.CraftLevel == Crafting.CurCraft?.StatLevel && !CraftingProcessor.ActiveSolver.IsType<MacroSolver>())
                {
                    ImGui.Dummy(new System.Numerics.Vector2(12f));
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, L10n.Tr("This is a current level master recipe. Your success rate may vary so it is recommended to use an Artisan macro or manually solve this."));
                }

                bool autoMode = P.Config.AutoMode;
                if (ImGui.Checkbox(L10n.Tr("Auto Action Mode"), ref autoMode))
                {
                    P.Config.AutoMode = autoMode;
                    P.Config.Save();
                }

                if (autoMode && !P.Config.ReplicateMacroDelay)
                {
                    var delay = P.Config.AutoDelay;
                    ImGui.Text(L10n.Tr("Set delay (ms)"));
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.SliderInt("##SetDelayMs", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        P.Config.AutoDelay = delay;
                        P.Config.Save();
                    }
                }

                if (Endurance.RecipeID != 0 && !CraftingListUI.Processing && Endurance.Enable)
                {
                    if (ImGui.Button(L10n.Tr("Disable Endurance")))
                    {
                        Endurance.ToggleEndurance(false);
                        P.TM.Abort();
                        CraftingListFunctions.CLTM.Abort();
                        PreCrafting.Tasks.Clear();
                    }
                }

                if (!Endurance.Enable && Crafting.IsTrial)
                    ImGui.Checkbox(L10n.Tr("Trial Craft Repeat"), ref RepeatTrial);

                if (CraftingProcessor.ActiveSolver)
                {
                    var solverName = L10n.Tr(CraftingProcessor.ActiveSolver.Name);
                    var solverLine = L10n.Tr("Using {0}", solverName).Replace("%", "");
                    ImGui.TextUnformatted(solverLine);

                    if (CraftingProcessor.NextRec.Comment.Length > 0)
                    {
                        var commentLine = L10n.Tr(CraftingProcessor.NextRec.Comment).Replace("%", "");
                        ImGui.TextUnformatted(commentLine);
                    }
                }

                if (P.Config.CraftingX && Endurance.Enable)
                    ImGui.Text(L10n.Tr("Remaining Crafts: {0}", P.Config.CraftX));

                if (_estimatedCraftEnd != default)
                {
                    var diff = _estimatedCraftEnd - DateTime.Now;
                    string duration = string.Format("{0:D2}h {1:D2}m {2:D2}s", diff.Hours, diff.Minutes, diff.Seconds);
                    ImGui.Text(L10n.Tr("Approximate Remaining Duration: {0}", duration));
                }

                if (!P.Config.AutoMode)
                {
                    ImGui.Text(L10n.Tr("Semi-Manual Mode"));

                    var action = CraftingProcessor.NextRec.Action;
                    using var disable = ImRaii.Disabled(action == Skills.None);

                    if (ImGui.Button(L10n.Tr("Execute recommended action")))
                    {
                        ActionManagerEx.UseSkill(action);
                    }
                    if (ImGui.Button(L10n.Tr("Fetch Recommendation")))
                    {
                        ShowRecommendation(action);
                    }
                }
            }
            catch { } //Idaf about your error windows
        }

        private void ShowRecommendation(Skills action)
        {
            if (!P.Config.DisableToasts)
            {
                QuestToastOptions options = new() { IconId = action.IconOfAction(CharacterInfo.JobID) };
                Svc.Toasts.ShowQuest(L10n.Tr("Use {0}", action.NameOfAction()), options);
            }
        }

        private void OnSolverStarted(Lumina.Excel.Sheets.Recipe recipe, SolverRef solver, CraftState craft, StepState initialStep)
        {
            if (P.Config.AutoMode && solver)
            {
                var estimatedTime = SolverUtils.EstimateCraftTime(solver.Clone()!, craft, initialStep.Quality);
                var count = P.Config.CraftingX && Endurance.Enable ? P.Config.CraftX : 1;
                _estimatedCraftEnd = DateTime.Now + count * estimatedTime;
            }
        }

        private void OnSolverFailed(Lumina.Excel.Sheets.Recipe recipe, string reason)
        {
            var text = L10n.Tr("{0}. Artisan will not continue.", reason);
            Svc.Toasts.ShowError(text);
            DuoLog.Error(text);
        }

        private void OnSolverFinished(Lumina.Excel.Sheets.Recipe? recipe, SolverRef solver, CraftState craft, StepState finalStep)
        {
            _estimatedCraftEnd = default;
        }

        private void OnRecommendationReady(Lumina.Excel.Sheets.Recipe? recipe, SolverRef solver, CraftState craft, StepState step, Solver.Recommendation recommendation)
        {
            if (!Simulator.CanUseAction(craft, step, recommendation.Action))
            {
                return;
            }
            ShowRecommendation(recommendation.Action);
            if (P.Config.AutoMode || Endurance.IPCOverride)
            {
                if (!P.Config.ReplicateMacroDelay)
                    P.CTM.DelayNext(P.Config.AutoDelay);
                P.CTM.Enqueue(() => Crafting.CurState == Crafting.State.InProgress, 3000, true, "WaitForStateToUseAction");
                P.CTM.Enqueue(() => ActionManagerEx.UseSkill(recommendation.Action));
                if (P.Config.ReplicateMacroDelay)
                    P.CTM.DelayNext(Calculations.ActionIsLengthyAnimation(recommendation.Action) ? 3000 : 2000);
            }
        }
    }
}
