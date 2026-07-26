using System;
using UnityEngine;

/// <summary>
/// 监听左右方向键，抛出滑卡事件（MVP 不使用鼠标拖拽）。
/// </summary>
public class SwipeInput : MonoBehaviour
{
    [Header("System References")]
    [Tooltip("Used to gate input to Playing / HotStreak states.")]
    [SerializeField] RunController runController;

    bool inputEnabled = true;

    public event Action<SwipeDirection> OnSwipe;

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    void Update()
    {
        if (!inputEnabled)
            return;

        if (runController != null && !IsSwipeAllowedState(runController.CurrentState))
            return;

        // 用 else if：避免同一帧左右键同时判定为 GetKeyDown 时，
        // 一帧内误触发两次滑动（玩家只做了一次操作，却被判成两张卡）。
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            OnSwipe?.Invoke(SwipeDirection.Left);
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            OnSwipe?.Invoke(SwipeDirection.Right);
    }

    static bool IsSwipeAllowedState(GameState state)
    {
        return state == GameState.Playing || state == GameState.HotStreak;
    }
}
