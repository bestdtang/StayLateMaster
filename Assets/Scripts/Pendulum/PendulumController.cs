using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拱桥形上弧钟摆：指针与扇形均在圆心，靠旋转/Filled 表现；判定仅比较角度。
/// 绿圈=内圈成功，黄圈=外圈偏早/偏晚，红圈=圈外失败。
/// 判定区在两次眨眼之间持续缩小；任意按键眨眼后刷新；缩至 0 仍未按 = 完全失败。
/// 疲劳三阶段：0–49 居中 / 50–79 反侧随机刷新+缩小 / 80–99 不规则加速度摆动。
/// </summary>
public class PendulumController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Pendulum bob RectTransform; rotates with swing angle.")]
    [SerializeField] RectTransform pendulumBob;
    [Tooltip("Root of the judgment zone fan graphics.")]
    [SerializeField] RectTransform judgmentZoneRoot;
    [Tooltip("Arc mask behind the judgment wedges.")]
    [SerializeField] Image judgmentZoneArcMask;
    [Tooltip("Outer (yellow) judgment wedge, clockwise half.")]
    [SerializeField] Image judgmentZoneOuter;
    [Tooltip("Outer (yellow) judgment wedge, counter-clockwise half.")]
    [SerializeField] Image judgmentZoneOuterMirror;
    [Tooltip("Inner (green) success wedge, clockwise half.")]
    [SerializeField] Image judgmentZoneInner;
    [Tooltip("Inner (green) success wedge, counter-clockwise half.")]
    [SerializeField] Image judgmentZoneInnerMirror;

    [Header("Config & Stats")]
    [Tooltip("Swing period, shrink rate, and gameplay judgment angles.")]
    [SerializeField] GameBalanceConfig balanceConfig;
    [Tooltip("Drives judgment-zone phase by current fatigue value.")]
    [SerializeField] FatigueMeter fatigueMeter;

    [Header("Arc Center UI")]
    [Tooltip("Art-facing offset (degrees) added to bob rotation.")]
    [SerializeField] float bobRotationOffset;

    [Header("Zone Visuals (Inspector / Scene)")]
    [Tooltip("内圈扇形显示半宽（度）；仅 UI，眨眼判定仍读 GameBalanceConfig。")]
    [SerializeField] float visualInnerHalfAngle = 13f;
    [Tooltip("外圈扇形显示半宽（度）；仅 UI，眨眼判定仍读 GameBalanceConfig。")]
    [SerializeField] float visualOuterHalfAngle = 22f;

    float bobPhaseRadians;
    float visualInnerHalfAngleBase;
    float visualOuterHalfAngleBase;
    float currentBobAngleDeg;
    float zoneCenterAngleDeg;
    float baseInnerHalfAngle;
    float baseOuterHalfAngle;
    float currentInnerHalfAngle;
    float currentOuterHalfAngle;

    float lockedZoneCenterDeg;
    float zoneSwingPhase;
    float zoneSwingVelocity;
    float phase3NoiseSeed;
    JudgmentZonePhase lastPhase = JudgmentZonePhase.Centered;

    bool isRunning;

    float crazyFatiguePeriodMultiplier = 1f;

    Color arcMaskBaseColor;
    Color innerBaseColor;
    Color innerMirrorBaseColor;
    Color outerBaseColor;
    Color outerMirrorBaseColor;

    const float ZoneFlashDuration = 0.15f;
    const float ZoneCollapseEpsilon = 0.01f;
    const float BobCenterEpsilon = 2f;

    enum ZoneFlashTarget
    {
        Inner,
        Outer,
        Red
    }

    enum JudgmentZonePhase
    {
        Centered,
        OppositeRandom,
        IrregularAccelSwing
    }

    public float CurrentBobAngleDeg => currentBobAngleDeg;
    public float ZoneCenterAngleDeg => zoneCenterAngleDeg;
    public float CurrentInnerHalfAngle => currentInnerHalfAngle;
    public float CurrentOuterHalfAngle => currentOuterHalfAngle;

    /// <summary>判定区缩至 0 且玩家未按键时触发。</summary>
    public event Action OnZoneCollapsedMiss;

    void Awake()
    {
        if (judgmentZoneArcMask != null)
            arcMaskBaseColor = judgmentZoneArcMask.color;

        if (judgmentZoneInner != null)
            innerBaseColor = judgmentZoneInner.color;

        if (judgmentZoneInnerMirror != null)
            innerMirrorBaseColor = judgmentZoneInnerMirror.color;

        if (judgmentZoneOuter != null)
            outerBaseColor = judgmentZoneOuter.color;

        if (judgmentZoneOuterMirror != null)
            outerMirrorBaseColor = judgmentZoneOuterMirror.color;

        SetupRadialWedge(judgmentZoneInner);
        SetupRadialWedge(judgmentZoneInnerMirror);
        SetupRadialWedge(judgmentZoneOuter);
        SetupRadialWedge(judgmentZoneOuterMirror);

        CacheVisualHalfAnglesFromInspectorOrScene();

        EnsureCenteredAtOrigin(pendulumBob);
    }

    void OnDestroy()
    {
        KillZoneFlashTweens();
    }

    void Update()
    {
        if (!isRunning || balanceConfig == null)
            return;

        UpdateAngles();
        UpdateZoneShrink(Time.deltaTime);
        UpdateTransforms();
    }

    public void ApplyBalance(GameBalanceConfig config)
    {
        balanceConfig = config;
        RefreshZoneSize();
    }

    public void ResetState()
    {
        bobPhaseRadians = 0f;
        lockedZoneCenterDeg = 0f;
        zoneSwingPhase = 0f;
        zoneSwingVelocity = 0f;
        phase3NoiseSeed = UnityEngine.Random.Range(0f, 1000f);
        lastPhase = JudgmentZonePhase.Centered;

        KillZoneFlashTweens();
        RestoreZoneColors();
        crazyFatiguePeriodMultiplier = 1f;

        if (balanceConfig != null)
            RefreshZoneSize();

        UpdateAngles();
        UpdateTransforms();
    }

    public void SetCrazyFatiguePeriodMultiplier(float multiplier)
    {
        crazyFatiguePeriodMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void StartRunning()
    {
        isRunning = true;
    }

    public void StopRunning()
    {
        isRunning = false;
    }

    public bool IsBobInSector(float halfAngleDeg)
    {
        return GetBobDeltaAngle() <= halfAngleDeg;
    }

    public float GetBobDeltaAngle()
    {
        return Mathf.Abs(currentBobAngleDeg - zoneCenterAngleDeg);
    }

    public BlinkResult? EvaluateBlinkInput()
    {
        if (!isRunning || balanceConfig == null)
            return null;

        if (currentOuterHalfAngle <= ZoneCollapseEpsilon)
            return null;

        float deltaAngle = GetBobDeltaAngle();
        BlinkResult result;

        if (deltaAngle <= currentInnerHalfAngle)
        {
            TriggerZoneFlash(ZoneFlashTarget.Inner);
            result = BlinkResult.Success;
        }
        else if (deltaAngle <= currentOuterHalfAngle)
        {
            TriggerZoneFlash(ZoneFlashTarget.Outer);
            result = BlinkResult.EarlyLate;
        }
        else
        {
            TriggerZoneFlash(ZoneFlashTarget.Red);
            result = BlinkResult.CompleteMiss;
        }

        RefreshZoneAfterBlink();
        UpdateTransforms();
        return result;
    }

    void RefreshZoneSize()
    {
        if (balanceConfig == null)
            return;

        float sizeMultiplier = GetZoneSizeMultiplier();
        baseInnerHalfAngle = balanceConfig.JudgmentInnerHalfAngle * sizeMultiplier;
        baseOuterHalfAngle = Mathf.Max(
            balanceConfig.JudgmentOuterHalfAngle * sizeMultiplier,
            baseInnerHalfAngle);
        currentInnerHalfAngle = baseInnerHalfAngle;
        currentOuterHalfAngle = baseOuterHalfAngle;

        visualInnerHalfAngleBase = visualInnerHalfAngle * sizeMultiplier;
        visualOuterHalfAngleBase = Mathf.Max(visualOuterHalfAngle * sizeMultiplier, visualInnerHalfAngleBase);
    }

    void RefreshZoneAfterBlink()
    {
        RefreshZoneSize();
        RefreshZonePlacementAfterBlink();
    }

    void RefreshZonePlacementAfterBlink()
    {
        switch (GetCurrentPhase())
        {
            case JudgmentZonePhase.Centered:
                lockedZoneCenterDeg = 0f;
                break;
            case JudgmentZonePhase.OppositeRandom:
                PickOppositeRandomCenter();
                break;
        }
    }

    void UpdateZoneShrink(float deltaTime)
    {
        if (balanceConfig.JudgmentShrinkPerSecond <= 0f)
            return;

        if (currentOuterHalfAngle <= ZoneCollapseEpsilon)
            return;

        currentOuterHalfAngle -= balanceConfig.JudgmentShrinkPerSecond * deltaTime;

        if (currentOuterHalfAngle <= ZoneCollapseEpsilon)
        {
            currentOuterHalfAngle = 0f;
            currentInnerHalfAngle = 0f;
            TriggerZoneCollapsedMiss();
            return;
        }

        float ratio = currentOuterHalfAngle / baseOuterHalfAngle;
        currentInnerHalfAngle = baseInnerHalfAngle * ratio;
    }

    void TriggerZoneCollapsedMiss()
    {
        TriggerZoneFlash(ZoneFlashTarget.Red);
        OnZoneCollapsedMiss?.Invoke();
        RefreshZoneAfterBlink();
    }

    void UpdateAngles()
    {
        JudgmentZonePhase phase = GetCurrentPhase();

        if (phase == JudgmentZonePhase.IrregularAccelSwing
            && lastPhase != JudgmentZonePhase.IrregularAccelSwing)
        {
            InitializeZoneSwingFromAngle(lockedZoneCenterDeg);
        }

        lastPhase = phase;

        float period = Mathf.Max(0.01f, GetEffectiveSwingPeriod());
        float maxAngle = balanceConfig.PendulumMaxAngle;
        bobPhaseRadians += 2f * Mathf.PI * Time.deltaTime / period;
        currentBobAngleDeg = Mathf.Sin(bobPhaseRadians) * maxAngle;
        zoneCenterAngleDeg = CalculateZoneCenterAngle(phase, Time.deltaTime);
    }

    float CalculateZoneCenterAngle(JudgmentZonePhase phase, float deltaTime)
    {
        switch (phase)
        {
            case JudgmentZonePhase.Centered:
                return 0f;
            case JudgmentZonePhase.OppositeRandom:
                return lockedZoneCenterDeg;
            case JudgmentZonePhase.IrregularAccelSwing:
                return UpdateZoneSwingAndGetCenter(deltaTime);
            default:
                return 0f;
        }
    }

    float UpdateZoneSwingAndGetCenter(float deltaTime)
    {
        float omega = balanceConfig.ZonePhase3SwingOmega;
        float t = Time.time * balanceConfig.ZonePhase3IrregularAccelSpeed;
        float irregularAccel = (Mathf.PerlinNoise(t, phase3NoiseSeed) * 2f - 1f)
                               * balanceConfig.ZonePhase3IrregularAccelAmplitude;

        float restoreAccel = -omega * omega * Mathf.Sin(zoneSwingPhase);
        zoneSwingVelocity += (restoreAccel + irregularAccel) * deltaTime;
        zoneSwingPhase += zoneSwingVelocity * deltaTime;

        return Mathf.Sin(zoneSwingPhase) * GetEffectiveZoneSwingAmplitude();
    }

    void InitializeZoneSwingFromAngle(float centerDeg)
    {
        float amplitude = GetEffectiveZoneSwingAmplitude();
        if (amplitude <= ZoneCollapseEpsilon)
        {
            zoneSwingPhase = 0f;
            zoneSwingVelocity = balanceConfig.ZonePhase3SwingOmega;
            return;
        }

        float normalized = Mathf.Clamp(centerDeg / amplitude, -1f, 1f);
        zoneSwingPhase = Mathf.Asin(normalized);
        zoneSwingVelocity = balanceConfig.ZonePhase3SwingOmega
                            * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
    }

    void PickOppositeRandomCenter()
    {
        float maxCenterAbs = GetMaxZoneCenterAbsAngle();
        float minOffset = Mathf.Min(balanceConfig.ZonePhase2MinCenterOffset, maxCenterAbs);

        if (maxCenterAbs <= ZoneCollapseEpsilon)
        {
            lockedZoneCenterDeg = 0f;
            return;
        }

        if (maxCenterAbs <= minOffset)
        {
            lockedZoneCenterDeg = currentBobAngleDeg > 0f ? -maxCenterAbs : maxCenterAbs;
            return;
        }

        float bob = currentBobAngleDeg;
        if (Mathf.Abs(bob) <= BobCenterEpsilon)
        {
            lockedZoneCenterDeg = UnityEngine.Random.value > 0.5f
                ? UnityEngine.Random.Range(minOffset, maxCenterAbs)
                : UnityEngine.Random.Range(-maxCenterAbs, -minOffset);
        }
        else if (bob > 0f)
        {
            lockedZoneCenterDeg = UnityEngine.Random.Range(-maxCenterAbs, -minOffset);
        }
        else
        {
            lockedZoneCenterDeg = UnityEngine.Random.Range(minOffset, maxCenterAbs);
        }
    }

    JudgmentZonePhase GetCurrentPhase()
    {
        if (fatigueMeter == null || balanceConfig == null)
            return JudgmentZonePhase.Centered;

        float fatigue = fatigueMeter.Value;
        if (fatigue >= balanceConfig.ZonePhase3AtFatigue)
            return JudgmentZonePhase.IrregularAccelSwing;
        if (fatigue >= balanceConfig.ZonePhase2AtFatigue)
            return JudgmentZonePhase.OppositeRandom;

        return JudgmentZonePhase.Centered;
    }

    float GetZoneSizeMultiplier()
    {
        JudgmentZonePhase phase = GetCurrentPhase();
        if (phase == JudgmentZonePhase.OppositeRandom
            || phase == JudgmentZonePhase.IrregularAccelSwing)
        {
            return balanceConfig.ZonePhase2SizeMultiplier;
        }

        return 1f;
    }

    float GetEffectiveSwingPeriod()
    {
        float basePeriod = balanceConfig.PendulumSwingPeriod;
        float period = basePeriod;
        switch (GetCurrentPhase())
        {
            case JudgmentZonePhase.OppositeRandom:
                period = basePeriod * balanceConfig.PendulumPhase2PeriodMultiplier;
                break;
            case JudgmentZonePhase.IrregularAccelSwing:
                period = basePeriod * balanceConfig.PendulumPhase3PeriodMultiplier;
                break;
        }

        return period * crazyFatiguePeriodMultiplier;
    }

    float GetMaxZoneCenterAbsAngle()
    {
        float trackLimit = balanceConfig.PendulumMaxAngle;
        float margin = currentOuterHalfAngle;
        return Mathf.Max(0f, trackLimit - margin);
    }

    float GetEffectiveZoneSwingAmplitude()
    {
        float desired = balanceConfig.ZonePhase3SwingAmplitude;
        return Mathf.Min(desired, GetMaxZoneCenterAbsAngle());
    }

    void UpdateTransforms()
    {
        UpdatePendulumBobRotation();
        UpdateZoneWedges();
    }

    void UpdatePendulumBobRotation()
    {
        if (pendulumBob == null)
            return;

        // 逻辑角：0=弧顶，正=右侧；Unity UI 顺时针为负 Z
        pendulumBob.localRotation = Quaternion.Euler(
            0f, 0f, -currentBobAngleDeg + bobRotationOffset);
    }

    void UpdateZoneWedges()
    {
        // 与指针一致：逻辑角正=右侧，Unity UI 顺时针为负 Z（否则判定区会被镜像到指针同侧）
        if (judgmentZoneRoot != null)
            judgmentZoneRoot.localRotation = Quaternion.Euler(0f, 0f, -zoneCenterAngleDeg);

        float shrinkRatio = baseOuterHalfAngle > ZoneCollapseEpsilon
            ? currentOuterHalfAngle / baseOuterHalfAngle
            : 0f;

        float displayOuterHalfAngle = visualOuterHalfAngleBase * shrinkRatio;
        float displayInnerHalfAngle = visualInnerHalfAngleBase * shrinkRatio;

        ApplySymmetricSector(judgmentZoneOuter, judgmentZoneOuterMirror, displayOuterHalfAngle);
        ApplySymmetricSector(judgmentZoneInner, judgmentZoneInnerMirror, displayInnerHalfAngle);
    }

    void ApplySymmetricSector(Image clockwiseHalf, Image counterClockwiseHalf, float halfAngleDeg)
    {
        ApplyHalfWedge(clockwiseHalf, halfAngleDeg, clockwise: true);
        ApplyHalfWedge(counterClockwiseHalf, halfAngleDeg, clockwise: false);
    }

    void ApplyHalfWedge(Image image, float halfAngleDeg, bool clockwise)
    {
        if (image == null)
            return;

        image.rectTransform.localRotation = Quaternion.identity;
        image.fillClockwise = clockwise;
        image.fillAmount = halfAngleDeg / 360f;
    }

    void CacheVisualHalfAnglesFromInspectorOrScene()
    {
        if (visualInnerHalfAngle <= 0f && judgmentZoneInner != null)
            visualInnerHalfAngle = judgmentZoneInner.fillAmount * 360f;

        if (visualOuterHalfAngle <= 0f && judgmentZoneOuter != null)
            visualOuterHalfAngle = judgmentZoneOuter.fillAmount * 360f;

        visualInnerHalfAngle = Mathf.Max(0f, visualInnerHalfAngle);
        visualOuterHalfAngle = Mathf.Max(visualOuterHalfAngle, visualInnerHalfAngle);
        visualInnerHalfAngleBase = visualInnerHalfAngle;
        visualOuterHalfAngleBase = visualOuterHalfAngle;
    }

    static void EnsureCenteredAtOrigin(RectTransform rect)
    {
        if (rect == null)
            return;

        if (rect.anchorMin != rect.anchorMax)
        {
            float size = Mathf.Max(rect.rect.width, rect.rect.height);
            PrepareCenteredRect(rect, size > 0f ? size : 980f);
            return;
        }

        rect.anchoredPosition = Vector2.zero;
    }

    static void PrepareCenteredRect(RectTransform rect, float size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
    }

    static void SetupRadialWedge(Image image)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
    }

    void TriggerZoneFlash(ZoneFlashTarget target)
    {
        switch (target)
        {
            case ZoneFlashTarget.Inner:
                FlashImagePair(judgmentZoneInner, innerBaseColor, judgmentZoneInnerMirror, innerMirrorBaseColor);
                break;
            case ZoneFlashTarget.Outer:
                FlashImagePair(judgmentZoneOuter, outerBaseColor, judgmentZoneOuterMirror, outerMirrorBaseColor);
                break;
            case ZoneFlashTarget.Red:
                FlashImage(judgmentZoneArcMask, arcMaskBaseColor);
                break;
        }
    }

    void FlashImagePair(Image first, Color firstBase, Image second, Color secondBase)
    {
        FlashImage(first, firstBase);
        FlashImage(second, secondBase);
    }

    void FlashImage(Image image, Color baseColor)
    {
        if (image == null)
            return;

        image.DOKill();
        image.color = baseColor;
        image.DOColor(Color.white, ZoneFlashDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo);
    }

    void KillZoneFlashTweens()
    {
        judgmentZoneArcMask?.DOKill();
        judgmentZoneInner?.DOKill();
        judgmentZoneInnerMirror?.DOKill();
        judgmentZoneOuter?.DOKill();
        judgmentZoneOuterMirror?.DOKill();
    }

    void RestoreZoneColors()
    {
        if (judgmentZoneArcMask != null)
            judgmentZoneArcMask.color = arcMaskBaseColor;

        if (judgmentZoneInner != null)
            judgmentZoneInner.color = innerBaseColor;

        if (judgmentZoneInnerMirror != null)
            judgmentZoneInnerMirror.color = innerMirrorBaseColor;

        if (judgmentZoneOuter != null)
            judgmentZoneOuter.color = outerBaseColor;

        if (judgmentZoneOuterMirror != null)
            judgmentZoneOuterMirror.color = outerMirrorBaseColor;
    }
}
