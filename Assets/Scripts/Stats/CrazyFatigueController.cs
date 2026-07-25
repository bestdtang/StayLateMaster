using System;
using UnityEngine;

/// <summary>
/// 连续完全失败进入超级疲劳：额外疲劳增速 + 钟摆略快；成功或偏早/偏晚眨眼解除。
/// </summary>
public class CrazyFatigueController : MonoBehaviour
{
    const float PersistentModifierDuration = 99999f;

    [Header("System References")]
    [SerializeField] FatigueMeter _fatigueMeter;
    [SerializeField] PendulumController _pendulumController;

    int _triggerStreak = 3;
    float _rateMultiplier = 1.35f;
    float _pendulumPeriodMultiplier = 0.92f;

    int _consecutiveCompleteMisses;
    bool _isActive;

    public bool IsActive => _isActive;

    public event Action OnEntered;
    public event Action OnExited;

    void Awake()
    {
        if (_fatigueMeter == null)
            _fatigueMeter = FindObjectOfType<FatigueMeter>();

        if (_pendulumController == null)
            _pendulumController = FindObjectOfType<PendulumController>();
    }

    void OnDisable()
    {
        if (_isActive)
            ExitInternal(silent: true);
    }

    public void ApplyBalance(GameBalanceConfig config)
    {
        if (config == null)
            return;

        _triggerStreak = Mathf.Max(1, config.CrazyFatigueTriggerStreak);
        _rateMultiplier = Mathf.Max(1f, config.CrazyFatigueRateMultiplier);
        _pendulumPeriodMultiplier = Mathf.Clamp(config.CrazyFatiguePendulumPeriodMultiplier, 0.1f, 1f);
    }

    public void Reset()
    {
        _consecutiveCompleteMisses = 0;
        if (_isActive)
            ExitInternal(silent: true);
    }

    public void RegisterCompleteMiss()
    {
        if (_isActive)
            return;

        _consecutiveCompleteMisses++;
        if (_consecutiveCompleteMisses >= _triggerStreak)
            Enter();
    }

    public void RegisterNonMissBlink()
    {
        _consecutiveCompleteMisses = 0;

        if (_isActive)
            ExitInternal(silent: false);
    }

    void Enter()
    {
        if (_isActive || _fatigueMeter == null)
            return;

        _isActive = true;
        _consecutiveCompleteMisses = 0;

        _fatigueMeter.ApplyRateModifier(
            FatigueModifierSource.CrazyFatigue,
            _rateMultiplier,
            PersistentModifierDuration);

        _pendulumController?.SetCrazyFatiguePeriodMultiplier(_pendulumPeriodMultiplier);

        Debug.Log("[CrazyFatigue] 进入超级疲劳：疲劳增速 ×" + _rateMultiplier
                  + "，钟摆周期 ×" + _pendulumPeriodMultiplier);
        OnEntered?.Invoke();
    }

    void ExitInternal(bool silent)
    {
        if (!_isActive)
            return;

        _isActive = false;
        _consecutiveCompleteMisses = 0;

        _fatigueMeter?.ClearModifier(FatigueModifierSource.CrazyFatigue);
        _pendulumController?.SetCrazyFatiguePeriodMultiplier(1f);

        if (silent)
            return;

        Debug.Log("[CrazyFatigue] 解除超级疲劳。");
        OnExited?.Invoke();
    }
}
