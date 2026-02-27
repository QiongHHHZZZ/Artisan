using Artisan.QuestSync;
using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Artisan.CraftingLists
{
    internal static class SpecialLists
    {
        private static string listName = string.Empty;
        private static Dictionary<uint, bool> JobSelected = LuminaSheets.ClassJobSheet.Values.Where(x => x.RowId >= 8 && x.RowId <= 15).ToDictionary(x => x.RowId, x => false);
        private static Dictionary<ushort, bool> Durabilities = LuminaSheets.RecipeSheet.Values.Where(x => x.Number > 0).Select(x => (ushort)(x.RecipeLevelTable.Value.Durability * ((float)x.DurabilityFactor / 100))).Distinct().Order().ToDictionary(x => x, x => false);

        private static int minLevel = 1;
        private static int maxLevel = 100;

        private static int minCraftsmanship = LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredCraftsmanship);
        private static int minControl = LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredControl);

        private static Dictionary<int, bool> isExpert = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> hasToBeUnlocked = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> questRecipe = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isSecondary = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> alreadyCrafted = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isLevelBased = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isCollectable = new Dictionary<int, bool>() { [1] = false, [2] = false };
        private static Dictionary<int, bool> isHQAble = new Dictionary<int, bool>() { [1] = false, [2] = false };

        private static string Contains = string.Empty;

        private static Dictionary<int, bool> Yields = LuminaSheets.RecipeSheet.Values.DistinctBy(x => x.AmountResult).OrderBy(x => x.AmountResult).ToDictionary(x => (int)x.AmountResult, x => false);
        private static Dictionary<string, bool> Stars = LuminaSheets.RecipeLevelTableSheet.Values.DistinctBy(x => x.Stars).ToDictionary(x => "★".Repeat(x.Stars), x => false);
        private static Dictionary<int, bool> Stats = LuminaSheets.RecipeSheet.Values.SelectMany(x => x.ItemResult.Value.BaseParam).DistinctBy(x => x.Value.RowId).Where(x => x.RowId > 0).OrderBy(x => x.RowId).ToDictionary(x => (int)x.RowId, x => false);

        public static void Draw()
        {
            try
            {
                ImGui.TextWrapped(L10n.Tr("This section is for building lists based on certain criteria rather than individually. Give your list a name and select your criteria from below then select \"Build List\" and a new list will be created with all items that match the criteria. If you do not select any checkboxes then that category will be treated as \"Any\" or \"All\" except for which job crafts it."));

                ImGui.Separator();

                ImGui.TextWrapped(L10n.Tr("List Name"));
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X / 2);
                ImGui.InputText("###NameInput", ref listName, 300);

                var yesNoListHeight = ListBoxHeightForItems(2, columns: 2, minRows: 1, maxRows: 1);
                var minCraftsmanshipValue = LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredCraftsmanship);
                var maxCraftsmanshipValue = LuminaSheets.RecipeSheet.Values.Max(x => x.RequiredCraftsmanship);
                var minControlValue = LuminaSheets.RecipeSheet.Values.Min(x => x.RequiredControl);
                var maxControlValue = LuminaSheets.RecipeSheet.Values.Max(x => x.RequiredControl);

                var availableWidth = ImGui.GetContentRegionAvail().X;
                if (availableWidth >= 900f)
                {
                    if (ImGui.BeginTable("###SpecialListCriteria", 2, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("###SpecialListCriteriaLeft", ImGuiTableColumnFlags.WidthStretch, 0.46f);
                        ImGui.TableSetupColumn("###SpecialListCriteriaRight", ImGuiTableColumnFlags.WidthStretch, 0.54f);
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        DrawLeftCriteriaPane();

                        ImGui.TableNextColumn();
                        DrawRightCriteriaPane(yesNoListHeight, minCraftsmanshipValue, maxCraftsmanshipValue, minControlValue, maxControlValue);

                        ImGui.EndTable();
                    }
                }
                else
                {
                    DrawLeftCriteriaPane();
                    ImGui.Separator();
                    DrawRightCriteriaPane(yesNoListHeight, minCraftsmanshipValue, maxCraftsmanshipValue, minControlValue, maxControlValue);
                }

                ImGui.Spacing();
                DrawBaseStatsPane();

                ImGui.Spacing();
                if (ImGui.Button(L10n.Tr("Build List"), new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 0)))
                {
                    if (listName.IsNullOrWhitespace())
                    {
                        Notify.Error(L10n.Tr("Please give your list a name."));
                        return;
                    }

                    Notify.Info(L10n.Tr("Your list is being created. Please wait."));
                    Task.Run(() => CreateList(false)).ContinueWith(result => NotifySuccess(result));
                }
                if (ImGui.Button(L10n.Tr("Build List (with subcrafts)"), new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 0)))
                {
                    if (listName.IsNullOrWhitespace())
                    {
                        Notify.Error(L10n.Tr("Please give your list a name."));
                        return;
                    }

                    Notify.Info(L10n.Tr("Your list is being created. Please wait."));
                    Task.Run(() => CreateList(true)).ContinueWith(result => NotifySuccess(result));
                }
            }
            catch { }
        }

        private static void DrawLeftCriteriaPane()
        {
            var jobListHeight = ListBoxHeightForItems(JobSelected.Count, columns: 2, maxRows: 5);
            var durabilityListHeight = ListBoxHeightForItems(Durabilities.Count, columns: 2, maxRows: 5);
            var topRowListHeight = Math.Max(jobListHeight, durabilityListHeight);

            var yieldsListHeight = ListBoxHeightForItems(Yields.Count, columns: 2, maxRows: 6);
            var starsListHeight = ListBoxHeightForItems(Stars.Count, columns: 2, maxRows: 6);
            var bottomRowListHeight = Math.Max(yieldsListHeight, starsListHeight);

            if (ImGui.BeginTable("###LeftCriteriaGrid", 2, ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextWrapped(L10n.Tr("Select Job(s)"));
                if (ImGui.BeginListBox("###JobSelectListBox", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, topRowListHeight)))
                {
                    ImGui.Columns(2, "###JobSelectColumns", false);
                    foreach (var item in JobSelected)
                    {
                        string jobName = LuminaSheets.ClassJobSheet[item.Key].Name.ToString();
                        bool val = item.Value;
                        if (BorderedCheckbox(jobName, ref val))
                        {
                            JobSelected[item.Key] = val;
                        }
                        ImGui.NextColumn();
                    }
                    ImGui.Columns(1);
                    ImGui.EndListBox();
                }

                ImGui.TableNextColumn();
                ImGui.TextWrapped(L10n.Tr("Max Durability"));
                if (ImGui.BeginListBox("###SpecialListDurability", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, topRowListHeight)))
                {
                    ImGui.Columns(2, "###DurabilityColumns", false);
                    foreach (var dur in Durabilities)
                    {
                        var val = dur.Value;
                        if (BorderedCheckbox($"{dur.Key}", ref val))
                        {
                            Durabilities[dur.Key] = val;
                        }
                        ImGui.NextColumn();
                    }
                    ImGui.Columns(1);
                    ImGui.EndListBox();
                }

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextWrapped(L10n.Tr("Amount Result"));
                if (ImGui.BeginListBox("###Yields", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, bottomRowListHeight)))
                {
                    ImGui.Columns(2, "###YieldColumns", false);
                    foreach (var item in Yields)
                    {
                        var val = item.Value;
                        if (BorderedCheckbox($"{item.Key}", ref val))
                        {
                            Yields[item.Key] = val;
                        }
                        ImGui.NextColumn();
                    }
                    ImGui.Columns(1);
                    ImGui.EndListBox();
                }

                ImGui.TableNextColumn();
                ImGui.TextWrapped(L10n.Tr("Stars"));
                if (ImGui.BeginListBox("###Stars", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, bottomRowListHeight)))
                {
                    ImGui.Columns(2, "###StarsColumns", false);
                    foreach (var item in Stars)
                    {
                        var val = item.Value;
                        if (BorderedCheckbox($"{item.Key}", ref val))
                        {
                            Stars[item.Key] = val;
                        }
                        ImGui.NextColumn();
                    }
                    ImGui.Columns(1);
                    ImGui.EndListBox();
                }

                ImGui.EndTable();
            }

        }

        private static void DrawRightCriteriaPane(float yesNoListHeight, int minCraftsmanshipValue, int maxCraftsmanshipValue, int minControlValue, int maxControlValue)
        {
            ImGui.TextWrapped(L10n.Tr("Name Contains"));
            ImGuiComponents.HelpMarker(L10n.Tr("Supports RegEx."));
            ImGuiEx.SetNextItemFullWidth();
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5 });
            ImGui.InputText("###NameContains", ref Contains, 100);
            ImGui.PopStyleVar();

            if (ImGui.BeginTable("###RightCriteriaPairs", 2, ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawSliderFilter("Minimum Level", "###SpecialListMinLevel", ref minLevel, 1, 100);
                ImGui.TableNextColumn();
                DrawSliderFilter("Max Level", "###SpecialListMaxLevel", ref maxLevel, 1, 100);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawSliderFilter("Min. Craftsmanship", "###MinCraftsmanship", ref minCraftsmanship, minCraftsmanshipValue, maxCraftsmanshipValue);
                ImGui.TableNextColumn();
                DrawSliderFilter("Min. Control", "###MinControl", ref minControl, minControlValue, maxControlValue);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawYesNoFilter("Recipe from a Book", "###UnlockableRecipe", hasToBeUnlocked, yesNoListHeight);
                ImGui.TableNextColumn();
                DrawYesNoFilter("Quest Only Recipe", "###QuestRecipe", questRecipe, yesNoListHeight);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawYesNoFilter("Expert Recipe", "###ExpertRecipe", isExpert, yesNoListHeight);
                ImGui.TableNextColumn();
                DrawYesNoFilter("Secondary Recipe", "###SecondaryRecipes", isSecondary, yesNoListHeight);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawYesNoFilter("Collectable Recipe", "###CollectableRecipes", isCollectable, yesNoListHeight);
                ImGui.TableNextColumn();
                DrawYesNoFilter("HQable Recipe", "###HQRecipes", isHQAble, yesNoListHeight);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawYesNoFilter("Already Crafted Recipe", "###AlreadyCraftedRecipes", alreadyCrafted, yesNoListHeight);
                ImGui.TableNextColumn();
                DrawYesNoFilter("Level-based Recipes", "###IsLevelBasedRecipe", isLevelBased, yesNoListHeight);

                ImGui.EndTable();
            }
        }

        private static void DrawBaseStatsPane()
        {
            ImGui.TextWrapped(L10n.Tr("Base Stats"));
            var statsListHeight = ListBoxHeightForItems(Stats.Count, columns: 4, maxRows: 5);
            if (ImGui.BeginListBox("###Stats", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, statsListHeight)))
            {
                ImGui.Columns(4, "###StatsColumns", false);
                var paramSheet = Svc.Data.GetExcelSheet<BaseParam>();
                foreach (var stat in Stats)
                {
                    var val = stat.Value;
                    var statName = paramSheet?.First(x => x.RowId == stat.Key).Name.GetText() ?? stat.Key.ToString();
                    if (BorderedCheckbox($"###{stat.Key}", ref val))
                    {
                        Stats[stat.Key] = val;
                    }
                    ImGui.SameLine();
                    ImGui.TextWrapped(statName);
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
                ImGui.EndListBox();
            }
        }

        private static float ListBoxHeightForItems(int itemCount, int columns = 1, int minRows = 1, int maxRows = int.MaxValue)
        {
            var safeColumns = Math.Max(1, columns);
            var rows = (int)Math.Ceiling(itemCount / (float)safeColumns);
            rows = Math.Clamp(rows, minRows, maxRows);
            var style = ImGui.GetStyle();
            return ImGui.GetFrameHeightWithSpacing() * rows + (style.FramePadding.Y * 2f) + style.ItemSpacing.Y;
        }

        private static bool BorderedCheckbox(string label, ref bool value)
        {
            var borderColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
            if (borderColor.W < 0.85f) borderColor.W = 0.85f;
            ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            var changed = ImGui.Checkbox(label, ref value);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            return changed;
        }

        private static void DrawYesNoFilter(string title, string listBoxId, Dictionary<int, bool> filterState, float listHeight)
        {
            ImGui.TextWrapped(L10n.Tr(title));
            if (ImGui.BeginListBox(listBoxId, new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, listHeight)))
            {
                ImGui.Columns(2, $"{listBoxId}_Columns", false);
                bool yes = filterState[1];
                if (BorderedCheckbox(L10n.Tr("Yes"), ref yes))
                {
                    filterState[1] = yes;
                }
                ImGui.NextColumn();
                bool no = filterState[2];
                if (BorderedCheckbox(L10n.Tr("No"), ref no))
                {
                    filterState[2] = no;
                }
                ImGui.Columns(1);
                ImGui.EndListBox();
            }
        }

        private static void DrawSliderFilter(string title, string sliderId, ref int value, int min, int max)
        {
            ImGui.TextWrapped(L10n.Tr(title));
            ImGui.SetNextItemWidth(-1f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding with { Y = 5 });
            ImGui.SliderInt(sliderId, ref value, min, max);
            ImGui.PopStyleVar();
        }

        private static bool NotifySuccess(Task<bool> result)
        {
            if (result.Result)
            {
                Notify.Success(L10n.Tr("{0} has been created.", listName));
                return true;
            }
            return false;
        }

        private static bool CreateList(bool withSubcrafts)
        {
            var craftingList = new NewCraftingList();
            craftingList.Name = listName;
            var recipes = new List<Recipe>();

            foreach (var job in JobSelected)
            {
                if (job.Value)
                {
                    recipes.AddRange(LuminaSheets.RecipeSheet.Values.Where(x => x.Number > 0 && x.CraftType.RowId == job.Key - 8));

                    if (Stats.Any(x => x.Value))
                    {
                        recipes.RemoveAll(x => x.ItemResult.Value.BaseParam.All(y => y.RowId == 0));
                        foreach (var v in Stats.Where(x => x.Key > 0).OrderByDescending(x => x.Key == 70 || x.Key == 71 || x.Key == 72 || x.Key == 73).ThenBy(x => x.Key))
                        {
                            if (!v.Value)
                            {
                                recipes.RemoveAll(x => x.ItemResult.Value.BaseParam[0].RowId == v.Key);
                            }
                            else
                            {
                                recipes.AddRange(LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.BaseParam.Any(y => y.RowId == v.Key) && x.CraftType.RowId == job.Key - 8));
                            }
                        }
                    }
                }
            }

            foreach (var quest in QuestList.Quests)
            {
                recipes.RemoveAll(x => x.RowId == quest.Value.CRP);
                recipes.RemoveAll(x => x.RowId == quest.Value.BSM);
                recipes.RemoveAll(x => x.RowId == quest.Value.ARM);
                recipes.RemoveAll(x => x.RowId == quest.Value.GSM);
                recipes.RemoveAll(x => x.RowId == quest.Value.LTW);
                recipes.RemoveAll(x => x.RowId == quest.Value.WVR);
                recipes.RemoveAll(x => x.RowId == quest.Value.ALC);
                recipes.RemoveAll(x => x.RowId == quest.Value.CUL);
            }


            recipes.RemoveAll(x => x.RecipeLevelTable.Value.ClassJobLevel < minLevel);
            recipes.RemoveAll(x => x.RecipeLevelTable.Value.ClassJobLevel > maxLevel);
            recipes.RemoveAll(x => x.RequiredCraftsmanship < minCraftsmanship);
            recipes.RemoveAll(x => x.RequiredControl < minControl);


            if (Durabilities.Any(x => x.Value))
            {
                foreach (var dur in Durabilities)
                {
                    if (!dur.Value)
                    {
                        recipes.RemoveAll(x => (ushort)(x.RecipeLevelTable.Value.Durability * ((float)x.DurabilityFactor / 100)) == dur.Key);
                    }
                }
            }

            if (hasToBeUnlocked.Any(x => x.Value))
            {
                foreach (var v in hasToBeUnlocked)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.SecretRecipeBook.RowId > 0);
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.SecretRecipeBook.RowId == 0);
                        }
                    }
                }
            }

            if (alreadyCrafted.Any(x => x.Value))
            {
                foreach (var v in alreadyCrafted)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.RowId >= 30000 || P.ri.HasRecipeCrafted(x.RowId));
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.RowId >= 30000 || !P.ri.HasRecipeCrafted(x.RowId));
                        }
                    }
                }
            }

            if (isLevelBased.Any(x => x.Value))
            {
                foreach (var v in isLevelBased)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.RecipeNotebookList.RowId < 1000);
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.RecipeNotebookList.RowId >= 1000);
                        }
                    }
                }
            }

            if (isExpert.Any(x => x.Value))
            {
                foreach (var v in isExpert)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.IsExpert);
                        }
                        else
                        {
                            recipes.RemoveAll(x => !x.IsExpert);
                        }
                    }
                }
            }

            if (questRecipe.Any(x => x.Value))
            {
                foreach (var v in questRecipe)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.Quest.RowId > 0);
                        }
                        else
                        {
                            recipes.RemoveAll(x => x.Quest.RowId == 0);
                        }
                    }
                }
            }

            if (isSecondary.Any(x => x.Value))
            {
                foreach (var v in isSecondary)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.IsSecondary);
                        }
                        else
                        {
                            recipes.RemoveAll(x => !x.IsSecondary);
                        }
                    }
                }
            }

            if (isCollectable.Any(x => x.Value))
            {
                foreach (var v in isCollectable)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.ItemResult.Value.AlwaysCollectable);
                        }
                        if (v.Key == 2)
                        {
                            recipes.RemoveAll(x => !x.ItemResult.Value.AlwaysCollectable);
                        }
                    }
                }
            }

            if (isHQAble.Any(x => x.Value))
            {
                foreach (var v in isHQAble)
                {
                    if (!v.Value)
                    {
                        if (v.Key == 1)
                        {
                            recipes.RemoveAll(x => x.CanHq);
                        }
                        if (v.Key == 2)
                        {
                            recipes.RemoveAll(x => !x.CanHq);
                        }
                    }
                }
            }

            if (Yields.Any(x => x.Value))
            {
                foreach (var v in Yields)
                {
                    if (!v.Value)
                    {
                        recipes.RemoveAll(x => x.AmountResult == v.Key);
                    }
                }
            }

            if (Stars.Any(x => x.Value))
            {
                foreach (var v in Stars)
                {
                    if (!v.Value)
                    {
                        recipes.RemoveAll(x => x.RecipeLevelTable.Value.Stars == v.Key.Length);
                    }
                }
            }

            if (!string.IsNullOrEmpty(Contains))
            {
                Regex regex = new Regex(Contains);
                recipes.RemoveAll(x => !regex.IsMatch(x.ItemResult.Value.Name.ToDalamudString().ToString()));
            }

            if (recipes.Count == 0)
            {
                Notify.Error(L10n.Tr("Your list has no items"));
                return false;
            }

            if (!withSubcrafts)
            {
                foreach (var recipe in recipes.Distinct())
                {
                    craftingList.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1, ListItemOptions = new() });
                }
                CraftingListHelpers.TidyUpList(craftingList);
                craftingList.SetID();
                craftingList.Save(true);
            }
            else
            {
                foreach (var recipe in recipes.Distinct())
                {
                    Svc.Log.Debug($"{recipe.RowId.NameOfRecipe()}");
                    CraftingListUI.AddAllSubcrafts(recipe, craftingList, 1);
                    if (craftingList.Recipes.Any(x => x.ID == recipe.RowId))
                    {
                        craftingList.Recipes.First(x => x.ID == recipe.RowId).Quantity++;
                    }
                    else
                    {
                        craftingList.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1, ListItemOptions = new() });
                    }
                }
                CraftingListHelpers.TidyUpList(craftingList);
                craftingList.SetID();
                craftingList.Save(true);
            }

            return true;
        }
    }
}
