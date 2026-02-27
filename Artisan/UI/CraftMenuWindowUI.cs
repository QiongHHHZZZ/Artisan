using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.UI
{
    internal class CraftMenuWindowUI : Window
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        public bool EnableMacroOptions { get; set; }

        public CraftMenuWindowUI(string windowName, ImGuiWindowFlags flags) : base(GetWindowTitle(windowName), flags)
        {
            IsOpen = false;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            DisableWindowSounds = true;
            PositionCondition = ImGuiCond.Appearing;

            TitleBarButtons.Add(new()
            {
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGui.SetTooltip(T("Open Config")),
                Click = (x) => P.PluginUi.IsOpen = true,
            });
        }

        private static string GetWindowTitle(string windowName)
        {
            return windowName switch
            {
                "CraftMenuWindow" => $"{T("Craft Menu")}###CraftMenuWindow",
                "CosmicCraftMenuWindow" => $"{T("Cosmic Craft Menu")}###CosmicCraftMenuWindow",
                _ => windowName,
            };
        }

        public override bool DrawConditions()
        {
            return IsOpen;
        }

        public override void PreDraw()
        {
            if (P.Config.DisableTheme)
            {
                return;
            }

            P.Style.Push();
            P.StylePushed = true;
        }

        public override void PostDraw()
        {
            if (!P.StylePushed)
            {
                return;
            }

            P.Style.Pop();
            P.StylePushed = false;
        }

        public override void Draw()
        {
            try
            {
                if (!IsOpen)
                {
                    return;
                }
                var config = P.Config.RecipeConfigs.GetValueOrDefault(Endurance.RecipeID) ?? new();
                var autoMode = P.Config.AutoMode;

                if (ImGui.Checkbox(T("Automatic Action Execution Mode"), ref autoMode))
                {
                    P.Config.AutoMode = autoMode;
                    P.Config.Save();
                }

                var enable = Endurance.Enable;
                var recipe = LuminaSheets.RecipeSheet!.First(x => x.Key == Endurance.RecipeID).Value;

                if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID) && !Endurance.Enable)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Checkbox(T("Endurance Mode Toggle"), ref enable))
                {
                    Endurance.ToggleEndurance(enable);
                }

                if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID) && !Endurance.Enable)
                {
                    ImGui.EndDisabled();
                    ImGuiEx.Text(ImGuiColors.DalamudYellow, T("Missing Ingredients:\r\n- {0}", string.Join("\r\n- ", PreCrafting.MissingIngredients(recipe))));
                }

                if (Crafting.MaterialMiracleCharges() > 0 && (config.SolverIsStandard || config.SolverIsExpert))
                {
                    bool useMatMiracle = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert ? P.Config.ExpertSolverConfig.UseMaterialMiracle : P.Config.UseMaterialMiracle;
                    int delayMatMiracle = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert ? P.Config.ExpertSolverConfig.MinimumStepsBeforeMiracle : P.Config.MinimumStepsBeforeMiracle;
                    bool multiMatMiracle = P.Config.MaterialMiracleMulti;
                    
                    if (ImGui.Checkbox(T("Use Material Miracle"), ref useMatMiracle))
                    {
                        if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                            P.Config.ExpertSolverConfig.UseMaterialMiracle = useMatMiracle;
                        else
                            P.Config.UseMaterialMiracle = useMatMiracle;
                    }
                    if (useMatMiracle)
                    {
                        ImGui.Text(T("Minimum steps to execute before trying Material Miracle"));
                        if (ImGui.SliderInt("###MaterialMiracleSlider", ref delayMatMiracle, 0, 20))
                        {
                            if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                                P.Config.ExpertSolverConfig.MinimumStepsBeforeMiracle = delayMatMiracle;
                            else
                                P.Config.MinimumStepsBeforeMiracle = delayMatMiracle;
                        }

                        if (false == LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                        {
                            if (ImGui.Checkbox(T("Use multiple material miracles"), ref multiMatMiracle))
                                P.Config.MaterialMiracleMulti = multiMatMiracle;
                        }
                    }
                }

                // todo: this should also reference the raphael steady setting for non-experts
                if (Crafting.SteadyHandCharges() > 0 && LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                {
                    int maxSteadyUses = P.Config.ExpertSolverConfig.MaxSteadyUses;
                    ImGui.Text($"Max {Skills.SteadyHand.NameOfAction()} uses");
                    if (ImGui.SliderInt($"###MaxStellarHand", ref maxSteadyUses, 0, 2))
                    {
                        P.Config.ExpertSolverConfig.MaxSteadyUses = maxSteadyUses;
                    }
                }

                if (EnableMacroOptions)
                {
                    ImGui.Spacing();

                    if (SimpleTweaks.IsFocusTweakEnabled())
                    {
                        ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, T("Warning: You have the \"Auto Focus Recipe Search\" SimpleTweak enabled. This is highly incompatible with Artisan and is recommended to disable it."));
                    }

                    if (Endurance.RecipeID == 0)
                    {
                        return;
                    }

                    if (!config.Draw(Endurance.RecipeID))
                    {
                        return;
                    }

                    P.Config.RecipeConfigs[Endurance.RecipeID] = config;
                    P.Config.Save();
                }
            }
            catch { }
        }
    }
}
