using UnityEngine;

/// <summary>
/// 监听滑卡输入，驱动手指 Animator 播放左右滑动画。
/// </summary>
public class FingerSwipeAnimator : MonoBehaviour
{
    static readonly int LeftTrigger = Animator.StringToHash("Left");
    static readonly int RightTrigger = Animator.StringToHash("Right");

    [Header("System References")]
    [SerializeField] SwipeInput _swipeInput;

    [Header("Animation")]
    [SerializeField] Animator _animator;

    void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_swipeInput == null)
            _swipeInput = FindObjectOfType<SwipeInput>();
    }

    void OnEnable()
    {
        if (_swipeInput == null)
            return;

        _swipeInput.OnSwipe += HandleSwipe;
    }

    void OnDisable()
    {
        if (_swipeInput == null)
            return;

        _swipeInput.OnSwipe -= HandleSwipe;
    }

    void HandleSwipe(SwipeDirection direction)
    {
        if (_animator == null)
            return;

        _animator.SetTrigger(direction == SwipeDirection.Left ? LeftTrigger : RightTrigger);
    }
}
