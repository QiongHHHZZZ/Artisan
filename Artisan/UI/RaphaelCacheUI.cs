using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.UI.Tables;
using Dalamud.Bindings.ImGui;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using System.Linq;
using System.Numerics;

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
                if (Svc.ClientState.IsLoggedIn && Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween)
                {
                    ImGui.Text(T("正在制作中，停止制作前无法使用宏设置。"));
                    return;
                }

                ImGui.TextWrapped(T("此标签页显示当前已保存的 Raphael 生成宏。"));
                ImGui.Separator();
                ImGui.TextWrapped(T("当前保存的宏：{0}", P.Config.RaphaelSolverCacheV6.Keys.Count));
                ImGui.Spacing();

                if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 32f.Scale()), true))
                {
                    Table ??= new(P.Config.RaphaelSolverCacheV6.Keys.ToList());
                    Table.Draw(ImGui.GetTextLineHeightWithSpacing() - 4f);
                }

                ImGui.EndChild();

                var filterActive = Table.FilteredItems.Count != 0 && Table.FilteredItems.Count != P.Config.RaphaelSolverCacheV6.Keys.Count;
                var filterCount = filterActive ? $"{Table.FilteredItems.Count} " : string.Empty;

                if (!filterActive)
                    ImGui.BeginDisabled();

                if (ImGuiEx.ButtonCtrl(T("删除筛选出的 {0} 个宏", filterCount), new Vector2(ImGui.GetContentRegionAvail().X / 2, ImGui.GetContentRegionAvail().Y)))
                {
                    foreach (var (key, _) in Table.FilteredItems.ToList())
                        P.Config.RaphaelSolverCacheV6.TryRemove(key, out _);

                    P.Config.Save();
                    Table = new(P.Config.RaphaelSolverCacheV6.Keys.ToList());
                }

                if (!filterActive)
                    ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGuiEx.ButtonCtrl(T("清空整个缓存"), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y)))
                {
                    P.Config.RaphaelSolverCacheV6.Clear();
                    P.Config.Save();
                    Table = new(P.Config.RaphaelSolverCacheV6.Keys.ToList());
                }
            }
            catch { }
        }
    }
}
