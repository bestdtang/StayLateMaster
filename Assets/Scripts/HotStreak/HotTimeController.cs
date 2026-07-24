using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 火热时间：连续正确达到阈值触发；持续数秒，期间推荐流强制当前兴趣，
/// 玩家持续右滑，每次正确固定加快乐；结束后切一次兴趣。
/// 同一根进度条：累积阶段按连击数上涨，火热阶段作为倒计时下降。
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
    [Tooltip("进度条根物体；Playing 期间常显，累积上涨 / 火热倒计时共用。")]
    [SerializeField] GameObject _barRoot;
    [Tooltip("填充 Image：累积 = 连击/阈值，火热 = 剩余时间/总时长。")]
    [SerializeField] Image _countdownFill;

    int _triggerStreak = 5;
    float _duration = 3f;
    float _happinessPerSwipe = 6f;
    float _postBufferDuration = 0.6f;

    bool _isActive;
    float _remaining;
    float _postBufferRemaining;
    int _hotStreakCountThisRun;

    public bool IsActive => _isActive;
    public bool IsPostHotBufferActive => _postBufferRemaining > 0f;
    public int HotStreakCountThisRun => _hotStreakCountThisRun;

    public event Action OnHotTimeStarted;
    public event Action OnHotTimeEnded;

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
    }

    void Update()
    {
        if (_runController != null && _runController.CurrentState == GameState.Paused)
            return;

        if (_isActive)
        {
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
        _postBufferDuration = Mathf.Max(0f, config.HotPostBufferDuration);

        if (!_isActive)
            UpdateAccumulationBar(_comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0);
    }

    public void Reset()
    {
        _hotStreakCountThisRun = 0;
        CancelPostHotBuffer(restoreInput: false);
        StopHotTimeInternal(switchInterest: false);
        ShowBar();
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
        _remaining = _duration;
        _hotStreakCountThisRun++;

        if (_interestManager != null && _cardPicker != null)
            _cardPicker.SetForcedTopic(_interestManager.CurrentTopic);

        ShowBar();
        UpdateHotTimeBar();

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
        _remaining = 0f;

        _cardPicker?.ClearForcedTopic();

        if (!wasActive)
            return;

        _runController?.ExitHotStreak();
        OnHotTimeEnded?.Invoke();

        ShowBar();
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

    void ShowBar()
    {
        if (_barRoot != null)
            _barRoot.SetActive(true);
    }

    void UpdateAccumulationBar(int streak)
    {
        if (_countdownFill == null)
            return;

        _countdownFill.fillAmount = _triggerStreak > 0
            ? Mathf.Clamp01((float)streak / _triggerStreak)
            : 0f;
    }

    void UpdateHotTimeBar()
    {
        if (_countdownFill == null)
            return;

        _countdownFill.fillAmount = _duration > 0f
            ? Mathf.Clamp01(_remaining / _duration)
            : 0f;
    }
}
