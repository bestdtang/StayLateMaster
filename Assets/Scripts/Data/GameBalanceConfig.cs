using UnityEngine;

/// <summary>
/// 全局数值调参总览；在 Project 中创建资产后挂到 RunController 即可统一调节。
/// </summary>
[CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "StayLate/Game Balance Config")]
public class GameBalanceConfig : ScriptableObject
{
    [Header("Fatigue Growth")]
    [Tooltip("开局的基础疲劳增速（点/秒）。")]
    [SerializeField] private float _fatigueBaseRate = 2f;

    [Tooltip("随时间线性增加的加速度（点/秒²）。")]
    [SerializeField] private float _fatigueAcceleration = 0.055f;

    [Tooltip("自然疲劳增速的硬上限（点/秒）；0 表示不限制。")]
    [SerializeField] private float _fatigueMaxRate = 3.4f;

    [Tooltip("疲劳到该值后开始尾段衰减（与二阶段阈值一致）。")]
    [SerializeField] private float _fatigueLateDampenStart = 50f;

    [Tooltip("尾段衰减后疲劳 100 时的增速倍率（1 表示不衰减）。")]
    [SerializeField] private float _fatigueLateDampenEndMultiplier = 0.78f;

    [Header("Pendulum (Arc Track)")]
    [Tooltip("完整摆动一周的时长（秒）；保持恒定。")]
    [SerializeField] private float _pendulumSwingPeriod = 2f;

    [Tooltip("摆锤相对中心的最大摆角（度）。")]
    [SerializeField] private float _pendulumMaxAngle = 55f;

    [Tooltip("内圈判定半宽（度）→ 成功；仅逻辑判定，扇形 UI 由 PendulumController 的 Visual 字段控制。")]
    [SerializeField] private float _judgmentInnerHalfAngle = 8f;

    [Tooltip("外圈判定半宽（度）→ 偏早/偏晚；须 ≥ 内圈；仅逻辑判定，扇形 UI 由 PendulumController 的 Visual 字段控制。")]
    [SerializeField] private float _judgmentOuterHalfAngle = 18f;

    [Tooltip("两次眨眼之间外圈半宽的缩小速率（度/秒）；缩到 0 且未按 = 完全失败。")]
    [SerializeField] private float _judgmentShrinkPerSecond = 2f;

    [Header("Judgment Zone Fatigue Phases")]
    [Tooltip("疲劳 ≥ 此值 → 二阶段（反侧随机刷新 + 判定区缩小）。")]
    [SerializeField] private float _zonePhase2AtFatigue = 50f;

    [Tooltip("疲劳 ≥ 此值 → 三阶段（不规则加速度摆动的判定区）。")]
    [SerializeField] private float _zonePhase3AtFatigue = 80f;

    [Tooltip("二/三阶段眨眼刷新后的判定区满宽倍率。")]
    [SerializeField] private float _zonePhase2SizeMultiplier = 0.75f;

    [Tooltip("二阶段反侧随机：中心相对弧顶的最小偏移（度）。")]
    [SerializeField] private float _zonePhase2MinCenterOffset = 12f;

    [Tooltip("三阶段判定区期望摆幅（度）；运行时会钳制以留在轨道内。")]
    [SerializeField] private float _zonePhase3SwingAmplitude = 55f;

    [Tooltip("三阶段判定区摆动的基础角频率（弧度/秒）。")]
    [SerializeField] private float _zonePhase3SwingOmega = 3.14f;

    [Tooltip("三阶段 Perlin 角加速度扰动的幅度。")]
    [SerializeField] private float _zonePhase3IrregularAccelAmplitude = 8f;

    [Tooltip("三阶段不规则加速度的变化速率。")]
    [SerializeField] private float _zonePhase3IrregularAccelSpeed = 1.5f;

    [Header("Pendulum Fatigue Speed")]
    [Tooltip("二阶段摆动周期倍率（<1 = 摆得更快）。")]
    [SerializeField] private float _pendulumPhase2PeriodMultiplier = 0.85f;

    [Tooltip("三阶段摆动周期倍率（<1 = 摆得更快）。")]
    [SerializeField] private float _pendulumPhase3PeriodMultiplier = 0.7f;

    [Header("Blink Modifiers")]
    [Tooltip("绿圈眨眼成功时的疲劳增速倍率。")]
    [SerializeField] private float _blinkSuccessRateMultiplier = 0.5f;

    [Tooltip("黄圈偏早/偏晚眨眼时的疲劳增速倍率。")]
    [SerializeField] private float _blinkEarlyLateRateMultiplier = 0.85f;

    [Tooltip("红圈或判定区缩尽失败时的疲劳增速倍率。")]
    [SerializeField] private float _blinkMissRateMultiplier = 2f;

    [Tooltip("眨眼增速修饰的默认持续时间（秒）。")]
    [SerializeField] private float _blinkModifierDuration = 4f;

    [Header("Crazy Fatigue")]
    [Tooltip("连续多少次完全失败进入超级疲劳。")]
    [SerializeField] private int _crazyFatigueTriggerStreak = 3;

    [Tooltip("超级疲劳额外疲劳增速倍率（与 Blink 修饰相乘）。")]
    [SerializeField] private float _crazyFatigueRateMultiplier = 1.35f;

    [Tooltip("超级疲劳期间钟摆周期倍率（<1 = 略快）。")]
    [SerializeField] private float _crazyFatiguePendulumPeriodMultiplier = 0.92f;

    [Header("Recommendation (Topic Weights)")]
    [Tooltip("每个 Topic 的最小抽卡权重；防止任意 Topic 完全消失。")]
    [SerializeField] private float _minTopicWeight = 0.25f;

    [Tooltip("兴趣 Topic 被右滑（喜欢）时的权重增量。")]
    [SerializeField] private float _correctInterestRightWeightDelta = 0.35f;

    [Tooltip("非兴趣 Topic 被左滑（跳过）时的权重增量。")]
    [SerializeField] private float _correctSkipLeftWeightDelta = -0.08f;

    [Tooltip("兴趣 Topic 被左滑（跳过）时的权重增量。")]
    [SerializeField] private float _wrongInterestLeftWeightDelta = -0.25f;

    [Tooltip("非兴趣 Topic 被右滑（喜欢）时的权重增量。")]
    [SerializeField] private float _wrongAcceptRightWeightDelta = 0.15f;

    [Tooltip("当前兴趣 Topic 的抽卡权重倍率。")]
    [SerializeField] private float _interestAffinity = 4f;

    [Tooltip("连续多少张未出当前兴趣就强制出（硬保底）。")]
    [SerializeField] private int _interestGuaranteeGap = 3;

    [Header("Recommendation (Swipe Happiness)")]
    [Tooltip("兴趣 + 右滑时获得的快乐值。")]
    [SerializeField] private float _happinessCorrectInterestRight = 8f;

    [Tooltip("错误滑卡时扣除的基础快乐值。")]
    [SerializeField] private float _happinessWrongBase = 3f;

    [Tooltip("错误滑卡时每点疲劳额外增加的扣快乐惩罚。")]
    [SerializeField] private float _happinessWrongFatigueScale = 0.02f;

    [Header("Combo Streak")]
    [Tooltip("连击第 1 次正确后的初始续连窗口（秒）。")]
    [SerializeField] private float _comboBaseWindow = 4f;

    [Tooltip("连击每 +1 时续连窗口缩短量（秒）；窗口 = max(下限, 初始 - (连击-1)×此值）。")]
    [SerializeField] private float _comboWindowShrinkPerStreak = 0.35f;

    [Tooltip("续连窗口下限（秒）；连击越高窗口越短但不会低于此值。")]
    [SerializeField] private float _comboMinWindow = 1.2f;

    [Header("Hot Time")]
    [Tooltip("触发火热时间所需的连续正确滑卡数。")]
    [SerializeField] private int _hotTriggerStreak = 5;

    [Tooltip("火热时间持续时长（秒）。")]
    [SerializeField] private float _hotTimeDuration = 3f;

    [Tooltip("火热时间内每次正确（右滑）固定增加的快乐值。")]
    [SerializeField] private float _hotHappinessPerSwipe = 6f;

    [Tooltip("火热时间结束后，滑卡输入禁用的容错缓冲（秒）。")]
    [SerializeField] private float _hotPostBufferDuration = 0.6f;

    public float FatigueBaseRate => _fatigueBaseRate;
    public float FatigueAcceleration => _fatigueAcceleration;
    public float FatigueMaxRate => _fatigueMaxRate;
    public float FatigueLateDampenStart => _fatigueLateDampenStart;
    public float FatigueLateDampenEndMultiplier => _fatigueLateDampenEndMultiplier;

    public float PendulumSwingPeriod => _pendulumSwingPeriod;
    public float PendulumMaxAngle => _pendulumMaxAngle;
    public float JudgmentInnerHalfAngle => _judgmentInnerHalfAngle;
    public float JudgmentOuterHalfAngle => _judgmentOuterHalfAngle;
    public float JudgmentShrinkPerSecond => _judgmentShrinkPerSecond;
    public float ZonePhase2AtFatigue => _zonePhase2AtFatigue;
    public float ZonePhase3AtFatigue => _zonePhase3AtFatigue;
    public float ZonePhase2SizeMultiplier => _zonePhase2SizeMultiplier;
    public float ZonePhase2MinCenterOffset => _zonePhase2MinCenterOffset;
    public float ZonePhase3SwingAmplitude => _zonePhase3SwingAmplitude;
    public float ZonePhase3SwingOmega => _zonePhase3SwingOmega;
    public float ZonePhase3IrregularAccelAmplitude => _zonePhase3IrregularAccelAmplitude;
    public float ZonePhase3IrregularAccelSpeed => _zonePhase3IrregularAccelSpeed;
    public float PendulumPhase2PeriodMultiplier => _pendulumPhase2PeriodMultiplier;
    public float PendulumPhase3PeriodMultiplier => _pendulumPhase3PeriodMultiplier;

    /// <summary>
    /// BGM 阶段 crossfade 时长：与对应判定区阶段的钟摆一周时长一致（与 PendulumController 同源配置）。
    /// </summary>
    public float GetBgmCrossfadeDuration(BgmTrack track)
    {
        switch (track)
        {
            case BgmTrack.Phase2:
                return _pendulumSwingPeriod * _pendulumPhase2PeriodMultiplier;
            case BgmTrack.Phase3:
                return _pendulumSwingPeriod * _pendulumPhase3PeriodMultiplier;
            default:
                return _pendulumSwingPeriod;
        }
    }

    public float BlinkSuccessRateMultiplier => _blinkSuccessRateMultiplier;
    public float BlinkEarlyLateRateMultiplier => _blinkEarlyLateRateMultiplier;
    public float BlinkMissRateMultiplier => _blinkMissRateMultiplier;
    public float BlinkModifierDuration => _blinkModifierDuration;

    public int CrazyFatigueTriggerStreak => _crazyFatigueTriggerStreak;
    public float CrazyFatigueRateMultiplier => _crazyFatigueRateMultiplier;
    public float CrazyFatiguePendulumPeriodMultiplier => _crazyFatiguePendulumPeriodMultiplier;

    public float MinTopicWeight => _minTopicWeight;
    public float CorrectInterestRightWeightDelta => _correctInterestRightWeightDelta;
    public float CorrectSkipLeftWeightDelta => _correctSkipLeftWeightDelta;
    public float WrongInterestLeftWeightDelta => _wrongInterestLeftWeightDelta;
    public float WrongAcceptRightWeightDelta => _wrongAcceptRightWeightDelta;
    public float InterestAffinity => _interestAffinity;
    public int InterestGuaranteeGap => _interestGuaranteeGap;

    public float HappinessCorrectInterestRight => _happinessCorrectInterestRight;
    public float HappinessWrongBase => _happinessWrongBase;
    public float HappinessWrongFatigueScale => _happinessWrongFatigueScale;

    public float ComboBaseWindow => _comboBaseWindow;
    public float ComboWindowShrinkPerStreak => _comboWindowShrinkPerStreak;
    public float ComboMinWindow => _comboMinWindow;

    public int HotTriggerStreak => _hotTriggerStreak;
    public float HotTimeDuration => _hotTimeDuration;
    public float HotHappinessPerSwipe => _hotHappinessPerSwipe;
    public float HotPostBufferDuration => _hotPostBufferDuration;
}
