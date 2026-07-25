using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 快乐条 MiddleLayer：加快乐时中间层先跳、Fill 后追；减快乐时 Fill 先落、中间层后追。
/// 高频加值（火热连滑）用 Retarget 连续追赶，避免 Kill 重开。
/// </summary>
public class HappinessMiddleLayerView : MonoBehaviour
{
    enum LagDirection
    {
        None,
        Gain,
        Loss
    }

    [Header("Target")]
    [SerializeField] RectTransform _barRoot;
    [SerializeField] Image _middleLayerImage;
    [SerializeField] Image _fillImage;
    [SerializeField] HappinessMeter _happinessMeter;

    [Header("Colors")]
    [SerializeField] Color _gainColor = new Color(1f, 0.746f, 0.451f, 1f);
    [SerializeField] Color _lossColor = new Color(0.55f, 0.48f, 0.62f, 1f);

    [Header("Catch Up")]
    [SerializeField] float _gainCatchUpDuration = 0.28f;
    [SerializeField] float _minGainCatchUpDuration = 0.08f;
    [SerializeField] float _lossCatchUpDuration = 0.35f;
    [SerializeField] float _minLossCatchUpDuration = 0.1f;
    [SerializeField] Ease _catchUpEase = Ease.OutCubic;

    [Header("Gain Punch")]
    [SerializeField] float _gainPunchDuration = 0.2f;
    [SerializeField] float _gainPunchStrength = 0.08f;
    [SerializeField] int _gainPunchVibrato = 6;
    [SerializeField] float _gainPunchElasticity = 0.45f;

    [Header("Loss Shake")]
    [Tooltip("减快乐：仅向右快速抖动后回原点，X 不越过 rest。")]
    [SerializeField] float _lossShakeDuration = 0.14f;
    [SerializeField] float _lossShakeOffset = 10f;
    [SerializeField] int _lossShakePulses = 3;

    const float TypicalGainFillDelta = 8f / HappinessMeter.MaxValue;

    Vector3 _baseScale = Vector3.one;
    Vector2 _restAnchoredPosition;
    float _targetFill;
    LagDirection _lastDirection = LagDirection.None;
    Tweener _fillCatchUpTween;
    Tweener _middleCatchUpTween;
    Tween _gainPunchTween;
    Tween _lossShakeTween;

    void Awake()
    {
        if (_happinessMeter == null)
            _happinessMeter = GetComponent<HappinessMeter>();

        if (_barRoot == null)
            _barRoot = transform as RectTransform;

        if (_barRoot != null)
        {
            _baseScale = _barRoot.localScale;
            _restAnchoredPosition = _barRoot.anchoredPosition;
        }
    }

    void OnEnable()
    {
        if (_happinessMeter == null)
            return;

        _happinessMeter.SetFillDriveExternal(true);
        _happinessMeter.OnValueChanged += HandleValueChanged;

        if (_barRoot != null)
            _restAnchoredPosition = _barRoot.anchoredPosition;

        SyncInstant(_happinessMeter.Value);
    }

    void OnDisable()
    {
        if (_happinessMeter != null)
        {
            _happinessMeter.OnValueChanged -= HandleValueChanged;
            _happinessMeter.SetFillDriveExternal(false);
        }

        StopCatchUpTweens();
        StopBarJuice();
    }

    void OnDestroy()
    {
        StopCatchUpTweens();
        StopBarJuice();
    }

    void SyncInstant(float value)
    {
        StopCatchUpTweens();
        StopBarJuice();
        _targetFill = value / HappinessMeter.MaxValue;
        _lastDirection = LagDirection.None;

        if (_targetFill <= 0f)
        {
            SnapBoth(0f);
            SetMiddleVisible(false);
            return;
        }

        SetMiddleVisible(true);
        SnapBoth(_targetFill);
    }

    void HandleValueChanged(float newValue)
    {
        float newTarget = newValue / HappinessMeter.MaxValue;

        if (newTarget <= 0f)
        {
            StopCatchUpTweens();
            _targetFill = 0f;
            _lastDirection = LagDirection.None;
            SnapBoth(0f);
            SetMiddleVisible(false);
            return;
        }

        SetMiddleVisible(true);

        if (Mathf.Approximately(newTarget, _targetFill))
            return;

        bool isGain = newTarget > _targetFill;
        LagDirection newDirection = isGain ? LagDirection.Gain : LagDirection.Loss;

        if (_lastDirection != LagDirection.None && _lastDirection != newDirection)
            StopCatchUpTweens();

        _targetFill = newTarget;
        _lastDirection = newDirection;

        if (isGain)
            PlayGain(newTarget);
        else
            PlayLoss(newTarget);
    }

    void PlayGain(float target)
    {
        if (_middleLayerImage != null)
        {
            _middleLayerImage.fillAmount = target;
            _middleLayerImage.color = _gainColor;
        }

        float from = _fillImage != null ? _fillImage.fillAmount : 0f;
        float duration = ComputeGainDuration(from, target);
        EnsureFillCatchUpTween(target, duration);
        PlayGainPunch();
    }

    void PlayLoss(float target)
    {
        if (_fillImage != null)
            _fillImage.fillAmount = target;

        if (_middleLayerImage != null)
            _middleLayerImage.color = _lossColor;

        float from = _middleLayerImage != null ? _middleLayerImage.fillAmount : 0f;
        float duration = ComputeLossDuration(from, target);
        EnsureMiddleCatchUpTween(target, duration);
        PlayLossShake();
    }

    void EnsureFillCatchUpTween(float target, float duration)
    {
        if (_fillImage == null || duration <= 0f)
        {
            if (_fillImage != null)
                _fillImage.fillAmount = target;
            return;
        }

        if (_fillCatchUpTween != null && _fillCatchUpTween.IsActive())
        {
            _fillCatchUpTween.ChangeEndValue(target, duration, false);
            return;
        }

        _fillCatchUpTween = _fillImage
            .DOFillAmount(target, duration)
            .SetEase(_catchUpEase);
    }

    void EnsureMiddleCatchUpTween(float target, float duration)
    {
        if (_middleLayerImage == null || duration <= 0f)
        {
            if (_middleLayerImage != null)
                _middleLayerImage.fillAmount = target;
            return;
        }

        if (_middleCatchUpTween != null && _middleCatchUpTween.IsActive())
        {
            _middleCatchUpTween.ChangeEndValue(target, duration, false);
            return;
        }

        _middleCatchUpTween = _middleLayerImage
            .DOFillAmount(target, duration)
            .SetEase(_catchUpEase);
    }

    float ComputeGainDuration(float from, float to)
    {
        float delta = Mathf.Abs(to - from);
        if (delta <= 0f)
            return 0f;

        float scaled = _gainCatchUpDuration * (delta / TypicalGainFillDelta);
        return Mathf.Clamp(scaled, _minGainCatchUpDuration, _gainCatchUpDuration);
    }

    float ComputeLossDuration(float from, float to)
    {
        float delta = Mathf.Abs(to - from);
        if (delta <= 0f)
            return 0f;

        float typicalLossDelta = 3f / HappinessMeter.MaxValue;
        float scaled = _lossCatchUpDuration * (delta / typicalLossDelta);
        return Mathf.Clamp(scaled, _minLossCatchUpDuration, _lossCatchUpDuration);
    }

    void SnapBoth(float fill)
    {
        if (_fillImage != null)
            _fillImage.fillAmount = fill;

        if (_middleLayerImage != null)
            _middleLayerImage.fillAmount = fill;
    }

    void SetMiddleVisible(bool visible)
    {
        if (_middleLayerImage != null)
            _middleLayerImage.gameObject.SetActive(visible);
    }

    void PlayGainPunch()
    {
        if (_barRoot == null || _gainPunchDuration <= 0f || _gainPunchStrength <= 0f)
            return;

        StopBarJuice();
        ClampBarScaleToBase();

        Vector3 punch = _baseScale * _gainPunchStrength;
        _gainPunchTween = _barRoot
            .DOPunchScale(punch, _gainPunchDuration, _gainPunchVibrato, _gainPunchElasticity)
            .OnUpdate(ClampBarScaleToBase)
            .OnComplete(ResetBarScale);
    }

    void PlayLossShake()
    {
        if (_barRoot == null || _lossShakeDuration <= 0f || _lossShakeOffset <= 0f)
            return;

        StopBarJuice();
        ResetBarPosition();

        int pulses = Mathf.Max(1, _lossShakePulses);
        float halfPulse = _lossShakeDuration / (pulses * 2f);
        Sequence sequence = DOTween.Sequence();

        for (int i = 0; i < pulses; i++)
        {
            float decay = 1f - i / (float)pulses * 0.35f;
            float offset = _lossShakeOffset * decay;
            sequence.Append(
                _barRoot.DOAnchorPos(_restAnchoredPosition + Vector2.right * offset, halfPulse)
                    .SetEase(Ease.OutQuad));
            sequence.Append(
                _barRoot.DOAnchorPos(_restAnchoredPosition, halfPulse)
                    .SetEase(Ease.InQuad));
        }

        _lossShakeTween = sequence
            .OnUpdate(ClampBarPositionNotLeft)
            .OnComplete(ResetBarPosition);
    }

    void ClampBarPositionNotLeft()
    {
        if (_barRoot == null)
            return;

        Vector2 pos = _barRoot.anchoredPosition;
        pos.x = Mathf.Max(pos.x, _restAnchoredPosition.x);
        _barRoot.anchoredPosition = pos;
    }

    void ResetBarPosition()
    {
        if (_barRoot != null)
            _barRoot.anchoredPosition = _restAnchoredPosition;
    }

    void ClampBarScaleToBase()
    {
        if (_barRoot == null)
            return;

        Vector3 scale = _barRoot.localScale;
        _barRoot.localScale = new Vector3(
            Mathf.Max(scale.x, _baseScale.x),
            Mathf.Max(scale.y, _baseScale.y),
            Mathf.Max(scale.z, _baseScale.z));
    }

    void ResetBarScale()
    {
        if (_barRoot != null)
            _barRoot.localScale = _baseScale;
    }

    void StopGainPunch()
    {
        if (_gainPunchTween != null)
        {
            _gainPunchTween.Kill();
            _gainPunchTween = null;
        }

        ResetBarScale();
    }

    void StopLossShake()
    {
        if (_lossShakeTween != null)
        {
            _lossShakeTween.Kill();
            _lossShakeTween = null;
        }

        ResetBarPosition();
    }

    void StopBarJuice()
    {
        StopGainPunch();
        StopLossShake();
    }

    void StopCatchUpTweens()
    {
        if (_fillCatchUpTween != null)
        {
            _fillCatchUpTween.Kill();
            _fillCatchUpTween = null;
        }

        if (_middleCatchUpTween != null)
        {
            _middleCatchUpTween.Kill();
            _middleCatchUpTween = null;
        }
    }
}
