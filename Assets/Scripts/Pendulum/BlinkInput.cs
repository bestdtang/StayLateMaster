using System;
using UnityEngine;

/// <summary>
/// 监听空格键，驱动 PendulumController 判定并抛出三态事件。
/// </summary>
public class BlinkInput : MonoBehaviour
{
    [Header("System References")]
    [Tooltip("Evaluates blink timing against the pendulum zones.")]
    [SerializeField] PendulumController pendulumController;
    [Tooltip("Used to gate input to Playing / HotStreak states.")]
    [SerializeField] RunController runController;

    public event Action OnBlinkSuccess;
    public event Action OnBlinkEarlyLate;
    public event Action OnBlinkCompleteMiss;

    void OnEnable()
    {
        if (pendulumController != null)
            pendulumController.OnZoneCollapsedMiss += HandleZoneCollapsedMiss;
    }

    void OnDisable()
    {
        if (pendulumController != null)
            pendulumController.OnZoneCollapsedMiss -= HandleZoneCollapsedMiss;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Space))
            return;

        if (runController != null && !IsBlinkAllowedState(runController.CurrentState))
            return;

        if (pendulumController == null)
            return;

        BlinkResult? result = pendulumController.EvaluateBlinkInput();
        if (result == null)
            return;

        switch (result.Value)
        {
            case BlinkResult.Success:
                OnBlinkSuccess?.Invoke();
                break;
            case BlinkResult.EarlyLate:
                OnBlinkEarlyLate?.Invoke();
                break;
            case BlinkResult.CompleteMiss:
                OnBlinkCompleteMiss?.Invoke();
                break;
        }
    }

    void HandleZoneCollapsedMiss()
    {
        if (runController != null && !IsBlinkAllowedState(runController.CurrentState))
            return;

        OnBlinkCompleteMiss?.Invoke();
    }

    static bool IsBlinkAllowedState(GameState state)
    {
        return state == GameState.Playing || state == GameState.HotStreak;
    }
}
