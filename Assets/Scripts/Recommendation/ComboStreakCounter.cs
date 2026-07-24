using System;
using UnityEngine;

/// <summary>
/// 连续正确筛选连击；错误操作或续连超时清零。Phase 6 火热时间触发用。
/// 连击越高，下一次正确前的允许间隔越短。
/// </summary>
public class ComboStreakCounter : MonoBehaviour
{
    [Header("System References")]
    [Tooltip("仅在 Playing 状态下计时与续连。")]
    [SerializeField] RunController _runController;

    float _baseWindow = 4f;
    float _windowShrinkPerStreak = 0.35f;
    float _minWindow = 1.2f;

    int _currentStreak;
    int _maxStreakThisRun;
    float _remainingWindow;

    public int CurrentStreak => _currentStreak;
    public int MaxStreakThisRun => _maxStreakThisRun;
    public float StreakTimeRemaining => _remainingWindow;
    public float CurrentWindowDuration => GetWindowForStreak(_currentStreak);

    public event Action<int> OnStreakChanged;

    void Awake()
    {
        if (_runController == null)
            _runController = FindObjectOfType<RunController>();
    }

    void Update()
    {
        if (_currentStreak <= 0)
            return;

        if (_runController != null && _runController.CurrentState != GameState.Playing)
            return;

        _remainingWindow -= Time.deltaTime;
        if (_remainingWindow <= 0f)
            ClearStreak();
    }

    public void ApplyBalance(GameBalanceConfig config)
    {
        if (config == null)
            return;

        _baseWindow = Mathf.Max(0.1f, config.ComboBaseWindow);
        _windowShrinkPerStreak = Mathf.Max(0f, config.ComboWindowShrinkPerStreak);
        _minWindow = Mathf.Max(0.1f, config.ComboMinWindow);

        if (_currentStreak > 0)
            _remainingWindow = Mathf.Min(_remainingWindow, GetWindowForStreak(_currentStreak));
    }

    public void Reset()
    {
        _currentStreak = 0;
        _maxStreakThisRun = 0;
        _remainingWindow = 0f;
        OnStreakChanged?.Invoke(_currentStreak);
    }

    public void RegisterSwipe(bool wasCorrect)
    {
        if (wasCorrect)
        {
            _currentStreak++;
            if (_currentStreak > _maxStreakThisRun)
                _maxStreakThisRun = _currentStreak;

            _remainingWindow = GetWindowForStreak(_currentStreak);
        }
        else
        {
            ClearStreak();
            return;
        }

        OnStreakChanged?.Invoke(_currentStreak);
    }

    float GetWindowForStreak(int streak)
    {
        if (streak <= 0)
            return 0f;

        float shrink = (streak - 1) * _windowShrinkPerStreak;
        return Mathf.Max(_minWindow, _baseWindow - shrink);
    }

    void ClearStreak()
    {
        if (_currentStreak == 0)
            return;

        _currentStreak = 0;
        _remainingWindow = 0f;
        OnStreakChanged?.Invoke(_currentStreak);
    }
}
