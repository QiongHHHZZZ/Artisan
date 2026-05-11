using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.GameInterop.CSExt;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using TerraFX.Interop.WinRT;
using static ECommons.GenericHelpers;
using RepairManager = Artisan.Autocraft.RepairManager;

namespace Artisan.UI
{
    internal unsafe class DebugTab
    {
        private static string T(string key) => L10n.Tr(key);

        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;
        public static int DebugValue = 1;
        static int NQMats, HQMats = 0;

        internal static void Draw()
        {
            try
            {
                ImGui.Checkbox("调试日志", ref Debug);
                ImGui.Checkbox("追踪专家状态", ref P.Config.DebugTrackConditionData);
                if (ImGui.CollapsingHeader("生产职业食物"))
                {
                    foreach (var x in ConsumableChecker.GetFood())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包中的生产食物"))
                {
                    foreach (var x in ConsumableChecker.GetFood(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("背包中的高品质生产食物"))
                {
                    foreach (var x in ConsumableChecker.GetFood(true, true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id, true);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("生产职业药水"))
                {
                    foreach (var x in ConsumableChecker.GetPots())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包中的生产药水"))
                {
                    foreach (var x in ConsumableChecker.GetPots(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("背包中的高品质生产药水"))
                {
                    foreach (var x in ConsumableChecker.GetPots(true, true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id, true);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("手册"))
                {
                    foreach (var x in ConsumableChecker.GetManuals())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包中的手册"))
                {
                    foreach (var x in ConsumableChecker.GetManuals(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("军队手册"))
                {
                    foreach (var x in ConsumableChecker.GetSquadronManuals())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包中的军队手册"))
                {
                    foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("配方配置"))
                {
                    if (ImGui.Button("清空（按住 Ctrl）") && ImGui.GetIO().KeyCtrl)
                    {
                        P.Config.RecipeConfigs.Clear();
                        P.Config.Save();
                    }
                    ImGui.BeginTable("DebugeRcipeConfigs", 9);
                    ImGui.TableHeader(T("DebugRecipeConfigs"));
                    ImGui.TableNextColumn();
                    ImGui.Text("物品");
                    ImGui.TableNextColumn();
                    ImGui.Text("需求食物");
                    ImGui.TableNextColumn();
                    ImGui.Text("高品质？");
                    ImGui.TableNextColumn();
                    ImGui.Text("需求药水");
                    ImGui.TableNextColumn();
                    ImGui.Text("高品质？");
                    ImGui.TableNextColumn();
                    ImGui.Text("需求手册");
                    ImGui.TableNextColumn();
                    ImGui.Text("需求军队手册");
                    ImGui.TableNextColumn();
                    ImGui.Text("求解器类型");
                    ImGui.TableNextColumn();
                    ImGui.Text("求解器风格");

                    foreach (var (k, v) in P.Config.RecipeConfigs)
                    {
                        ImGui.TableNextRow();
                        var recipe = LuminaSheets.RecipeSheet[k];
                        ImGui.TableNextColumn();
                        ImGui.Text(recipe.ItemResult.Value.Name.ToDalamudString().ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.requiredFood}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.requiredFoodHQ}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.requiredPotion}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.requiredPotionHQ}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.requiredManual}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.requiredSquadronManual}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.CurrentSolverType.Split('.').Last()}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v.CurrentSolverFlavour}");
                    }
                    ImGui.EndTable();
                }
                if (ImGui.CollapsingHeader("基础属性"))
                {
                    ImGui.Text($"{CharacterStats.GetCurrentStats()}");
                }

                if (ImGui.CollapsingHeader("制作属性") && Crafting.CurCraft != null && Crafting.CurStep != null)
                {
                    ImGui.Text($"加工精度：{Crafting.CurCraft.StatControl}");
                    ImGui.Text($"作业精度：{Crafting.CurCraft.StatCraftsmanship}");
                    ImGui.Text($"当前耐久：{Crafting.CurStep.Durability}");
                    ImGui.Text($"最大耐久：{Crafting.CurCraft.CraftDurability}");
                    ImGui.Text($"当前进展：{Crafting.CurStep.Progress}");
                    ImGui.Text($"最大进展：{Crafting.CurCraft.CraftProgress}");
                    ImGui.Text($"当前品质：{Crafting.CurStep.Quality}");
                    ImGui.Text($"最大品质：{Crafting.CurCraft.CraftQualityMax}");
                    ImGui.Text($"品质百分比：{Calculations.GetHQChance(Crafting.CurStep.Quality * 100.0 / Crafting.CurCraft.CraftQualityMax)}");
                    ImGui.Text($"物品名：{Crafting.CurRecipe?.ItemResult.Value.Name.ToDalamudString()}");
                    ImGui.Text($"当前状态：{Crafting.CurStep.Condition}");
                    ImGui.Text($"当前步骤：{Crafting.CurStep.Index}");
                    ImGui.Text($"快速制作：{Crafting.QuickSynthState.Cur} / {Crafting.QuickSynthState.Max}");
                    ImGui.Text($"阔步+比尔格 连段：{StandardSolver.GreatStridesByregotCombo(Crafting.CurCraft, Crafting.CurStep)}");
                    ImGui.Text($"基础品质：{Simulator.BaseQuality(Crafting.CurCraft)}");
                    ImGui.Text($"基础进展：{Simulator.BaseProgress(Crafting.CurCraft)}");
                    ImGui.Text($"预测品质：{StandardSolver.CalculateNewQuality(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action)}");
                    ImGui.Text($"预测进展：{StandardSolver.CalculateNewProgress(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action)}");
                    ImGui.Text($"收藏价值低档：{Crafting.CurCraft.CraftQualityMin1}");
                    ImGui.Text($"收藏价值中档：{Crafting.CurCraft.CraftQualityMin2}");
                    ImGui.Text($"收藏价值高档：{Crafting.CurCraft.CraftQualityMin3}");
                    ImGui.Text($"制作状态：{Crafting.CurState}");
                    ImGui.Text($"可否收尾：{StandardSolver.CanFinishCraft(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action)}");
                    ImGui.Text($"当前推荐：{CraftingProcessor.NextRec.Action.NameOfAction()}");
                    ImGui.Text($"上一步动作：{Crafting.CurStep.PrevComboAction.NameOfAction()}");
                    ImGui.Text($"是否可首回合直接精修：{Crafting.CurStep.Index == 1 && StandardSolver.CanFinishCraft(Crafting.CurCraft, Crafting.CurStep, Skills.DelicateSynthesis) && StandardSolver.CalculateNewQuality(Crafting.CurCraft, Crafting.CurStep, Skills.DelicateSynthesis) >= Crafting.CurCraft.CraftQualityMin3}");
                    ImGui.Text($"条件标记：{Crafting.CurCraft.ConditionFlags}");
                    ImGui.Text($"神奇材料剩余层数：{Crafting.CurStep.MaterialMiracleCharges}");
                    ImGui.Text($"稳手剩余充能：{Crafting.CurStep.SteadyHandCharges}");
                    ImGui.Text($"稳手剩余层数：{Crafting.CurStep.SteadyHandLeft}");
                }

                if (ImGui.CollapsingHeader("精炼度"))
                {
                    ImGui.Text($"主手精炼度：{Spiritbond.Weapon}");
                    ImGui.Text($"副手精炼度：{Spiritbond.Offhand}");
                    ImGui.Text($"头部精炼度：{Spiritbond.Helm}");
                    ImGui.Text($"身体精炼度：{Spiritbond.Body}");
                    ImGui.Text($"手部精炼度：{Spiritbond.Hands}");
                    ImGui.Text($"腿部精炼度：{Spiritbond.Legs}");
                    ImGui.Text($"脚部精炼度：{Spiritbond.Feet}");
                    ImGui.Text($"耳环精炼度：{Spiritbond.Earring}");
                    ImGui.Text($"项链精炼度：{Spiritbond.Neck}");
                    ImGui.Text($"手镯精炼度：{Spiritbond.Wrist}");
                    ImGui.Text($"戒指 1 精炼度：{Spiritbond.Ring1}");
                    ImGui.Text($"戒指 2 精炼度：{Spiritbond.Ring2}");

                    ImGui.Text($"是否有任意部位可精炼：{Spiritbond.IsSpiritbondReadyAny()}");

                }

                if (ImGui.CollapsingHeader("任务"))
                {
                    QuestManager* qm = QuestManager.Instance();
                    foreach (var quest in qm->DailyQuests)
                    {
                        ImGui.TextWrapped($"任务 ID：{quest.QuestId}，阶段：{QuestManager.GetQuestSequence(quest.QuestId)}，名称：{quest.QuestId.NameOfQuest()}，标记：{quest.Flags}");
                    }

                }

                if (ImGui.CollapsingHeader("IPC 调试"))
                {
                    ImGui.Text($"AutoRetainer：{AutoRetainerIPC.IsEnabled()}");
                    if (ImGui.Button("抑制"))
                    {
                        AutoRetainerIPC.Suppress();
                    }
                    if (ImGui.Button("取消抑制"))
                    {
                        AutoRetainerIPC.Unsuppress();
                    }

                    ImGui.Text($"耐力模式 IPC：{Svc.PluginInterface.GetIpcSubscriber<bool>("Artisan.GetEnduranceStatus").InvokeFunc()}");
                    ImGui.Text($"清单 IPC：{Svc.PluginInterface.GetIpcSubscriber<bool>("Artisan.IsListRunning").InvokeFunc()}");
                    if (ImGui.Button("启用"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(true);
                    }
                    if (ImGui.Button("禁用"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(false);
                    }

                    if (ImGui.Button("发送停止请求（true）"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(true);
                    }

                    if (ImGui.Button("发送停止请求（false）"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(false);
                    }

                    if (ImGui.Button($"停止 Navmesh"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Stop").InvokeAction();
                    }

                    ImGui.Text($"ATools 已安装：{RetainerInfo.AToolsInstalled}");
                    ImGui.Text($"ATools 已启用：{RetainerInfo.AToolsEnabled}");
                    ImGui.Text($"ATools 可用：{RetainerInfo.ATools}");

                    if (ImGui.Button("Artisan 执行 Craft X"))
                    {
                        IPC.IPC.CraftX(Endurance.RecipeID, 1);
                    }
                    ImGui.Text($"IPC 覆盖：{Endurance.IPCOverride}");

                    if (ImGui.Button("当前配方切换到 Raphael（临时）"))
                    {
                        IPC.IPC.ChangeSolver(Endurance.RecipeID, "Raphael Recipe Solver", true);
                    }
                    if (ImGui.Button("当前配方仅使用 Raphael（永久）"))
                    {
                        IPC.IPC.ChangeSolver(Endurance.RecipeID, "Raphael Recipe Solver", false);
                    }
                    if (ImGui.Button("重置回非临时求解器"))
                    {
                        IPC.IPC.SetTempSolverBackToNormal(Endurance.RecipeID);
                    }
                }

                if (ImGui.CollapsingHeader("收藏品"))
                {
                    foreach (var item in LuminaSheets.ItemSheet.Values.Where(x => x.IsCollectable).OrderBy(x => x.LevelItem.RowId))
                    {
                        if (Svc.Data.GetSubrowExcelSheet<CollectablesShopItem>().SelectMany(x => x).TryGetFirst(x => x.Item.RowId == item.RowId, out var collectibleSheetItem))
                        {
                            ImGui.Text($"{item.Name} - {collectibleSheetItem.CollectablesShopRewardScrip.Value.LowReward}");
                        }
                    }
                }

                if (ImGui.CollapsingHeader("配方笔记"))
                {
                    var recipes = RecipeNoteRecipeData.Ptr();

                    byte* basePtr = (byte*)recipes;
                    byte* targetPtr = basePtr + 1112 + DebugValue;

                    // Suppose the value at that offset is an int:
                    ushort value = *(ushort*)targetPtr;
                    ImGui.Text($"{*targetPtr}");
                    if (recipes != null && recipes->Recipes != null)
                    {
                        if (recipes->SelectedIndex < recipes->RecipesCount)
                            DrawRecipeEntry($"已选中 {recipes->SelectedIndex}", recipes->Recipes + recipes->SelectedIndex);
                        for (int i = 0; i < recipes->RecipesCount; ++i)
                            DrawRecipeEntry(i.ToString(), recipes->Recipes + i);
                    }
                    else
                    {
                        ImGui.TextUnformatted($"空指针：{(nint)recipes:X}");
                    }
                }

                if (ImGui.CollapsingHeader("装备"))
                {
                    ImGui.TextUnformatted($"游戏内属性：{CharacterInfo.Craftsmanship}/{CharacterInfo.Control}/{CharacterInfo.MaxCP}/{CharacterInfo.FCCraftsmanshipbuff}");
                    DrawEquippedGear();
                    foreach (ref var gs in RaptureGearsetModule.Instance()->Entries)
                        DrawGearset(ref gs);
                }

                if (ImGui.CollapsingHeader("修理"))
                {
                    if (ImGui.Button("全部修理"))
                    {
                        RepairManager.ProcessRepair();
                    }
                    ImGuiEx.Text($"装备耐久：{RepairManager.GetMinEquippedPercent()}");

                    ImGui.Text($"可否修理：{(LuminaSheets.ItemSheet.ContainsKey((uint)DebugValue) ? LuminaSheets.ItemSheet[(uint)DebugValue].Name : "")} {RepairManager.CanRepairItem((uint)DebugValue)}");
                    ImGui.Text($"是否可修理任意装备：{RepairManager.CanRepairAny()}");
                    ImGui.Text($"附近是否有修理 NPC：{RepairManager.RepairNPCNearby(out _)}");

                    if (ImGui.Button("与修理 NPC 交互"))
                    {
                        P.TM.Enqueue(() => RepairManager.InteractWithRepairNPC(), "RepairManagerDebug");
                    }

                    ImGui.Text($"修理价格：{RepairManager.GetNPCRepairPrice()}");

                }
                if (ImGui.CollapsingHeader(T("Recipe Level Completion")))
                {
                    ImGui.Columns(8);
                    for (int i = (int)Job.CRP; i <= (int)Job.CUL; i++)
                    {
                        var j = (Job)i;
                        for (uint l = 0; l <= 39; l++)
                        {
                            var sheet = Svc.Data.GetExcelSheet<RecipeNotebookList>();
                            uint row = (uint)(((i - 8)  * 40) + l);
                            if (sheet.TryGetRow(row, out var d))
                            {
                                var division = Svc.Data.GetExcelSheet<NotebookDivision>().GetRow(l);
                                int count = d.Count;
                                if (count == 0)
                                    continue;

                                int completed = 0;
                                for (int r = 0; r < count; r++)
                                {
                                    var recipe = d.Recipe[r];
                                    if (P.ri.HasRecipeCrafted(recipe.RowId))
                                        completed++;
                                }

                                ImGui.Text($"{j} - {division.Name} | {completed}/{count}");
                            }
                        }
                        ImGui.NextColumn();
                    }
                    ImGui.Columns(1);
                }
                if (ImGui.CollapsingHeader("Expert Condition Data"))
                {
                    Dictionary<string, string> recipeNames = new Dictionary<string, string>()
                    {
                        { "None-60/3856/12323",    "Artisanal 2" },
                        { "None-50/4672/15656",    "Artisanal 3" },
                        { "None-55/5059/15474",    "Artisanal 4" },
                        { "None-60/4220/14618",    "Oddly Delicate Skysung" },  // todo
                        { "None-55/5077/14321",    "Resplendent A" }, // todo
                        { "None-55/5095/14854",    "Resplendent B" }, // todo
                        { "None-60/5470/16156",    "Resplendent C" }, // todo
                        { "None-60/7480/13620",    "Otter Fountain" },
                        { "None-60/7920/17240",    "Smaller Otter Fountain" },
                        { "None-60/6600/15368",    "Connoisseur's Brilliant" },
                        { "None-60/7040/16308",    "Connoisseur's Vrandtic" },
                        { "None-60/8800/18040",    "Uncharted Course" }, // todo
                        { "None-70/9240/20066",    "Aetherial Arbor" },
                        { "None-80/9900/20300",    "Moon A (mixed 1 craft)" },
                        { "MM-60/400/21000",       "Moon A (low prog)" },
                        { "None-70/7500/15000",    "Moon A (mixed 3 craft)" },
                        { "MM-80/9000/23000",      "Moon EX (80 dura)" },
                        { "MM-70/8700/22000",      "Moon EX (70 dura)" },
                        { "MM-30/7300/18900",      "Moon EX+ I Pre" },
                        { "MM-55/10000/27400",     "Moon EX+ I Main" },
                        { "MM-35/6500/18000",      "Moon EX+ II Pre" },
                        { "MM-60/9500/26800",      "Moon EX+ II Main" },
                        { "MM-40/8500/19500",      "Moon EX+ III Pre" },
                        { "MM-70/11400/29800",     "Moon EX+ III Main" },
                        { "None-55/10000/12000",   "Moon EX Weather" },
                        { "MM-20/4700/14900",      "Moon EX+ Weather MM Pre" },
                        { "MM-60/9500/24000",      "Moon EX+ Weather MM Main" },
                        { "None-20/4700/14900",    "Moon EX+ Weather None Pre" },
                        { "None-60/9300/22500",    "Moon EX+ Weather None Main" },
                        { "None-80/5200/13700",    "Phaenna A (1 craft)" },
                        { "None-70/4000/11200",    "Phaenna A (3 crafts)" },
                        { "MM-60/500/15100",       "Phaenna A Miracle" },
                        { "None-55/9700/11600",    "Phaenna EX-A" },
                        { "None-60/600/21100",     "Phaenna EX-A III" },
                        { "MM-65/4200/14600",      "Phaenna EX-A Timed II" },
                        { "MM-30/7000/19500",      "Phaenna EX+ I Pre" },
                        { "MM-55/10200/27900",     "Phaenna EX+ I Main" },
                        { "MM-35/6500/19200",      "Phaenna EX+ II Pre" },
                        { "MM-60/9500/27600",      "Phaenna EX+ II Main" },
                        { "MM-40/8900/23000",      "Phaenna EX+ III Pre" },
                        { "MM-65/12000/32300",     "Phaenna EX+ III Main" }, // todo?
                        { "None-60/1700/29700",    "Phaenna EX+ Timed Pre" },
                        { "MM-70/2300/24700",      "Oizys EX-A Miracle" },
                        { "Steady-70/9700/17300",  "Oizys EX-A Steady" },
                        { "None-60/8500/19500",    "Oizys EX-A No Action" },
                        { "None-70/8200/28300",    "Oizys EX+ Timed" },
                        { "Steady-35/7500/21000",  "Oizys EX+ I Pre" },
                        { "Steady-50/9800/27900",  "Oizys EX+ I Main" },
                        { "Steady-35/8000/22000",  "Oizys EX+ II Pre" },
                        { "Steady-60/11000/29100", "Oizys EX+ II Main" },
                        { "Steady-50/9000/23000",  "Oizys EX+ III Pre" },
                        { "Steady-70/12500/33500", "Oizys EX+ III Main" },
                    };
                    List<CraftingLogic.CraftData.Condition> tableConditions = new List<CraftingLogic.CraftData.Condition>()
                    {
                        CraftingLogic.CraftData.Condition.Normal,
                        CraftingLogic.CraftData.Condition.Good,
                        CraftingLogic.CraftData.Condition.Centered,
                        CraftingLogic.CraftData.Condition.Sturdy,
                        CraftingLogic.CraftData.Condition.Pliant,
                        CraftingLogic.CraftData.Condition.Malleable,
                        CraftingLogic.CraftData.Condition.Primed,
                        CraftingLogic.CraftData.Condition.GoodOmen,
                        CraftingLogic.CraftData.Condition.Robust
                    };

                    if (ImGui.Button("Load File"))
                    {
                        if (!P.Config.DebugTrackConditions.IsLoaded)
                            P.Config.DebugTrackConditions.LoadFile();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Refresh"))
                    {
                        P.Config.DebugTrackConditions.TableLoaded = false;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Copy Combined"))
                    {
                        if (P.Config.DebugTrackConditions.IsLoaded && P.Config.DebugTrackConditions.combinedConditionData != null)
                        {
                            string copyStr = "";
                            copyStr += "Conditions\t" + "Flags\t" + "Recipe Stats\t" + "Note\t" + "Total\t";
                            foreach (var cond in tableConditions)
                                copyStr += cond.ToString() + "\t";
                            copyStr += "\r\n";

                            var sortedData = P.Config.DebugTrackConditions.combinedConditionData.OrderBy(x => x.Value.RecipeConditionIDs).ThenBy(x => x.Value.RecipeAction).ThenBy(x => recipeNames.TryGetValue(x.Value.StatsString(), out var note) ? note : "");
                            foreach (var (rowId, rc) in sortedData)
                            {
                                if (rc.RecipeConditionFlags == "MM")
                                    continue;

                                copyStr += rc.RecipeConditionFlags + "\t";
                                copyStr += rc.RecipeConditionIDs.ToString() + "\t";
                                copyStr += rc.StatsString() + "\t";
                                copyStr += (recipeNames.TryGetValue(rc.StatsString(), out var note) ? note : "") + "\t";
                                copyStr += (P.Config.DebugTrackConditions.combinedTotals.TryGetValue(rowId, out int value) ? value : 1) + "\t";
                                foreach (var cond in tableConditions)
                                    copyStr += (rc.Counts.TryGetValue(cond, out int c) ? c : 0) + "\t";
                                copyStr += "\r\n";
                            }
                            ImGui.SetClipboardText(copyStr);
                            Notify.Success("Combined condition data copied to clipboard");
                        }
                    }

                    if (!P.Config.DebugTrackConditions.TableLoaded)
                    {
                        P.Config.DebugTrackConditions.combinedConditionData = new();
                        P.Config.DebugTrackConditions.combinedTotals = new();

                        foreach (var rec in P.Config.DebugTrackConditions.Records)
                        {
                            string combinedId = rec.StatsString() + "-" + rec.RecipeConditionFlags;

                            // combine the MM records
                            if (rec.RecipeID > 1000000)
                            {
                                combinedId = "MM";
                            }

                            if (!P.Config.DebugTrackConditions.combinedConditionData.ContainsKey(combinedId))
                            {
                                P.Config.DebugTrackConditions.combinedConditionData[combinedId] = new RecipeConditions() { 
                                    RecipeDurability = rec.RecipeDurability,
                                    RecipeProgress = rec.RecipeProgress,
                                    RecipeQuality = rec.RecipeQuality,
                                    RecipeConditionFlags = combinedId == "MM" ? "MM" : rec.RecipeConditionFlags,
                                    RecipeConditionIDs = rec.RecipeConditionIDs,
                                    RecipeAction = rec.RecipeAction
                                };
                            }

                            foreach (var (c, n) in rec.Counts)
                            {
                                if (!P.Config.DebugTrackConditions.combinedConditionData[combinedId].Counts.ContainsKey(c))
                                    P.Config.DebugTrackConditions.combinedConditionData[combinedId].Counts[c] = 0;

                                P.Config.DebugTrackConditions.combinedConditionData[combinedId].Counts[c] += n;

                                if (!P.Config.DebugTrackConditions.combinedTotals.ContainsKey(combinedId))
                                    P.Config.DebugTrackConditions.combinedTotals[combinedId] = 0;
                                P.Config.DebugTrackConditions.combinedTotals[combinedId] += n;
                            }
                        }

                        P.Config.DebugTrackConditions.TableLoaded = true;
                    }

                    if (P.Config.DebugTrackConditions.IsLoaded && P.Config.DebugTrackConditions.combinedConditionData != null)
                    {
                        try
                        {
                            ImGui.Text("Combined:");
                            ImGui.BeginTable("ExpertConditionData", 14);

                            ImGui.TableSetupColumn("Conditions", ImGuiTableColumnFlags.WidthFixed, 120.0f);
                            ImGui.TableSetupColumn("Flags", ImGuiTableColumnFlags.WidthFixed, 50.0f);
                            ImGui.TableSetupColumn("Dura/Prog/Qual", ImGuiTableColumnFlags.WidthFixed, 150.0f);
                            ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthFixed, 150.0f);
                            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 50.0f);
                            ImGui.TableSetupColumn("Normal", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Good", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Centered", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Sturdy", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Pliant", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Malleable", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Primed", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Good Omen", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableSetupColumn("Robust", ImGuiTableColumnFlags.WidthStretch, 0.0f);
                            ImGui.TableHeadersRow();

                            var sortedData = P.Config.DebugTrackConditions.combinedConditionData.OrderBy(x => recipeNames.TryGetValue(x.Value.StatsString(), out var note) ? note : "");
                            foreach (var (rowId, rc) in sortedData)
                            {
                                int rowTotal = P.Config.DebugTrackConditions.combinedTotals.TryGetValue(rowId, out int value) ? value : 1;

                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(rc.RecipeConditionFlags);
                                ImGui.TableNextColumn();
                                ImGui.Text(rc.RecipeConditionIDs.ToString());
                                ImGui.TableNextColumn();
                                ImGui.Text(rc.StatsString());
                                ImGui.TableNextColumn();
                                ImGui.Text(recipeNames.TryGetValue(rc.StatsString(), out var note) ? note : "");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{rowTotal}");

                                foreach (var cond in tableConditions)
                                {
                                    ImGui.TableNextColumn();
                                    int count = rc.Counts.TryGetValue(cond, out int c) ? c : 0;
                                    ImGui.Text($"{Math.Round(((double)count / (double)rowTotal) * 100, 0)}% ({count})");
                                }
                            }
                            ImGui.EndTable();
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error($"Error rendering condition table\n{ex}");
                        }
                    }
                }

                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.Separator();
                ImGui.Dummy(new Vector2(0, 5f));

                ImGui.Text($"耐力模式配方：{Endurance.RecipeID} {Endurance.RecipeName}");
                if (ImGui.Button($"打开耐力模式配方"))
                {
                    CraftingListFunctions.OpenRecipeByID(Endurance.RecipeID);
                }
                if (ImGui.Button($"{T("Craft X IPC")}"))
                {
                    IPC.IPC.CraftX((ushort)DebugValue, 1);
                }

                ImGui.InputInt("调试值", ref DebugValue, 1, 10);
                if (ImGui.Button($"打开配方"))
                {
                    PreCrafting.TaskSelectRecipe(Svc.Data.GetExcelSheet<Recipe>().GetRow((uint)DebugValue));
                }

                ImGui.Text($"物品数量：{CraftingListUI.NumberOfIngredient((uint)DebugValue)}");

                ImGui.Text($"是否已完成配方：{((uint)DebugValue).NameOfRecipe()} {P.ri.HasRecipeCrafted((uint)DebugValue)}");

                if (ImGui.Button($"打开并快速制作"))
                {
                    Operations.QuickSynthItem(DebugValue);
                }
                if (ImGui.Button($"关闭快速制作窗口"))
                {
                    Operations.CloseQuickSynthWindow();
                }
                if (ImGui.Button($"打开魔晶石窗口"))
                {
                    Spiritbond.OpenMateriaMenu();
                }
                if (ImGui.Button($"精炼第一颗魔晶石"))
                {
                    Spiritbond.ExtractFirstMateria();
                }
                if (ImGui.Button("调试装备物品"))
                {
                    PreCrafting.TaskEquipItem(49374);
                }

                if (ImGui.Button($"{T("Pandora IPC")}"))
                {
                    var state = Svc.PluginInterface.GetIpcSubscriber<string, bool?>($"PandorasBox.GetFeatureEnabled").InvokeFunc("Auto-Fill Numeric Dialogs");
                    Svc.Log.Debug($"State of Auto-Fill Numeric Dialogs: {state}");
                    Svc.PluginInterface.GetIpcSubscriber<string, bool, object>($"PandorasBox.SetFeatureEnabled").InvokeAction("Auto-Fill Numeric Dialogs", !(state ?? false));
                    state = Svc.PluginInterface.GetIpcSubscriber<string, bool?>($"PandorasBox.GetFeatureEnabled").InvokeFunc("Auto-Fill Numeric Dialogs");
                    Svc.Log.Debug($"State of Auto-Fill Numeric Dialogs after setting: {state}");
                }

                ref var debugOverrideValue = ref Ref<int>.Get("dov", -1);
                ImGui.InputInt("dov", ref debugOverrideValue);
                if (ImGui.Button("设置材料"))
                {
                    CraftingListFunctions.SetIngredients(debugOverride: debugOverrideValue == -1?null: (uint)debugOverrideValue);
                }

                if (TryGetAddonByName<AtkUnitBase>("RetainerHistory", out var addon))
                {
                    var list = addon->UldManager.SearchNodeById(10)->GetAsAtkComponentList();
                    ImGui.Text($"{list->ListLength}");
                }

            }
            catch (Exception e)
            {
                e.Log();
            }

            ImGui.Text($"{Crafting.CurState}");
            ImGui.Text($"{PreCrafting.Tasks.Count()}");
            ImGui.Text($"{P.TM.IsBusy}");
            ImGui.Text($"{CraftingListFunctions.CLTM.IsBusy}");
            if (ImGui.Button($"传送到大国防联军"))
            {
                TeleportToGCTown();
            }

            Util.ShowStruct(WKSManager.Instance());
        }

        public unsafe static void TeleportToGCTown()
        {
            var gc = UIState.Instance()->PlayerState.GrandCompany;
            var aetheryte = gc switch
            {
                0 => 0u,
                1 => 8u,
                2 => 2u,
                3 => 9u,
                _ => 0u
            };
            var ticket = gc switch
            {
                0 => 0u,
                1 => 21069u,
                2 => 21070u,
                3 => 21071u,
                _ => 0u
            };
            if (InventoryManager.Instance()->GetInventoryItemCount(ticket) > 0)
                AgentInventoryContext.Instance()->UseItem(ticket);
            else
                Telepo.Instance()->Teleport(aetheryte, 0);
        }

        private static void DrawRecipeEntry(string tag, RecipeNoteRecipeEntry* e)
        {
            var recipe = Svc.Data.GetExcelSheet<Recipe>()?.GetRow(e->RecipeId);
            using var n = ImRaii.TreeNode($"{tag}: {e->RecipeId} '{recipe?.ItemResult.Value.Name.ToDalamudString()}'###{tag}");
            if (!n)
                return;

            int i = 0;
            foreach (ref var ing in e->IngredientsSpan)
            {
                if (ing.NumTotal != 0)
                {
                    var item = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>()?.GetRow(ing.ItemId);
                    using var n1 = ImRaii.TreeNode($"材料 {i}：{ing.ItemId} '{item?.Name}'（品级={item?.LevelItem.RowId}, 可高品质={item?.CanBeHq}），上限={ing.NumTotal}，普通品质={ing.NumAssignedNQ}/{ing.NumAvailableNQ}，高品质={ing.NumAssignedHQ}/{ing.NumAvailableHQ}###ingy{ing.ItemId}");
                    if (n1)
                    {
                        ImGui.InputInt("普通品质材料", ref NQMats);
                        ImGui.InputInt("高品质材料", ref HQMats);

                        if (ImGui.Button("设置为指定值"))
                        {
                            ing.SetSpecific(NQMats, HQMats);
                        }

                        if (ImGui.Button("设置为最大高品质"))
                        {
                            ing.SetMaxHQ();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("设置为最大普通品质"))
                        {
                            ing.SetMaxNQ();
                        }
                    }
                }
                i++;
            }

            if (recipe != null)
            {
                var startingQuality = Calculations.GetStartingQuality(recipe.Value, e->GetAssignedHQIngredients());
                using var n2 = ImRaii.TreeNode($"起始品质：{startingQuality}/{Calculations.RecipeMaxQuality(recipe.Value)}", ImGuiTreeNodeFlags.Leaf);
            }

            Util.ShowObject(recipe!.Value.RecipeLevelTable.Value);
        }

        private static void DrawEquippedGear()
        {
            using var nodeEquipped = ImRaii.TreeNode("已装备栏位");
            if (!nodeEquipped)
                return;

            var stats = CharacterStats.GetBaseStatsEquipped();
            ImGui.TextUnformatted($"总属性：{stats.Craftsmanship}/{stats.Control}/{stats.CP}（制作力）/{stats.SplendorCosmic}/{stats.Specialist}");

            var inventory = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (inventory == null)
                return;

            for (int i = 0; i < inventory->Size; ++i)
            {
                var item = inventory->Items + i;
                var details = new ItemStats(item);
                if (details.Data == null)
                    continue;

                using var n = ImRaii.TreeNode($"{i}: {item->ItemId} '{details.Data.Value.Name}' ({item->Flags}): crs={details.Stats[0].Base}+{details.Stats[0].Melded}/{details.Stats[0].Max}, ctrl={details.Stats[1].Base}+{details.Stats[1].Melded}/{details.Stats[1].Max}, cp={details.Stats[2].Base}+{details.Stats[2].Melded}/{details.Stats[2].Max}");
                if (n)
                {
                    ImGui.Text($"{details.Data.Value.LevelEquip} {details.Data.Value.Rarity}");
                    for (int j = 0; j < 5; ++j)
                    {
                        using var m = ImRaii.TreeNode($"魔晶石 {j}：{item->Materia[j]} {item->MateriaGrades[j]}", ImGuiTreeNodeFlags.Leaf);
                    }
                }
            }
        }

        private static void DrawGearset(ref RaptureGearsetModule.GearsetEntry gs)
        {
            if (!gs.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                return;

            fixed (byte* name = gs.Name)
            {
                using var nodeGearset = ImRaii.TreeNode($"装备套组 {gs.Id} '{Dalamud.Memory.MemoryHelper.ReadString((nint)name, 48)}' {(Job)gs.ClassJob} ({gs.Flags})");
                if (!nodeGearset)
                    return;

                var stats = CharacterStats.GetBaseStatsGearset(ref gs);
                ImGui.TextUnformatted($"总属性：{stats.Craftsmanship}/{stats.Control}/{stats.CP}（制作力）/{stats.SplendorCosmic}/{stats.Specialist}");

                for (int i = 0; i < gs.Items.Length; ++i)
                {
                    ref var item = ref gs.Items[i];
                    var details = new ItemStats((RaptureGearsetModule.GearsetItem*)Unsafe.AsPointer(ref item));
                    if (details.Data == null)
                        continue;

                    using var n = ImRaii.TreeNode($"{i}: {item.ItemId} '{details.Data.Value.Name}' ({item.Flags}): crs={details.Stats[0].Base}+{details.Stats[0].Melded}/{details.Stats[0].Max}, ctrl={details.Stats[1].Base}+{details.Stats[1].Melded}/{details.Stats[1].Max}, cp={details.Stats[2].Base}+{details.Stats[2].Melded}/{details.Stats[2].Max}");
                    if (n)
                    {
                        for (int j = 0; j < 5; ++j)
                        {
                            using var m = ImRaii.TreeNode($"魔晶石 {j}：{item.Materia[j]} {item.MateriaGrades[j]}", ImGuiTreeNodeFlags.Leaf);
                        }
                    }
                }
            }
        }

        public class Item
        {
            public uint Key { get; set; }
            public string Name { get; set; } = "";
            public ushort CraftingTime { get; set; }
            public uint UIIndex { get; set; }
        }
    }
}
