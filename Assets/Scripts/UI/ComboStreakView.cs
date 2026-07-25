using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 连击 HUD（HotBar + ComboNum）：streak≥1 时显示；连击越高 scale 上限越大，
/// 火热开始时 ButtonIcon 代替连击数字并闪烁，HotBar 大幅 scale duang 后进入持续抖动；结束时大抖后消失。
/// </summary>
public class ComboStreakView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("HotBar 根物体；控制整体显隐、缩放与抖动。")]
    [SerializeField] GameObject _displayRoot;
    [Tooltip("显示当前连击数 TextMeshPro。")]
    [SerializeField] TMP_Text _streakText;
    [Tooltip("火热时间开始时显示并代替连击数字（HotBar 上的 ButtonIcon）。")]
    [SerializeField] GameObject _hotReadyIcon;

    [Header("System References")]
    [SerializeField] ComboStreakCounter _comboStreakCounter;
    [SerializeField] HotTimeController _hotTimeController;
    [SerializeField] RunController _runController;

    [Header("Progress Scale")]
    [SerializeField] float _minScale = 0.7f;
    [SerializeField] float _maxScale = 1.25f;
    [SerializeField] float _scaleTweenDuration = 0.15f;
    [Tooltip("续连倒计时 scale 曲线指数；越小越长时间保持大 scale，末尾缩得越快。")]
    [SerializeField] float _timeScaleHoldPower = 0.35f;

    [Header("Combo Bump")]
    [SerializeField] float _comboPunchDuration = 0.28f;
    [Tooltip("DOPunchScale 增量；相对当前 scale 弹出。")]
    [SerializeField] float _comboPunchStrength = 0.18f;
    [SerializeField] int _comboPunchVibrato = 8;
    [SerializeField] float _comboPunchElasticity = 0.55f;

    [Header("Expire Warning")]
    [Tooltip("续连窗口剩余多少秒时触发失效前抖动。")]
    [SerializeField] float _expireWarningTime = 0.35f;
    [SerializeField] float _expireShakeDuration = 0.3f;
    [SerializeField] float _expireShakeStrength = 16f;

    [Header("Hot Streak Icon")]
    [SerializeField] float _hotReadyBlinkHalfDuration = 0.45f;

    [Header("Hot Start Duang")]
    [SerializeField] float _hotStartPunchDuration = 0.45f;
    [SerializeField] float _hotStartPunchStrength = 0.38f;
    [SerializeField] int _hotStartPunchVibrato = 10;
    [SerializeField] float _hotStartPunchElasticity = 0.42f;

    [Header("Hot Streak Shake")]
    [SerializeField] float _hotLoopDuration = 0.35f;
    [SerializeField] float _hotLoopStrength = 6f;
    [SerializeField] float _hotEndDuration = 0.45f;
    [SerializeField] float _hotEndStrength = 28f;

    RectTransform _displayRect;
    Vector2 _restAnchoredPosition;
    Vector3 _restLocalScale;
    int _lastStreak;
    bool _hotEndPlaying;
    bool _expireWarningPlayed;
    float _peakScale = 1f;
    float _currentTargetScale = 1f;
    float _scaleOverrideUntil;
    Image _hotReadyIconImage;
    Color _hotReadyIconBaseColor;
    Tweener _hotReadyBlinkTween;

    void Awake()
    {
        if (_comboStreakCounter == null)
            _comboStreakCounter = FindObjectOfType<ComboStreakCounter>();

        if (_hotTimeController == null)
            _hotTimeController = FindObjectOfType<HotTimeController>();

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_displayRoot != null)
        {
            _displayRect = _displayRoot.transform as RectTransform;
            CacheRestTransform();
        }

        if (_hotReadyIcon != null)
        {
            _hotReadyIconImage = _hotReadyIcon.GetComponent<Image>();
            if (_hotReadyIconImage != null)
                _hotReadyIconBaseColor = _hotReadyIconImage.color;

            _hotReadyIcon.SetActive(false);
        }
    }

    void Start()
    {
        if (_displayRoot != null && !IsHotActive())
            _displayRoot.SetActive(false);

        RefreshPresentation();
    }

    void OnEnable()
    {
        if (_comboStreakCounter != null)
            _comboStreakCounter.OnStreakChanged += HandleStreakChanged;

        if (_hotTimeController != null)
        {
            _hotTimeController.OnHotTimeStarted += HandleHotTimeStarted;
            _hotTimeController.OnHotTimeEnded += HandleHotTimeEnded;
        }

        if (_runController != null)
            _runController.OnStateChanged += HandleRunStateChanged;

        RefreshPresentation();
    }

    void OnDisable()
    {
        if (_comboStreakCounter != null)
            _comboStreakCounter.OnStreakChanged -= HandleStreakChanged;

        if (_hotTimeController != null)
        {
            _hotTimeController.OnHotTimeStarted -= HandleHotTimeStarted;
            _hotTimeController.OnHotTimeEnded -= HandleHotTimeEnded;
        }

        if (_runController != null)
            _runController.OnStateChanged -= HandleRunStateChanged;

        StopAllTweens();
    }

    void Update()
    {
        UpdateTimeBasedScale();
    }

    void OnDestroy()
    {
        StopAllTweens();
    }

    void HandleStreakChanged(int streak)
    {
        if (IsHotActive() || _hotEndPlaying)
            return;

        bool playBump = streak > _lastStreak && streak >= 1;
        RefreshPresentation(playBump);
    }

    void HandleHotTimeStarted()
    {
        _hotEndPlaying = false;
        StopAllTweens();

        SetRootActive(true);
        SetTextVisible(false);
        SetHotStreakIconVisible(true);
        PlayHotStartDuang();
    }

    void PlayHotStartDuang()
    {
        if (_displayRect == null)
        {
            StartHotLoopShake();
            return;
        }

        _scaleOverrideUntil = Time.time + _hotStartPunchDuration;
        _currentTargetScale = _maxScale;

        _displayRect.DOKill(complete: false);
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _displayRect.localScale = _restLocalScale * _maxScale;
        _displayRect
            .DOPunchScale(
                Vector3.one * _hotStartPunchStrength,
                _hotStartPunchDuration,
                _hotStartPunchVibrato,
                _hotStartPunchElasticity)
            .OnComplete(StartHotLoopShake);
    }

    void HandleHotTimeEnded()
    {
        StopHotLoopShake();

        if (_displayRect == null)
        {
            HideImmediate();
            return;
        }

        _hotEndPlaying = true;
        PlayHotEndShake(() =>
        {
            _hotEndPlaying = false;
            HideImmediate();
            _lastStreak = _comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0;
        });
    }

    void HandleRunStateChanged(GameState state)
    {
        if (state == GameState.Playing || state == GameState.HotStreak)
            return;

        HideImmediate();
        _lastStreak = 0;
    }

    void RefreshPresentation(bool playBumpOnIncrease = false)
    {
        if (_displayRect == null)
            return;

        if (IsHotActive() || _hotEndPlaying)
            return;

        GameState state = _runController != null ? _runController.CurrentState : GameState.Playing;
        if (state != GameState.Playing)
        {
            HideImmediate();
            return;
        }

        int streak = _comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0;
        if (streak < 1)
        {
            HideImmediate();
            _lastStreak = streak;
            return;
        }

        SetRootActive(true);
        SetTextVisible(true);
        SetHotStreakIconVisible(false);
        UpdateText(streak);
        _peakScale = CalculatePeakScale(streak);
        _expireWarningPlayed = false;
        SetScaleImmediate(_peakScale);

        if (playBumpOnIncrease && streak > _lastStreak)
            PlayComboBump();

        _lastStreak = streak;
    }

    void UpdateText(int streak)
    {
        if (_streakText == null)
            return;

        _streakText.text = streak.ToString();
    }

    void UpdateTimeBasedScale()
    {
        if (_displayRect == null || IsHotActive() || _hotEndPlaying)
            return;

        if (_runController != null && _runController.CurrentState != GameState.Playing)
            return;

        if (_comboStreakCounter == null || _comboStreakCounter.CurrentStreak < 1)
            return;

        float windowDuration = _comboStreakCounter.CurrentWindowDuration;
        float remaining = _comboStreakCounter.StreakTimeRemaining;
        if (windowDuration <= 0f)
            return;

        float timeRatio = Mathf.Clamp01(remaining / windowDuration);
        float shapedRatio = Mathf.Pow(timeRatio, _timeScaleHoldPower);
        float targetScale = Mathf.Lerp(_minScale, _peakScale, shapedRatio);

        if (Time.time >= _scaleOverrideUntil)
            SetScaleImmediate(targetScale);

        if (!_expireWarningPlayed && remaining <= _expireWarningTime)
        {
            _expireWarningPlayed = true;
            PlayExpireWarningShake();
        }
    }

    float CalculatePeakScale(int streak)
    {
        int trigger = GetTriggerStreak();
        float progress = trigger > 0 ? Mathf.Clamp01((float)streak / trigger) : 0f;
        return Mathf.Lerp(_minScale, _maxScale, progress);
    }

    void SetScaleImmediate(float targetScale)
    {
        if (_displayRect == null)
            return;

        _currentTargetScale = targetScale;
        _displayRect.localScale = _restLocalScale * targetScale;
    }

    void ApplyScale(float targetScale)
    {
        if (_displayRect == null)
            return;

        _currentTargetScale = targetScale;
        _displayRect.DOKill(complete: false);
        _displayRect.localScale = _restLocalScale * _currentTargetScale;
        _displayRect.DOScale(_restLocalScale * targetScale, _scaleTweenDuration);
    }

    void PlayExpireWarningShake()
    {
        if (_displayRect == null)
            return;

        StopPositionShake();
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _displayRect.DOShakeAnchorPos(
            _expireShakeDuration,
            _expireShakeStrength,
            vibrato: 24,
            randomness: 90f,
            fadeOut: true);
    }

    void PlayComboBump()
    {
        if (_displayRect == null)
            return;

        _scaleOverrideUntil = Time.time + _comboPunchDuration;

        _displayRect.DOKill(complete: false);
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _currentTargetScale = _peakScale;
        _displayRect.localScale = _restLocalScale * _peakScale;
        _displayRect.DOPunchScale(
            Vector3.one * _comboPunchStrength,
            _comboPunchDuration,
            _comboPunchVibrato,
            _comboPunchElasticity);
    }

    void StartHotLoopShake()
    {
        if (_displayRect == null)
            return;

        StopPositionShake();
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _displayRect.DOShakeAnchorPos(
                _hotLoopDuration,
                _hotLoopStrength,
                vibrato: 20,
                randomness: 90f,
                fadeOut: false)
            .SetLoops(-1, LoopType.Restart);
    }

    void PlayHotEndShake(Action onComplete)
    {
        if (_displayRect == null)
        {
            onComplete?.Invoke();
            return;
        }

        StopPositionShake();
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _displayRect.DOShakeAnchorPos(
                _hotEndDuration,
                _hotEndStrength,
                vibrato: 14,
                randomness: 90f,
                fadeOut: true)
            .OnComplete(() => onComplete?.Invoke());
    }

    void HideImmediate()
    {
        StopAllTweens();
        SetHotStreakIconVisible(false);
        ResetTransform();
        SetRootActive(false);
        SetTextVisible(false);
    }

    void SetHotStreakIconVisible(bool visible)
    {
        if (_hotReadyIcon == null)
            return;

        if (!visible)
        {
            StopHotStreakIconBlink();
            _hotReadyIcon.SetActive(false);
            return;
        }

        _hotReadyIcon.SetActive(true);
        StartHotStreakIconBlink();
    }

    void StartHotStreakIconBlink()
    {
        if (_hotReadyIconImage == null)
            return;

        StopHotStreakIconBlink();
        _hotReadyIconImage.color = _hotReadyIconBaseColor;
        _hotReadyBlinkTween = _hotReadyIconImage
            .DOColor(Color.white, _hotReadyBlinkHalfDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void StopHotStreakIconBlink()
    {
        if (_hotReadyBlinkTween != null)
        {
            _hotReadyBlinkTween.Kill();
            _hotReadyBlinkTween = null;
        }

        if (_hotReadyIconImage != null)
            _hotReadyIconImage.color = _hotReadyIconBaseColor;
    }

    void SetRootActive(bool active)
    {
        if (_displayRoot != null)
            _displayRoot.SetActive(active);
    }

    void SetTextVisible(bool visible)
    {
        if (_streakText != null)
            _streakText.gameObject.SetActive(visible);
    }

    void CacheRestTransform()
    {
        if (_displayRect == null)
            return;

        _restAnchoredPosition = _displayRect.anchoredPosition;
        _restLocalScale = _displayRect.localScale;
    }

    void ResetTransform()
    {
        if (_displayRect == null)
            return;

        _currentTargetScale = 1f;
        _scaleOverrideUntil = 0f;
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _displayRect.localScale = _restLocalScale;
    }

    void StopPositionShake()
    {
        if (_displayRect == null)
            return;

        _displayRect.DOKill(complete: false);
        _displayRect.anchoredPosition = _restAnchoredPosition;
        _displayRect.localScale = _restLocalScale * _currentTargetScale;
    }

    void StopHotLoopShake()
    {
        StopPositionShake();
    }

    void StopAllTweens()
    {
        StopHotStreakIconBlink();

        if (_displayRect == null)
            return;

        _displayRect.DOKill();
        ResetTransform();
    }

    bool IsHotActive()
    {
        return _hotTimeController != null && _hotTimeController.IsActive;
    }

    int GetTriggerStreak()
    {
        GameBalanceConfig config = _runController != null ? _runController.BalanceConfig : null;
        return config != null ? Mathf.Max(1, config.HotTriggerStreak) : 5;
    }
}
