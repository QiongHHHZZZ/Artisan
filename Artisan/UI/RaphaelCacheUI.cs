using Artisan.GameInterop;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using System.Linq;
using System.Numerics;
using Artisan.UI.Tables;
using ECommons;
using Artisan.CraftingLogic.Solvers;
using Dalamud.Interface.Utility.Raii;
using System;

namespace Artisan.UI
{
    internal class RaphaelCacheUI
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        public RaphaelCacheTable? Table;

        internal void Draw()
        {
            try
            {
                ImGui.TextWrapped(T("This tab shows all of the currently saved Raphael-generated macros for the currently logged in character."));

                if (Svc.ClientState.IsLoggedIn && Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween)
                {
                    ImGui.Text(T("Crafting in progress. Macro settings will be unavailable until you stop crafting."));
                    return;
                }
                ImGui.Spacing();

                ImGui.TextWrapped(T("Currently saved macros: {0}", RaphaelCache.CurrentCache.Keys.Count));
                ImGui.Spacing();

                using (ImRaii.Child("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 32f.Scale()), true))
                {
                    // todo: search by recipe?
                    if (Table == null)
                    {
                        var cacheList = RaphaelCache.CurrentCache.Keys.ToList();
                        Table = new(cacheList);
                    }
                    Table.Draw(ImGui.GetTextLineHeightWithSpacing() - 4f);
                }

                var filterActive = Table.FilteredItems.Count != 0 && Table.FilteredItems.Count != RaphaelCache.CurrentCache.Keys.Count;

                if (!filterActive) ImGui.BeginDisabled();
                if (ImGuiEx.ButtonCtrl(T("Delete Filtered Macros"), new Vector2(ImGui.GetContentRegionAvail().X / 2, ImGui.GetContentRegionAvail().Y)))
                {
                    var toDelete = Table.FilteredItems.JSONClone();
                    foreach ((RaphaelOptions key, int _) in toDelete)
                    {
                        RaphaelCache.CurrentCache.TryRemove(key, out _);
                    }
                    Table.FilteredItems.Clear();
                    P.Config.Save();
                }
                if (!filterActive) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGuiEx.ButtonCtrl(T("Delete Entire Cache"), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y)))
                {
                    RaphaelCache.CurrentCache.Clear();
                    P.Config.Save();
                }
            }
            catch (Exception ex) { ex.Log(); }
        }
    }
}
