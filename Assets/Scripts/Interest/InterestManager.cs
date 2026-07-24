using System;
using UnityEngine;

/// <summary>
/// 维护当前兴趣 Topic，定时切换并在切换前发出预告事件。
/// </summary>
[DisallowMultipleComponent]
public class InterestManager : MonoBehaviour
{
    static readonly TopicId[] AllTopics =
    {
        TopicId.Pets,
        TopicId.People,
        TopicId.Foods,
        TopicId.Cars,
        TopicId.Football
    };

    [Header("Timing")]
    [Tooltip("Minimum seconds before the next interest switch.")]
    [SerializeField] float _minSwitchInterval = 15f;
    [Tooltip("Maximum seconds before the next interest switch.")]
    [SerializeField] float _maxSwitchInterval = 30f;
    [Tooltip("Seconds before switch when OnSwitchPreview fires.")]
    [SerializeField] float _previewLeadTime = 2.5f;

    [Header("System References")]
    [Tooltip("Used to pause switching outside Playing.")]
    [SerializeField] RunController _runController;

    TopicId currentTopic;
    TopicId upcomingTopic;
    bool isRunning;
    bool pausedByOverlay;
    bool previewFired;
    float switchAtTime;
    float previewAtTime;

    public TopicId CurrentTopic => currentTopic;

    public event Action<TopicId> OnInterestChanged;
    public event Action<TopicId> OnSwitchPreview;

    void Awake()
    {
        if (_runController == null)
            _runController = FindObjectOfType<RunController>();
    }

    void OnEnable()
    {
        if (_runController != null)
            _runController.OnStateChanged += HandleRunStateChanged;
    }

    void OnDisable()
    {
        if (_runController != null)
            _runController.OnStateChanged -= HandleRunStateChanged;
    }

    void Update()
    {
        if (!isRunning || _runController == null || _runController.CurrentState != GameState.Playing)
            return;

        float now = Time.time;

        if (!previewFired && now >= previewAtTime)
        {
            previewFired = true;
            OnSwitchPreview?.Invoke(upcomingTopic);
        }

        if (now >= switchAtTime)
            ApplyScheduledSwitch();
    }

    public void ResetAndStart()
    {
        isRunning = true;
        currentTopic = PickRandomTopic();
        OnInterestChanged?.Invoke(currentTopic);
        ScheduleNextCycle();
    }

    public void Stop()
    {
        isRunning = false;
        pausedByOverlay = false;
    }

    /// <summary>教程 overlay 打开时暂停计时，关闭后恢复。</summary>
    public void PauseForOverlay()
    {
        if (!isRunning)
            return;

        pausedByOverlay = true;
        isRunning = false;
    }

    public void ResumeFromOverlay()
    {
        if (!pausedByOverlay || _runController == null)
            return;

        pausedByOverlay = false;
        GameState state = _runController.CurrentState;
        if (state == GameState.Playing || state == GameState.HotStreak)
            isRunning = true;
    }

    /// <summary>Phase 6：火热时间结束后立刻切换到新的兴趣。</summary>
    public void ForceSwitchNow()
    {
        if (!isRunning)
            return;

        currentTopic = PickNextTopic(currentTopic);
        OnInterestChanged?.Invoke(currentTopic);
        ScheduleNextCycle();
    }

    void HandleRunStateChanged(GameState state)
    {
        if (state == GameState.Win || state == GameState.Lose)
            Stop();
    }

    void ApplyScheduledSwitch()
    {
        currentTopic = upcomingTopic;
        OnInterestChanged?.Invoke(currentTopic);
        ScheduleNextCycle();
    }

    void ScheduleNextCycle()
    {
        upcomingTopic = PickNextTopic(currentTopic);

        float interval = UnityEngine.Random.Range(_minSwitchInterval, _maxSwitchInterval);
        switchAtTime = Time.time + interval;
        previewAtTime = switchAtTime - _previewLeadTime;

        if (previewAtTime <= Time.time)
            previewAtTime = Time.time;

        previewFired = previewAtTime >= switchAtTime;
    }

    TopicId PickRandomTopic()
    {
        return AllTopics[UnityEngine.Random.Range(0, AllTopics.Length)];
    }

    TopicId PickNextTopic(TopicId exclude)
    {
        if (AllTopics.Length <= 1)
            return AllTopics[0];

        TopicId picked = exclude;
        int safety = 0;
        while (picked == exclude && safety < 16)
        {
            picked = AllTopics[UnityEngine.Random.Range(0, AllTopics.Length)];
            safety++;
        }

        return picked;
    }
}
