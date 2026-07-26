using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 火热时间：连续正确达到阈值触发；持续数秒，期间推荐流强制当前兴趣，
/// 玩家持续右滑，每次正确固定加快乐；结束后切一次兴趣。
/// 同一根进度条：累积阶段按连击数上涨；火热开场先满格缓冲，再作为倒计时下降。
/// 满格进入火热时 fill 闪到火热色并在下降阶段保持，下次累积恢复原色。
/// </summary>
public class HotTimeController : MonoBehaviour
{
    [Header("System References")]
    [Tooltip("运行流程中枢，用于进入/退出火热状态。")]
    [SerializeField] RunController _runController;
    [Tooltip("火热期间固定增加快乐值。")]
    [SerializeField] HappinessMeter _happinessMeter;
    [Tooltip("读取当前连续正确数用于触发与进度条累积。")]
    [SerializeField] ComboStreakCounter _comboStreakCounter;
    [Tooltip("提供当前兴趣，并在火热结束后切换一次。")]
    [SerializeField] InterestManager _interestManager;
    [Tooltip("火热期间强制只出当前兴趣 Topic。")]
    [SerializeField] CardPicker _cardPicker;
    [Tooltip("火热结束后短暂禁用左右滑输入。")]
    [SerializeField] SwipeInput _swipeInput;

    [Header("Progress Bar")]
    [Tooltip("填充 Image：累积 = 连击/阈值，火热 = 剩余时间/总时长。显隐由 ComboStreakView 管理。")]
    [SerializeField] Image _countdownFill;
    [Tooltip("连击累积阶段（填满过程）使用的 fill 颜色。")]
    [SerializeField] Color _normalFillColor = new Color(1f, 0.35f, 0f, 1f);
    [Tooltip("满格进入火热后、倒计时下降期间使用的 fill 颜色。")]
    [SerializeField] Color _hotFillColor = new Color(0.35f, 1f, 0.45f, 1f);
    [Tooltip("满格时在原色与火热色之间闪烁的半程时长。")]
    [SerializeField] float _hotFillFlashHalfDuration = 0.1f;
    [Tooltip("闪烁半程次数（奇数结束在火热色）。")]
    [SerializeField] int _hotFillFlashHalfLoops = 3;

    int _triggerStreak = 5;
    float _duration = 3f;
    float _happinessPerSwipe = 6f;
    float _preBufferDuration = 0.4f;
    float _postBufferDuration = 0.6f;

    bool _isActive;
    float _remaining;
    float _preBufferRemaining;
    float _postBufferRemaining;
    int _hotStreakCountThisRun;
    Tweener _fillColorTween;

    public bool IsActive => _isActive;
    public bool IsPreHotBufferActive => _isActive && _preBufferRemaining > 0f;
    public bool IsPostHotBufferActive => _postBufferRemaining > 0f;
    public int HotStreakCountThisRun => _hotStreakCountThisRun;

    public event Action OnHotTimeStarted;
    public event Action OnHotTimeEnded;
    public event Action<bool> OnHotSwipe;

    void Awake()
    {
        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_happinessMeter == null)
            _happinessMeter = FindObjectOfType<HappinessMeter>();

        if (_comboStreakCounter == null)
            _comboStreakCounter = FindObjectOfType<ComboStreakCounter>();

        if (_interestManager == null)
            _interestManager = FindObjectOfType<InterestManager>();

        if (_cardPicker == null)
            _cardPicker = FindObjectOfType<CardPicker>();

        if (_swipeInput == null)
            _swipeInput = FindObjectOfType<SwipeInput>();

        RestoreBaseFillColor();
    }

    void OnEnable()
    {
        if (_runController != null)
            _runController.OnStateChanged += HandleRunStateChanged;

        if (_comboStreakCounter != null)
            _comboStreakCounter.OnStreakChanged += HandleStreakChanged;
    }

    void OnDisable()
    {
        if (_runController != null)
            _runController.OnStateChanged -= HandleRunStateChanged;

        if (_comboStreakCounter != null)
            _comboStreakCounter.OnStreakChanged -= HandleStreakChanged;

        KillFillColorTween();
    }

    void OnDestroy()
    {
        KillFillColorTween();
    }

    void Update()
    {
        if (_runController != null && _runController.CurrentState == GameState.Paused)
            return;

        if (_isActive)
        {
            if (_preBufferRemaining > 0f)
            {
                _preBufferRemaining -= Time.deltaTime;
                UpdateHotTimeBar();
                return;
            }

            _remaining -= Time.deltaTime;
            UpdateHotTimeBar();

            if (_remaining <= 0f)
                EndHotTime();

            return;
        }

        if (_postBufferRemaining <= 0f)
            return;

        _postBufferRemaining -= Time.deltaTime;
        if (_postBufferRemaining <= 0f)
            EndPostHotBuffer();
    }

    public void ApplyBalance(GameBalanceConfig config)
    {
        if (config == null)
            return;

        _triggerStreak = Mathf.Max(1, config.HotTriggerStreak);
        _duration = Mathf.Max(0.1f, config.HotTimeDuration);
        _happinessPerSwipe = config.HotHappinessPerSwipe;
        _preBufferDuration = Mathf.Max(0f, config.HotPreBufferDuration);
        _postBufferDuration = Mathf.Max(0f, config.HotPostBufferDuration);

        if (!_isActive)
            UpdateAccumulationBar(_comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0);
    }

    public void Reset()
    {
        _hotStreakCountThisRun = 0;
        CancelPostHotBuffer(restoreInput: false);
        StopHotTimeInternal(switchInterest: false);
        UpdateAccumulationBar(0);
    }

    /// <summary>Playing 状态下每次正确滑卡后调用；达到连击阈值则触发火热时间。</summary>
    public void RegisterCorrectSwipe()
    {
        if (_isActive)
            return;

        if (_runController != null && _runController.CurrentState != GameState.Playing)
            return;

        int streak = _comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0;
        if (streak >= _triggerStreak)
            StartHotTime();
    }

    /// <summary>火热期间每次滑卡调用；右滑（正确）固定加快乐，左滑无事发生。</summary>
    public void RegisterHotSwipe(bool wasCorrect)
    {
        if (!_isActive)
            return;

        if (wasCorrect && _happinessMeter != null)
            _happinessMeter.Add(_happinessPerSwipe);

        OnHotSwipe?.Invoke(wasCorrect);
    }

    void HandleStreakChanged(int streak)
    {
        if (_isActive)
            return;

        UpdateAccumulationBar(streak);
    }

    void StartHotTime()
    {
        if (_isActive)
            return;

        _isActive = true;
        _preBufferRemaining = _preBufferDuration;
        _remaining = _duration;
        _hotStreakCountThisRun++;

        if (_interestManager != null && _cardPicker != null)
            _cardPicker.SetForcedTopic(_interestManager.CurrentTopic);

        UpdateHotTimeBar();
        PlayHotFillFlash();

        _runController?.EnterHotStreak();
        _comboStreakCounter?.Reset();
        OnHotTimeStarted?.Invoke();

        Debug.Log("[HotTimeController] 进入火热时间。");
    }

    void EndHotTime()
    {
        if (!_isActive)
            return;

        StopHotTimeInternal(switchInterest: true);
        Debug.Log("[HotTimeController] 火热时间结束。");
    }

    void StopHotTimeInternal(bool switchInterest)
    {
        bool wasActive = _isActive;
        _isActive = false;
        _preBufferRemaining = 0f;
        _remaining = 0f;

        _cardPicker?.ClearForcedTopic();

        if (!wasActive)
            return;

        _runController?.ExitHotStreak();
        OnHotTimeEnded?.Invoke();

        UpdateAccumulationBar(_comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0);

        if (switchInterest)
        {
            _interestManager?.ForceSwitchNow();
            BeginPostHotBuffer();
        }
    }

    void BeginPostHotBuffer()
    {
        if (_postBufferDuration <= 0f)
            return;

        _postBufferRemaining = _postBufferDuration;
        _swipeInput?.SetInputEnabled(false);
    }

    void EndPostHotBuffer()
    {
        _postBufferRemaining = 0f;

        if (_runController != null && _runController.CurrentState == GameState.Playing)
            _swipeInput?.SetInputEnabled(true);
    }

    void CancelPostHotBuffer(bool restoreInput)
    {
        _postBufferRemaining = 0f;

        if (restoreInput
            && _runController != null
            && _runController.CurrentState == GameState.Playing)
        {
            _swipeInput?.SetInputEnabled(true);
        }
    }

    void HandleRunStateChanged(GameState state)
    {
        if (state == GameState.Win || state == GameState.Lose)
        {
            CancelPostHotBuffer(restoreInput: false);
            StopHotTimeInternal(switchInterest: false);
        }
    }

    void UpdateAccumulationBar(int streak)
    {
        if (_countdownFill == null)
            return;

        RestoreBaseFillColor();
        _countdownFill.fillAmount = _triggerStreak > 0
            ? Mathf.Clamp01((float)streak / _triggerStreak)
            : 0f;
    }

    void UpdateHotTimeBar()
    {
        if (_countdownFill == null)
            return;

        if (_preBufferRemaining > 0f)
        {
            _countdownFill.fillAmount = 1f;
            return;
        }

        _countdownFill.fillAmount = _duration > 0f
            ? Mathf.Clamp01(_remaining / _duration)
            : 0f;
    }

    void PlayHotFillFlash()
    {
        if (_countdownFill == null)
            return;

        KillFillColorTween();
        _countdownFill.color = _normalFillColor;

        int loops = Mathf.Max(1, _hotFillFlashHalfLoops);
        if ((loops & 1) == 0)
            loops++;

        _fillColorTween = _countdownFill
            .DOColor(_hotFillColor, Mathf.Max(0.01f, _hotFillFlashHalfDuration))
            .SetEase(Ease.Linear)
            .SetLoops(loops, LoopType.Yoyo)
            .OnComplete(() =>
            {
                if (_countdownFill != null)
                    _countdownFill.color = _hotFillColor;
                _fillColorTween = null;
            });
    }

    void RestoreBaseFillColor()
    {
        KillFillColorTween();

        if (_countdownFill != null)
            _countdownFill.color = _normalFillColor;
    }

    void KillFillColorTween()
    {
        if (_fillColorTween == null)
            return;

        _fillColorTween.Kill();
        _fillColorTween = null;
    }
}
