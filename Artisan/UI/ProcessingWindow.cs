using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using System;

namespace Artisan.UI
{
    internal class ProcessingWindow : Window
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        public ProcessingWindow() : base($"{L10n.Tr("Processing List")}###ProcessingList", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            SizeCondition = ImGuiCond.Appearing;
        }

        public override bool DrawConditions()
        {
            if (CraftingListUI.Processing)
                return true;

            return false;
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

        public unsafe override void Draw()
        {
            try
            {
                if (CraftingListUI.Processing)
                {
                    CraftingListFunctions.ProcessList(CraftingListUI.selectedList);

                    //if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
                    //{
                    //    P.PluginUi.IsOpen = true;
                    //}

                    ImGui.Text(T("Now Processing: {0}", CraftingListUI.selectedList.Name ?? string.Empty));
                    ImGui.Separator();
                    ImGui.Spacing();
                    if (CraftingListUI.CurrentProcessedItem != 0)
                    {
                        ImGuiEx.TextV(T("Crafting: {0}", LuminaSheets.RecipeSheet[CraftingListUI.CurrentProcessedItem].ItemResult.Value.Name.ToDalamudString().ToString()));
                        ImGuiEx.TextV(T("Current Item Progress: {0} / {1}", CraftingListUI.CurrentProcessedItemCount, CraftingListUI.CurrentProcessedItemListCount));
                        ImGuiEx.TextV(T("Overall List Progress: {0} / {1}", CraftingListFunctions.CurrentIndex + 1, CraftingListUI.selectedList.ExpandedList.Count));

                        string duration = CraftingListFunctions.ListEndTime == TimeSpan.Zero ? T("Unknown") : string.Format("{0:D2}d {1:D2}h {2:D2}m {3:D2}s", CraftingListFunctions.ListEndTime.Days, CraftingListFunctions.ListEndTime.Hours, CraftingListFunctions.ListEndTime.Minutes, CraftingListFunctions.ListEndTime.Seconds);
                        ImGuiEx.TextV(T("Approximate Remaining Duration: {0}", duration));

                    }

                    if (!CraftingListFunctions.Paused)
                    {
                        if (ImGui.Button(T("Pause")))
                        {
                            CraftingListFunctions.Paused = true;
                            P.TM.Abort();
                            CraftingListFunctions.CLTM.Abort();
                            PreCrafting.Tasks.Clear();
                        }
                    }
                    else
                    {
                        if (ImGui.Button(T("Resume")))
                        {
                            if (Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
                            {
                                var recipe = LuminaSheets.RecipeSheet[CraftingListUI.CurrentProcessedItem];
                                PreCrafting.Tasks.Add((() => PreCrafting.TaskSelectRecipe(recipe), default));
                            }

                            CraftingListFunctions.Paused = false;
                        }
                    }

                    ImGui.SameLine();
                    if (ImGui.Button(T("Cancel")))
                    {
                        CraftingListUI.Processing = false;
                        CraftingListFunctions.Paused = false;
                        P.TM.Abort();
                        CraftingListFunctions.CLTM.Abort();
                        PreCrafting.Tasks.Clear();
                        Crafting.CraftFinished -= CraftingListUI.UpdateListTimer;
                    }
                }
            }
            catch { }
        }
    }
}
