using System;
using UnityEngine;

/// <summary>
/// 驱动单局主循环：重置数值、管理状态、处理胜负。
/// </summary>
public class RunController : MonoBehaviour
{
    [Header("Balance")]
    [Tooltip("Global tuning asset for fatigue curve and blink modifiers.")]
    [SerializeField] GameBalanceConfig balanceConfig;

    [Header("System References")]
    [Tooltip("Happiness 0–100; win at max.")]
    [SerializeField] HappinessMeter happinessMeter;
    [Tooltip("Fatigue 0–102; lose at max.")]
    [SerializeField] FatigueMeter fatigueMeter;
    [Tooltip("Pendulum swing and judgment zones.")]
    [SerializeField] PendulumController pendulumController;
    [Tooltip("Space key blink input.")]
    [SerializeField] BlinkInput blinkInput;
    [Tooltip("Phone card spawn and swipe flow.")]
    [SerializeField] FeedSpawner feedSpawner;
    [Tooltip("Current interest topic and timed switches.")]
    [SerializeField] InterestManager interestManager;
    [Tooltip("Long-term Topic recommendation weights.")]
    [SerializeField] TopicWeights topicWeights;
    [Tooltip("Correct swipe streak for hot time (Phase 6).")]
    [SerializeField] ComboStreakCounter comboStreakCounter;
    [Tooltip("Swipe judgment and happiness updates.")]
    [SerializeField] SwipeFilterHandler swipeFilterHandler;
    [Tooltip("Hot time trigger, countdown, and forced-interest feed (Phase 6).")]
    [SerializeField] HotTimeController hotTimeController;
    [Tooltip("Consecutive complete-miss crazy fatigue state.")]
    [SerializeField] CrazyFatigueController crazyFatigueController;

    SwipeInput swipeInput;
    GameState stateBeforeTutorialPause;

    public GameBalanceConfig BalanceConfig => balanceConfig;

    GameState currentState = GameState.Intro;

    public GameState CurrentState => currentState;

    public event Action<GameState> OnStateChanged;
    public event Action OnGameWon;
    public event Action OnGameLost;

    void Awake()
    {
        if (pendulumController == null)
            pendulumController = FindObjectOfType<PendulumController>();

        if (blinkInput == null)
            blinkInput = FindObjectOfType<BlinkInput>();

        if (feedSpawner == null)
            feedSpawner = FindObjectOfType<FeedSpawner>();

        if (interestManager == null)
            interestManager = FindObjectOfType<InterestManager>();

        if (topicWeights == null)
            topicWeights = FindObjectOfType<TopicWeights>();

        if (comboStreakCounter == null)
            comboStreakCounter = FindObjectOfType<ComboStreakCounter>();

        if (swipeFilterHandler == null)
            swipeFilterHandler = FindObjectOfType<SwipeFilterHandler>();

        if (hotTimeController == null)
            hotTimeController = FindObjectOfType<HotTimeController>();

        if (crazyFatigueController == null)
            crazyFatigueController = FindObjectOfType<CrazyFatigueController>();

        if (swipeInput == null)
            swipeInput = FindObjectOfType<SwipeInput>();

        ApplyBalance();
        SubscribeBlinkInput();
    }

    void Start()
    {
        SubscribeMeters();
    }

    void LateUpdate()
    {
        EvaluateEndGame();
    }

    void OnDestroy()
    {
        UnsubscribeMeters();
        UnsubscribeBlinkInput();
    }

    public void BeginRun()
    {
        ApplyBalance();
        happinessMeter?.Reset();
        fatigueMeter?.Reset();

        pendulumController?.ResetState();
        pendulumController?.StartRunning();

        topicWeights?.Reset();
        comboStreakCounter?.Reset();
        hotTimeController?.Reset();
        crazyFatigueController?.Reset();

        SetState(GameState.Playing);
        fatigueMeter?.StartRunning();
        interestManager?.ResetAndStart();
        feedSpawner?.ResetAndSpawn();
    }

    /// <summary>
    /// 结算重开入口：切回 Intro 并停住玩法，由 TutorialPageView 播 321 后再 BeginRun。
    /// </summary>
    public void RestartRun()
    {
        PrepareRestartCountdown();
    }

    /// <summary>胜负后重开倒计时前：清场并回到 Intro，避免玩法仍按 Win/Lose 跑。</summary>
    public void PrepareRestartCountdown()
    {
        fatigueMeter?.StopRunning();
        pendulumController?.StopRunning();
        feedSpawner?.StopFeed();
        interestManager?.Stop();
        hotTimeController?.Reset();
        crazyFatigueController?.Reset();
        comboStreakCounter?.Reset();
        SetState(GameState.Intro);
    }

    /// <summary>局内教程 overlay 打开时暂停玩法（计时、钟摆、滑卡等）。</summary>
    public void PauseForTutorial()
    {
        if (currentState != GameState.Playing && currentState != GameState.HotStreak)
            return;

        stateBeforeTutorialPause = currentState;
        fatigueMeter?.StopRunning();
        pendulumController?.StopRunning();
        interestManager?.PauseForOverlay();
        swipeInput?.SetInputEnabled(false);
        SetState(GameState.Paused);
    }

    /// <summary>关闭局内教程 overlay 后恢复暂停前的玩法状态。</summary>
    public void ResumeFromTutorial()
    {
        if (currentState != GameState.Paused)
            return;

        GameState resumeState = stateBeforeTutorialPause;
        SetState(resumeState);

        fatigueMeter?.StartRunning();
        pendulumController?.StartRunning();
        interestManager?.ResumeFromOverlay();
        RestoreSwipeInput(resumeState);
    }

    void RestoreSwipeInput(GameState resumeState)
    {
        if (swipeInput == null)
            return;

        if (hotTimeController != null && hotTimeController.IsPostHotBufferActive)
        {
            swipeInput.SetInputEnabled(false);
            return;
        }

        if (resumeState == GameState.Playing || resumeState == GameState.HotStreak)
            swipeInput.SetInputEnabled(true);
    }

    /// <summary>Phase 6：进入火热时间（仅允许从 Playing 进入）。</summary>
    public void EnterHotStreak()
    {
        if (currentState != GameState.Playing)
            return;

        SetState(GameState.HotStreak);
    }

    /// <summary>Phase 6：退出火热时间，回到 Playing（仅在火热中生效）。</summary>
    public void ExitHotStreak()
    {
        if (currentState != GameState.HotStreak)
            return;

        SetState(GameState.Playing);
    }

    void SubscribeBlinkInput()
    {
        if (blinkInput == null)
            return;

        blinkInput.OnBlinkSuccess += HandleBlinkSuccess;
        blinkInput.OnBlinkEarlyLate += HandleBlinkEarlyLate;
        blinkInput.OnBlinkCompleteMiss += HandleBlinkCompleteMiss;
    }

    void UnsubscribeBlinkInput()
    {
        if (blinkInput == null)
            return;

        blinkInput.OnBlinkSuccess -= HandleBlinkSuccess;
        blinkInput.OnBlinkEarlyLate -= HandleBlinkEarlyLate;
        blinkInput.OnBlinkCompleteMiss -= HandleBlinkCompleteMiss;
    }

    void HandleBlinkSuccess()
    {
        if (!IsBlinkGameplayActive() || balanceConfig == null)
            return;

        crazyFatigueController?.RegisterNonMissBlink();

        fatigueMeter?.ApplyRateModifier(
            FatigueModifierSource.Blink,
            balanceConfig.BlinkSuccessRateMultiplier,
            balanceConfig.BlinkModifierDuration);

        Debug.Log("[RunController] 眨眼成功：疲劳增速 ×"
                  + balanceConfig.BlinkSuccessRateMultiplier);
    }

    void HandleBlinkEarlyLate()
    {
        if (!IsBlinkGameplayActive() || balanceConfig == null)
            return;

        crazyFatigueController?.RegisterNonMissBlink();

        fatigueMeter?.ApplyRateModifier(
            FatigueModifierSource.Blink,
            balanceConfig.BlinkEarlyLateRateMultiplier,
            balanceConfig.BlinkModifierDuration);

        Debug.Log("[RunController] 眨眼偏早/偏晚：疲劳增速 ×"
                  + balanceConfig.BlinkEarlyLateRateMultiplier);
    }

    void HandleBlinkCompleteMiss()
    {
        if (!IsBlinkGameplayActive() || balanceConfig == null)
            return;

        fatigueMeter?.ApplyRateModifier(
            FatigueModifierSource.Blink,
            balanceConfig.BlinkMissRateMultiplier,
            balanceConfig.BlinkModifierDuration);

        Debug.Log("[RunController] 红圈按键失败：疲劳增速 ×"
                  + balanceConfig.BlinkMissRateMultiplier);

        crazyFatigueController?.RegisterCompleteMiss();
    }

    void SubscribeMeters()
    {
        if (happinessMeter != null)
            happinessMeter.OnReachedMax += HandleMeterReachedMax;

        if (fatigueMeter != null)
            fatigueMeter.OnReachedMax += HandleMeterReachedMax;
    }

    void UnsubscribeMeters()
    {
        if (happinessMeter != null)
            happinessMeter.OnReachedMax -= HandleMeterReachedMax;

        if (fatigueMeter != null)
            fatigueMeter.OnReachedMax -= HandleMeterReachedMax;
    }

    void HandleMeterReachedMax()
    {
        EvaluateEndGame();
    }

    /// <summary>
    /// 集中胜负判定：同帧双满时快乐优先。
    /// </summary>
    void EvaluateEndGame()
    {
        if (currentState == GameState.Win || currentState == GameState.Lose)
            return;

        if (currentState != GameState.Playing && currentState != GameState.HotStreak)
            return;

        bool happyMax = happinessMeter != null
            && happinessMeter.Value >= HappinessMeter.MaxValue;
        bool fatigueMax = fatigueMeter != null
            && fatigueMeter.Value >= FatigueMeter.MaxValue;

        if (happyMax)
            EndRun(GameState.Win);
        else if (fatigueMax)
            EndRun(GameState.Lose);
    }

    void EndRun(GameState result)
    {
        if (result != GameState.Win && result != GameState.Lose)
            return;

        if (currentState == GameState.Win || currentState == GameState.Lose)
            return;

        fatigueMeter?.StopRunning();
        pendulumController?.StopRunning();
        feedSpawner?.StopFeed();
        interestManager?.Stop();
        SetState(result);

        if (result == GameState.Win)
        {
            Debug.Log($"[RunController] 胜利：快乐值已满。Happiness={happinessMeter?.Value:F2} Fatigue={fatigueMeter?.Value:F2}");
            OnGameWon?.Invoke();
        }
        else
        {
            Debug.Log($"[RunController] 失败：疲劳值已满。Happiness={happinessMeter?.Value:F2} Fatigue={fatigueMeter?.Value:F2}");
            OnGameLost?.Invoke();
        }
    }

    bool IsBlinkGameplayActive()
    {
        return currentState == GameState.Playing || currentState == GameState.HotStreak;
    }

    void SetState(GameState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        OnStateChanged?.Invoke(currentState);
    }

    void ApplyBalance()
    {
        fatigueMeter?.ApplyBalance(balanceConfig);
        pendulumController?.ApplyBalance(balanceConfig);
        hotTimeController?.ApplyBalance(balanceConfig);
        crazyFatigueController?.ApplyBalance(balanceConfig);
        comboStreakCounter?.ApplyBalance(balanceConfig);

        if (swipeFilterHandler != null)
            swipeFilterHandler.ApplyBalance(balanceConfig);
        else
            topicWeights?.ApplyBalance(balanceConfig);
    }
}
