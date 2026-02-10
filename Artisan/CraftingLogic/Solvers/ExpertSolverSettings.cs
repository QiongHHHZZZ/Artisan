using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using System;
using System.Numerics;
using static Artisan.RawInformation.AddonExtensions;

namespace Artisan.CraftingLogic.Solvers;

public class ExpertSolverSettings
{
    public bool MaxIshgardRecipes;
    public bool UseReflectOpener;
    public bool MuMeIntensiveGood = true; // if true, we allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs
    public bool MuMeIntensiveMalleable = false; // if true and we have malleable during mume, use intensive rather than hoping for rapid
    public bool MuMeIntensiveLastResort = true; // if true and we're on last step of mume, use intensive (forcing via H&S if needed) rather than hoping for rapid (unless we have centered)
    public bool MuMePrimedManip = false; // if true, allow using primed manipulation after veneration is up on mume
    public bool MuMeAllowObserve = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability
    public int MuMeMinStepsForManip = 2; // if this or less rounds are remaining on mume, don't use manipulation under favourable conditions
    public int MuMeMinStepsForVene = 1; // if this or less rounds are remaining on mume, don't use veneration
    public int MidMinIQForHSPrecise = 10; // min iq stacks where we use h&s+precise; 10 to disable
    public bool MidBaitPliantWithObservePreQuality = true; // if true, when very low on durability and without manip active during pre-quality phase, we use observe rather than normal manip
    public bool MidBaitPliantWithObserveAfterIQ = true; // if true, when very low on durability and without manip active after iq has 10 stacks, we use observe rather than normal manip or inno+finnesse
    public bool MidPrimedManipPreQuality = true; // if true, allow using primed manipulation during pre-quality phase
    public bool MidPrimedManipAfterIQ = true; // if true, allow using primed manipulation during after iq has 10 stacks
    public enum MidUseTPSetting  // where to use trained perfection
    {
        MidUseTPGroundwork,        // use TP on groundwork for high-prio progress
        MidUseTPPrepIQ,            // use TP on preparatory touch to build IQ stacks
        MidUseTPEitherPreQuality,  // use TP on either of the options above, depending on which status comes up first (default groundwork)
        MidUseTPPrepQuality        // use TP on prep touch after 10 IQ, with GS+Inno
    }
    public string GetMidUseTPSettingName(MidUseTPSetting value)
        => value switch
        {
            MidUseTPSetting.MidUseTPGroundwork => $"（前期）{Skills.Groundwork.NameOfAction()}",
            MidUseTPSetting.MidUseTPPrepIQ => $"（前期）{Skills.PreparatoryTouch.NameOfAction()}（叠 {Buffs.InnerQuiet.NameOfBuff()}）",
            MidUseTPSetting.MidUseTPEitherPreQuality => $"（前期）根据{ConditionString.ToLower()}选择任一动作",
            MidUseTPSetting.MidUseTPPrepQuality or _ => $"（后期）{Skills.PreparatoryTouch.NameOfAction()}（{Buffs.InnerQuiet.NameOfBuff()}满层时，侧重{QualityString.ToLower()}）",
        };
    public MidUseTPSetting MidUseTP = MidUseTPSetting.MidUseTPGroundwork;
    public int MidMaxBaitStepsForTP = 0; // how many observes should be used to bait favorable conditions for trained perfection; 0 to disable
    public enum MidKeepHighDuraSetting  // what to do in pre-quality when dura is starting to run low
    {
        MidKeepHighDuraUnbuffed,        // fish for procs with observe to conserve dura, as long as veneration isn't up
        MidKeepHighDuraVeneration,      // fish for procs with observe to conserve dura, no matter what
        MidUseDura                      // don't fish for procs, keep using durability
    }
    public string GetMidKeepHighDuraSettingName(MidKeepHighDuraSetting value)
        => value switch
        {
            MidKeepHighDuraSetting.MidKeepHighDuraUnbuffed => $"未开启{Buffs.Veneration.NameOfBuff()}时，使用{Skills.Observe.NameOfAction()}来等待更好的{ConditionString.ToLower()}",
            MidKeepHighDuraSetting.MidKeepHighDuraVeneration => $"即使在{Buffs.Veneration.NameOfBuff()}期间，也使用{Skills.Observe.NameOfAction()}来等待更好的{ConditionString.ToLower()}",
            MidKeepHighDuraSetting.MidUseDura or _ => $"不使用{Skills.Observe.NameOfAction()}，直接继续推进",
        };
    public MidKeepHighDuraSetting MidKeepHighDura = MidKeepHighDuraSetting.MidKeepHighDuraUnbuffed;
    public enum MidAllowIntensiveSetting  // how to handle good procs before finishable progress
    {
        MidAllowIntensiveUnbuffed,        // use intensive synthesis no matter what
        MidAllowIntensiveVeneration,      // use intensive synthesis as long as veneration is up
        MidNoIntensive                    // don't use intensive synthesis (good will be used for tricks or precise)
    }
    public string GetMidAllowIntensiveSettingName(MidAllowIntensiveSetting value)
        => value switch
        {
            MidAllowIntensiveSetting.MidNoIntensive => $"不使用{Skills.IntensiveSynthesis.NameOfAction()}",
            MidAllowIntensiveSetting.MidAllowIntensiveVeneration => $"仅在{Buffs.Veneration.NameOfBuff()}生效时使用{Skills.IntensiveSynthesis.NameOfAction()}",
            MidAllowIntensiveSetting.MidAllowIntensiveUnbuffed or _ => $"无视增益状态，始终使用{Skills.IntensiveSynthesis.NameOfAction()}"
        };
    public MidAllowIntensiveSetting MidAllowIntensive = MidAllowIntensiveSetting.MidNoIntensive;
    public bool MidAllowVenerationGoodOmen = true; // if true, we allow using veneration during iq phase if we lack a lot of progress on good omen
    public bool MidAllowVenerationAfterIQ = true; // if true, we allow using veneration after iq is fully stacked if we still lack a lot of progress
    public bool MidAllowPrecise = true; // if true, we allow spending good condition on precise touch if we still need iq
    public bool MidAllowSturdyPreсise = false; // if true,we consider sturdy+h&s+precise touch a good move for building iq
    public bool MidAllowCenteredHasty = true; // if true, we consider centered hasty touch a good move for building iq (85% reliability)
    public bool MidAllowSturdyHasty = true; // if true, we consider sturdy hasty touch a good move for building iq (50% reliability), otherwise we use combo
    public bool MidAllowGoodPrep = true; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool MidAllowSturdyPrep = true; // if true, we consider prep touch a good move for finisher under sturdy+inno
    public bool MidGSBeforeInno = true; // if true, we start quality combos with gs+inno rather than just inno
    public bool MidFinishProgressBeforeQuality = false; // if true, at 10 iq we first finish progress before starting on quality
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp
	public bool RapidSynthYoloAllowed = true; // if false, expert crafting may lock up midway, so not good for AFK crafting. This yolo however is likely to fail the craft, so disabling gives opportunity for intervention
    public bool UseMaterialMiracle = false;
	public int MinimumStepsBeforeMiracle = 10;

    [NonSerialized]
    public IDalamudTextureWrap? expertIcon;

    public ExpertSolverSettings()
    {
        var tex = Svc.PluginInterface.UiBuilder.LoadUld("ui/uld/RecipeNoteBook.uld");
        expertIcon = tex?.LoadTexturePart("ui/uld/RecipeNoteBook_hr1.tex", 14);
    }

    public bool Draw()
    {
            bool changed = false;
        try
        {
            ImGui.TextWrapped($"专家配方求解器并不是标准求解器的替代方案，仅用于专家配方。");
            if (expertIcon != null)
            {
                ImGui.TextWrapped($"该求解器仅适用于制作日志中带有");
                ImGui.SameLine();
                ImGui.Image(expertIcon.Handle, expertIcon.Size, new Vector2(0, 0), new Vector2(1, 1), new Vector4(0.94f, 0.57f, 0f, 1f));
                ImGui.SameLine();
                ImGui.TextWrapped($"图标的配方。");
            }

            ImGui.Indent();
            ImGui.Dummy(new Vector2(0, 5f));
            if (ImGui.CollapsingHeader("起手阶段"))
            {
                changed |= ImGui.Checkbox($"使用 {Skills.Reflect.NameOfAction()} 代替 {Skills.MuscleMemory.NameOfAction()}", ref UseReflectOpener);
                if (!UseReflectOpener)
                {
                    ImGui.Dummy(new Vector2(0, 5f));
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"以下设置仅在制作开局且 {Skills.MuscleMemory.NameOfAction()} 生效时适用。");
                    ImGui.Dummy(new Vector2(0, 5f));
                    changed |= ImGui.Checkbox($"当 ● {Condition.Good.ToLocalizedString()} 时，优先 {Skills.IntensiveSynthesis.NameOfAction()} (400%) 而非 {Skills.RapidSynthesis.NameOfAction()} (500%)", ref MuMeIntensiveGood);
                    changed |= ImGui.Checkbox($"当 ● {Condition.Malleable.ToLocalizedString()} 时，使用 {Skills.HeartAndSoul.NameOfAction()} + {Skills.IntensiveSynthesis.NameOfAction()}（若可用）", ref MuMeIntensiveMalleable);
                    changed |= ImGui.Checkbox($"当 ● {Condition.Primed.ToLocalizedString()} 且 {Skills.Veneration.NameOfAction()} 已生效时，使用 {Skills.Manipulation.NameOfAction()}", ref MuMePrimedManip);
                    ImGuiComponents.HelpMarker($"若禁用此项，{Skills.Manipulation.NameOfAction()} 仅会在 ● {Condition.Pliant.ToLocalizedString()} 且 {Skills.MuscleMemory.NameOfAction()} 生效时使用。");
                    changed |= ImGui.Checkbox($"当 ● {Condition.Normal.ToLocalizedString()} 或其他无关{ConditionString.ToLower()}时，用 {Skills.Observe.NameOfAction()} 代替 {Skills.RapidSynthesis.NameOfAction()}", ref MuMeAllowObserve);
                    ImGuiComponents.HelpMarker($"这会以减少 {Skills.MuscleMemory.NameOfAction()} 回合为代价来节省 {DurabilityString.ToLower()}。");
                    changed |= ImGui.Checkbox($"当 {Skills.MuscleMemory.NameOfAction()} 仅剩1步且不是 ● {Condition.Centered.ToLocalizedString()} 时，使用 {Skills.IntensiveSynthesis.NameOfAction()}（必要时通过 {Skills.HeartAndSoul.NameOfAction()} 强制）", ref MuMeIntensiveLastResort);
                    ImGuiComponents.HelpMarker($"若最后一步是 ● {Condition.Centered.ToLocalizedString()}，仍会使用 {Skills.RapidSynthesis.NameOfAction()}。");
                    ImGui.Text($"仅当 {Skills.MuscleMemory.NameOfAction()} 至少剩余以下步数时才使用这些技能：");
                    ImGuiComponents.HelpMarker($"求解器仍只会在合适的 {ConditionString.ToLower()} 下使用这些技能。");
                    // these have a minimum of 1 to avoid using a buff on the final turn of MuMe
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderInt($"{Skills.Manipulation.NameOfAction()}###MumeMinStepsForManip", ref MuMeMinStepsForManip, 1, 5);
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderInt($"{Skills.Veneration.NameOfAction()}###MuMeMinStepsForVene", ref MuMeMinStepsForVene, 1, 5);
                    ImGui.Dummy(new Vector2(0, 5f));
                }
            }

            if (ImGui.CollapsingHeader($"主循环 - {QualityString}前阶段"))
            {
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"以下设置在起手后生效，直到 {Buffs.InnerQuiet.NameOfBuff()} 叠满前。");

                // Pre-quality dura/CP settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"通用");
                ImGui.Indent();
                ImGui.TextWrapped($"{Skills.TrainedPerfection.NameOfAction()} 的使用时机：");
                ImGuiComponents.HelpMarker($"“（后期）”选项会尝试在 {Buffs.Innovation.NameOfBuff()} 与 {Buffs.GreatStrides.NameOfBuff()} 下使用 {Skills.PreparatoryTouch.NameOfAction()}。\n“任一动作”与下方 {Skills.Observe.NameOfAction()} 设定搭配效果最佳，且在中性 {ConditionString.ToLower()} 时默认使用 {Skills.Groundwork.NameOfAction()}。");
                ImGui.PushItemWidth(400);
                if (ImGui.BeginCombo("##midUseTPSetting", GetMidUseTPSettingName(MidUseTP)))
                {
                    foreach (MidUseTPSetting x in Enum.GetValues<MidUseTPSetting>())
                    {
                        if (ImGui.Selectable(GetMidUseTPSettingName(x)))
                        {
                            MidUseTP = x;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.PushItemWidth(150);
                changed |= ImGui.SliderInt($"在 {Skills.TrainedPerfection.NameOfAction()} 期间，为等待{ConditionString.ToLower()}最多使用 {Skills.Observe.NameOfAction()} 的次数（0=禁用）###MidMaxBaitStepsForTP", ref MidMaxBaitStepsForTP, 0, 5);
                ImGuiComponents.HelpMarker($"当 {Skills.TrainedPerfection.NameOfAction()} 用于 {Skills.Groundwork.NameOfAction()} 时会钓 ● {Condition.Malleable.ToLocalizedString()}；\n用于 {Skills.PreparatoryTouch.NameOfAction()} 时会钓 ● {Condition.Good.ToLocalizedString()}/{Condition.Pliant.ToLocalizedString()}。");
                changed |= ImGui.Checkbox($"当{DurabilityString.ToLower()}过低时，使用 {Skills.Observe.NameOfAction()} 争取有利{ConditionString.ToLower()}后再用 {Skills.Manipulation.NameOfAction()}", ref MidBaitPliantWithObservePreQuality);
                ImGuiComponents.HelpMarker($"会尝试钓 ● {Condition.Pliant.ToLocalizedString()}（若启用对应选项也会考虑 ● {Condition.Primed.ToLocalizedString()}）。\n禁用后，无论 {ConditionString.ToLower()} 如何都会立刻使用 {Skills.Manipulation.NameOfAction()}。");
                changed |= ImGui.Checkbox($"在 ● {Condition.Primed.ToLocalizedString()} 期间使用 {Skills.Manipulation.NameOfAction()}", ref MidPrimedManipPreQuality);
                ImGuiComponents.HelpMarker($"若禁用，此阶段的 ● {Condition.Primed.ToLocalizedString()} 通常会按 ● {Condition.Normal.ToLocalizedString()} 处理。");
                ImGui.Unindent();

                // Pre-quality progress settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{ProgressString}");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"优先推进{ProgressString.ToLower()}，高于 {Buffs.InnerQuiet.NameOfBuff()} 与 {QualityString.ToLower()}", ref MidFinishProgressBeforeQuality);
                ImGuiComponents.HelpMarker($"启用后会尽快使用 {Buffs.Veneration.NameOfBuff()} 与 {Skills.RapidSynthesis.NameOfAction()} 冲满进度，\n不再优先考虑 {Buffs.InnerQuiet.NameOfBuff()} 层数或当前 {ConditionString.ToLower()}（更稳完工但灵活性较低）。\n禁用后会先叠满 {Buffs.InnerQuiet.NameOfBuff()} 再强推{ProgressString.ToLower()}（更灵活，但极端情况可能来不及完工）。");
                ImGui.TextWrapped($"当{DurabilityString.ToLower()}开始偏低且需要用 {Skills.RapidSynthesis.NameOfAction()} 时：");
                ImGui.PushItemWidth(400);
                if (ImGui.BeginCombo("##midKeepHighDuraSetting", GetMidKeepHighDuraSettingName(MidKeepHighDura)))
                {
                    foreach (MidKeepHighDuraSetting x in Enum.GetValues<MidKeepHighDuraSetting>())
                    {
                        if (ImGui.Selectable(GetMidKeepHighDuraSettingName(x)))
                        {
                            MidKeepHighDura = x;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.TextWrapped($"当 ● {Condition.Good.ToLocalizedString()} 且仍需推进{ProgressString.ToLower()}时：");
                ImGuiComponents.HelpMarker($"若禁用，即使还有{ProgressString.ToLower()}，● {Condition.Good.ToLocalizedString()} 也会用于 {Skills.PreciseTouch.NameOfAction()} 或 {Skills.TricksOfTrade.NameOfAction()}（取决于其它设置）。");
                if (ImGui.BeginCombo("##midAllowIntensiveSetting", GetMidAllowIntensiveSettingName(MidAllowIntensive)))
                {
                    foreach (MidAllowIntensiveSetting x in Enum.GetValues<MidAllowIntensiveSetting>())
                    {
                        if (ImGui.Selectable(GetMidAllowIntensiveSettingName(x)))
                        {
                            MidAllowIntensive = x;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                changed |= ImGui.Checkbox($"当 ● {Condition.GoodOmen.ToLocalizedString()} 且{ProgressString.ToLower()}缺口较大时使用 {Skills.Veneration.NameOfAction()}", ref MidAllowVenerationGoodOmen);
                ImGuiComponents.HelpMarker($"即：若接下来 ● {Condition.Good.ToLocalizedString()} 回合的 {Skills.IntensiveSynthesis.NameOfAction()} 在不加 {Skills.Veneration.NameOfAction()} 的情况下无法补满{ProgressString.ToLower()}。");
                ImGui.Unindent();

                // Pre-quality Inner Quiet settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{Buffs.InnerQuiet.NameOfBuff()}");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"当 ● {Condition.Good.ToLocalizedString()} 时使用 {Skills.PreciseTouch.NameOfAction()}", ref MidAllowPrecise);
                ImGuiComponents.HelpMarker($"若还有{ProgressString.ToLower()}，默认优先 {Skills.IntensiveSynthesis.NameOfAction()}（除非其它设置禁用）。\n若两者都禁用，● {Condition.Good.ToLocalizedString()} 将用于 {Skills.TricksOfTrade.NameOfAction()}。");
                ImGui.TextWrapped($"使用 {Skills.HeartAndSoul.NameOfAction()} 强制触发 {Skills.PreciseTouch.NameOfAction()}：");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"当 ● {Condition.Sturdy.ToLocalizedString()}/{Condition.Robust.ToLocalizedString()} 时", ref MidAllowSturdyPreсise);
                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderInt($"在此 {Buffs.InnerQuiet.NameOfBuff()} 层数时触发（10=禁用）###MidMinIQForHSPrecise", ref MidMinIQForHSPrecise, 0, 10);
                ImGui.Unindent();
                ImGui.TextWrapped($"使用 {Skills.HastyTouch.NameOfAction()} 与 {Skills.DaringTouch.NameOfAction()}：");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"当 ● {Condition.Centered.ToLocalizedString()}（85% 成功率，消耗 10 {DurabilityString.ToLower()}）", ref MidAllowCenteredHasty);
                changed |= ImGui.Checkbox($"当 ● {Condition.Sturdy.ToLocalizedString()}/{Condition.Robust.ToLocalizedString()}（60% 成功率，消耗 5 {DurabilityString.ToLower()}）", ref MidAllowSturdyHasty);
                ImGui.Unindent();
                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 5f));
            }

            if (ImGui.CollapsingHeader($"主循环 - {QualityString}阶段"))
            {
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"以下设置在 {Buffs.InnerQuiet.NameOfBuff()} 叠满后生效。");

                // Mid-quality dura/CP settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"通用");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"当{DurabilityString.ToLower()}极低时，使用 {Skills.Observe.NameOfAction()} 以争取有利{ConditionString.ToLower()}来恢复{DurabilityString.ToLower()}", ref MidBaitPliantWithObserveAfterIQ);
                ImGuiComponents.HelpMarker($"会尝试钓 ● {Condition.Pliant.ToLocalizedString()}（也可能命中 ● {Condition.Primed.ToLocalizedString()}）。\n禁用后，恢复耐久或不消耗{DurabilityString.ToLower()}的动作会立刻使用，不再等待{ConditionString.ToLower()}。");
                changed |= ImGui.Checkbox($"当 ● {Condition.Primed.ToLocalizedString()} 且CP充足时使用 {Skills.Manipulation.NameOfAction()}（以充分利用恢复的{DurabilityString.ToLower()}）", ref MidPrimedManipAfterIQ);
                changed |= ImGui.Checkbox($"在 ● {Condition.GoodOmen.ToLocalizedString()} 且无增益时，优先 {Skills.Observe.NameOfAction()} → {Skills.TricksOfTrade.NameOfAction()}", ref MidObserveGoodOmenForTricks);
                ImGuiComponents.HelpMarker($"若禁用，会优先补增益，并把 ● {Condition.Good.ToLocalizedString()} 用于{ProgressString.ToLower()}或{QualityString.ToLower()}。\n通常启用更高效。");
                ImGui.Unindent();

                // Mid-quality progress settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{ProgressString}");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"当{ProgressString.ToLower()}缺口较大时使用 {Skills.Veneration.NameOfAction()}", ref MidAllowVenerationAfterIQ);
                ImGuiComponents.HelpMarker($"即便已到后期，若不加 {Skills.Veneration.NameOfAction()} 时单次 {Skills.IntensiveSynthesis.NameOfAction()} 无法完工，也会触发。\n若启用“优先推进{ProgressString.ToLower()}”则该逻辑会被覆盖。");
                ImGui.Unindent();

                // Mid-quality action settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{QualityString}");
                ImGui.Indent();
                ImGui.TextWrapped($"{Skills.PreparatoryTouch.NameOfAction()} 的使用条件：");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"在 ● {Condition.Good.ToLocalizedString()} + {Buffs.Innovation.NameOfBuff()} + {Buffs.GreatStrides.NameOfBuff()} 下", ref MidAllowGoodPrep);
                changed |= ImGui.Checkbox($"在 ● {Condition.Sturdy.ToLocalizedString()}/{Condition.Robust.ToLocalizedString()} + {Buffs.Innovation.NameOfBuff()} 下", ref MidAllowSturdyPrep);
                ImGui.Unindent();
                changed |= ImGui.Checkbox($"非收尾的{QualityString.ToLower()}连段前先使用 {Skills.GreatStrides.NameOfAction()}", ref MidGSBeforeInno);
                ImGuiComponents.HelpMarker($"例如：{Buffs.Innovation.NameOfBuff()} → {Skills.Observe.NameOfAction()} → {Skills.AdvancedTouch.NameOfAction()}。\n启用后会多耗CP但少耗{DurabilityString.ToLower()}，并可能减少昂贵耐久动作的使用。");
                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 5f));
            }

            if (ImGui.CollapsingHeader($"收尾阶段"))
            {
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"以下设置在接近{QualityString.ToLower()}上限或几乎无路可走时生效。");

                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"使用 {Skills.CarefulObservation.NameOfAction()} 尝试触发 ● {Condition.Good.ToLocalizedString()}：");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"用于 {Skills.ByregotsBlessing.NameOfAction()}（临时替代 {Skills.GreatStrides.NameOfAction()}）", ref FinisherBaitGoodByregot);
                ImGuiComponents.HelpMarker($"当 {Skills.GreatStrides.NameOfAction()} + {Skills.ByregotsBlessing.NameOfAction()} 足以收尾，\n但 CP 不足以使用 {Skills.GreatStrides.NameOfAction()} 或常规 {Skills.Observe.NameOfAction()} 时触发。");
                changed |= ImGui.Checkbox($"当CP极低时用于 {Skills.TricksOfTrade.NameOfAction()}", ref EmergencyCPBaitGood);
                ImGuiComponents.HelpMarker($"当几乎没有其它可用手段，且连 {Skills.ByregotsBlessing.NameOfAction()} 都不足以补齐{QualityString.ToLower()}时触发。");
                ImGui.Unindent();
                changed |= ImGui.Checkbox($"在无其它选择时允许用 {Skills.RapidSynthesis.NameOfAction()} 收尾", ref RapidSynthYoloAllowed);
                ImGuiComponents.HelpMarker($"若禁用，求解器会停手不动，可能中断AFK专家制作。\n通常可安全启用，因为仅在CP或{DurabilityString.ToLower()}都见底时才会触发。");
                ImGui.Dummy(new Vector2(0, 5f));
            }
            ImGui.Unindent();

            // Misc. settings
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= ImGui.Checkbox("天穹街复兴配方尽量做满品质（而不是只到达最高档位）", ref MaxIshgardRecipes);
            ImGuiComponents.HelpMarker("会尽可能拉满品质，以获得更多天穹点数。");
            changed |= ImGui.Checkbox($"在宇宙探索中使用 {Skills.MaterialMiracle.NameOfAction()}", ref UseMaterialMiracle);
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt($"尝试 {Skills.MaterialMiracle.NameOfAction()} 前最少执行步数###MinimumStepsBeforeMiracle", ref MinimumStepsBeforeMiracle, 0, 20);
            if (ImGuiEx.ButtonCtrl("重置专家求解器设置为默认"))
            {
                P.Config.ExpertSolverConfig = new();
                changed |= true;
            }
            return changed;
        }
        catch { }
        return changed;
    }
}
