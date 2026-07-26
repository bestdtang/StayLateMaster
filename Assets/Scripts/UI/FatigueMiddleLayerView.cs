using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 疲惫条 MiddleLayer：始终比 Fill 略长；按 Blink 增速修饰换色、颜色 pulse、fillAmount 呼吸；
/// TiredBar 根节点三档 scaleY 蠕动（普通 / 加速 / 疯狂）。
/// </summary>
public class FatigueMiddleLayerView : MonoBehaviour
{
    enum VisualMode
    {
        Neutral,
        Slowed,
        Accelerated,
        Crazy
    }

    enum WriggleTier
    {
        Normal,
        Accelerated,
        Crazy
    }

    [Header("Target")]
    [SerializeField] RectTransform _barRoot;
    [SerializeField] Image _middleLayerImage;
    [SerializeField] FatigueMeter _fatigueMeter;

    [Header("Fill Lead")]
    [SerializeField] float _leadAmount = 0.04f;

    [Header("Mode Colors")]
    [SerializeField] Color _slowedColor = new Color(0.613f, 0.693f, 1f, 1f);
    [SerializeField] Color _neutralColor = new Color(0.72f, 0.78f, 0.68f, 1f);
    [SerializeField] Color _acceleratedColor = new Color(1f, 0.45f, 0.28f, 1f);

    [Header("Pulse")]
    [SerializeField] float _slowPulseHalfDuration = 0.55f;
    [SerializeField] float _neutralPulseHalfDuration = 0.35f;
    [SerializeField] float _franticPulseHalfDuration = 0.1f;
    [SerializeField] float _pulseHighlight = 0.45f;

    [Header("Fill Breath")]
    [Tooltip("三态 lead 段 fillAmount 呼吸幅度（peak-to-peak，clamp 到 0–1）。")]
    [SerializeField] float _slowBreathAmplitude = 0.008f;
    [SerializeField] float _neutralBreathAmplitude = 0.018f;
    [SerializeField] float _franticBreathAmplitude = 0.04f;
    [SerializeField] float _slowBreathHalfDuration = 0.55f;
    [SerializeField] float _neutralBreathHalfDuration = 0.35f;
    [SerializeField] float _franticBreathHalfDuration = 0.1f;

    [Header("ScaleY Wriggle")]
    [SerializeField] float _normalWriggleAmplitude = 0.035f;
    [SerializeField] float _normalWriggleHalfDuration = 0.45f;
    [SerializeField] float _acceleratedWriggleAmplitude = 0.06f;
    [SerializeField] float _acceleratedWriggleHalfDuration = 0.22f;
    [SerializeField] float _crazyWriggleAmplitude = 0.1f;
    [SerializeField] float _crazyWriggleHalfDuration = 0.08f;

    VisualMode _currentMode = (VisualMode)(-1);
    WriggleTier _currentWriggleTier = (WriggleTier)(-1);
    Vector3 _baseScale = Vector3.one;
    float _baseFillAmount;
    float _breathPhase;
    Tweener _pulseTween;
    Tweener _breathTween;
    Tweener _wriggleTween;

    void Awake()
    {
        if (_fatigueMeter == null)
            _fatigueMeter = GetComponent<FatigueMeter>();

        if (_barRoot == null)
            _barRoot = transform as RectTransform;

        if (_barRoot != null)
            _baseScale = _barRoot.localScale;
    }

    void OnEnable()
    {
        if (_fatigueMeter == null)
            return;

        if (_barRoot != null)
            _baseScale = _barRoot.localScale;

        _fatigueMeter.OnValueChanged += HandleValueChanged;
        _fatigueMeter.OnModifiersChanged += RefreshVisualState;

        HandleValueChanged(_fatigueMeter.Value);
        RefreshVisualState();
    }

    void OnDisable()
    {
        if (_fatigueMeter != null)
        {
            _fatigueMeter.OnValueChanged -= HandleValueChanged;
            _fatigueMeter.OnModifiersChanged -= RefreshVisualState;
        }

        StopVisualTweens();
    }

    void OnDestroy()
    {
        StopVisualTweens();
    }

    void HandleValueChanged(float value)
    {
        if (_middleLayerImage == null)
            return;

        if (value <= 0f)
        {
            _middleLayerImage.gameObject.SetActive(false);
            StopMiddleLayerTweens();
            _currentMode = (VisualMode)(-1);
            EnsureWriggle(WriggleTier.Normal);
            return;
        }

        _middleLayerImage.gameObject.SetActive(true);
        _baseFillAmount = ComputeDisplayFill(value);
        ApplyFillAmountFromBreath();
        RefreshVisualState();
    }

    /// <summary>
    /// 未达 MaxValue 时 lead/呼吸不得视觉顶死为 1.0；满值才贴满。
    /// </summary>
    float ComputeDisplayFill(float value)
    {
        if (value >= FatigueMeter.MaxValue)
            return 1f;

        float fill = value / FatigueMeter.MaxValue + _leadAmount;
        return Mathf.Min(fill, 0.99f);
    }

    void RefreshVisualState()
    {
        if (_fatigueMeter == null)
            return;

        if (_fatigueMeter.Value <= 0f)
        {
            EnsureWriggle(WriggleTier.Normal);
            return;
        }

        if (_middleLayerImage == null)
            return;

        VisualMode mode = ResolveMode();
        WriggleTier wriggleTier = ResolveWriggleTier(mode);
        if (mode == _currentMode && _pulseTween != null && _pulseTween.IsActive()
            && _breathTween != null && _breathTween.IsActive()
            && _wriggleTween != null && _wriggleTween.IsActive()
            && wriggleTier == _currentWriggleTier)
            return;

        _currentMode = mode;
        ApplyModeVisuals(mode);
    }

    VisualMode ResolveMode()
    {
        if (_fatigueMeter.IsCrazyFatigueActive)
            return VisualMode.Crazy;

        if (_fatigueMeter.TryGetBlinkRateMultiplier(out float multiplier))
        {
            if (multiplier < 1f)
                return VisualMode.Slowed;

            if (multiplier > 1f)
                return VisualMode.Accelerated;
        }

        return VisualMode.Neutral;
    }

    void ApplyModeVisuals(VisualMode mode)
    {
        Color baseColor = mode switch
        {
            VisualMode.Slowed => _slowedColor,
            VisualMode.Accelerated => _acceleratedColor,
            VisualMode.Crazy => _acceleratedColor,
            _ => _neutralColor
        };

        float pulseHalfDuration = mode switch
        {
            VisualMode.Slowed => _slowPulseHalfDuration,
            VisualMode.Accelerated => _franticPulseHalfDuration,
            VisualMode.Crazy => _franticPulseHalfDuration,
            _ => _neutralPulseHalfDuration
        };

        float breathHalfDuration = mode switch
        {
            VisualMode.Slowed => _slowBreathHalfDuration,
            VisualMode.Accelerated => _franticBreathHalfDuration,
            VisualMode.Crazy => _franticBreathHalfDuration,
            _ => _neutralBreathHalfDuration
        };

        StopMiddleLayerTweens();
        _middleLayerImage.color = baseColor;

        Color pulseTarget = Color.Lerp(baseColor, Color.white, _pulseHighlight);
        _pulseTween = _middleLayerImage
            .DOColor(pulseTarget, pulseHalfDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo);

        _breathPhase = 0f;
        ApplyFillAmountFromBreath();
        _breathTween = DOTween.To(
                () => _breathPhase,
                phase =>
                {
                    _breathPhase = phase;
                    ApplyFillAmountFromBreath();
                },
                1f,
                breathHalfDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo);

        EnsureWriggle(ResolveWriggleTier(mode));
    }

    void EnsureWriggle(WriggleTier tier)
    {
        if (_wriggleTween != null && _wriggleTween.IsActive() && tier == _currentWriggleTier)
            return;

        _currentWriggleTier = tier;
        StartWriggle(tier);
    }

    WriggleTier ResolveWriggleTier(VisualMode mode)
    {
        return mode switch
        {
            VisualMode.Crazy => WriggleTier.Crazy,
            VisualMode.Accelerated => WriggleTier.Accelerated,
            _ => WriggleTier.Normal
        };
    }

    void StartWriggle(WriggleTier tier)
    {
        if (_barRoot == null)
            return;

        float amplitude;
        float halfDuration;

        switch (tier)
        {
            case WriggleTier.Crazy:
                amplitude = _crazyWriggleAmplitude;
                halfDuration = _crazyWriggleHalfDuration;
                break;
            case WriggleTier.Accelerated:
                amplitude = _acceleratedWriggleAmplitude;
                halfDuration = _acceleratedWriggleHalfDuration;
                break;
            default:
                amplitude = _normalWriggleAmplitude;
                halfDuration = _normalWriggleHalfDuration;
                break;
        }

        if (amplitude <= 0f || halfDuration <= 0f)
        {
            ResetBarScale();
            return;
        }

        ResetBarScale();
        float peakY = _baseScale.y * (1f + amplitude);
        _wriggleTween = _barRoot
            .DOScaleY(peakY, halfDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void ApplyFillAmountFromBreath()
    {
        if (_middleLayerImage == null)
            return;

        float amplitude = GetBreathAmplitude(_currentMode);
        float offset = Mathf.Lerp(-amplitude * 0.5f, amplitude * 0.5f, _breathPhase);
        float fill = _baseFillAmount + offset;

        if (_fatigueMeter != null && _fatigueMeter.Value >= FatigueMeter.MaxValue)
            fill = 1f;
        else
            fill = Mathf.Min(fill, 0.99f);

        _middleLayerImage.fillAmount = Mathf.Clamp01(fill);
    }

    float GetBreathAmplitude(VisualMode mode)
    {
        return mode switch
        {
            VisualMode.Slowed => _slowBreathAmplitude,
            VisualMode.Accelerated => _franticBreathAmplitude,
            VisualMode.Crazy => _franticBreathAmplitude,
            _ => _neutralBreathAmplitude
        };
    }

    void ResetBarScale()
    {
        if (_barRoot != null)
            _barRoot.localScale = _baseScale;
    }

    void StopMiddleLayerTweens()
    {
        if (_pulseTween != null)
        {
            _pulseTween.Kill();
            _pulseTween = null;
        }

        if (_breathTween != null)
        {
            _breathTween.Kill();
            _breathTween = null;
        }
    }

    void StopWriggleTween()
    {
        if (_wriggleTween != null)
        {
            _wriggleTween.Kill();
            _wriggleTween = null;
        }

        _currentWriggleTier = (WriggleTier)(-1);
        ResetBarScale();
    }

    void StopVisualTweens()
    {
        StopMiddleLayerTweens();
        StopWriggleTween();
    }
}
