using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单张内容卡 prefab：展示 Sprite，滑出后销毁（不复用）。
/// </summary>
public class CardView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Animated card root inside the mask; defaults to this transform.")]
    [SerializeField] RectTransform _cardRoot;
    [Tooltip("Optional card border/background image.")]
    [SerializeField] Image _backgroundImage;
    [Tooltip("Main content image; auto-resolves Image on this object if empty.")]
    [SerializeField] Image _mainImage;
    [Tooltip("Optional title label (Jam: can leave empty).")]
    [SerializeField] TMP_Text _titleText;
    [Tooltip("Optional description label (Jam: can leave empty).")]
    [SerializeField] TMP_Text _descriptionText;

    [Header("Dismiss Animation")]
    [Tooltip("Total duration for a correct swipe dismiss.")]
    [SerializeField] float _correctDismissDuration = 0.32f;
    [Tooltip("Total duration for a wrong swipe dismiss.")]
    [SerializeField] float _wrongDismissDuration = 0.48f;
    [Tooltip("Outward rotation angle (degrees) at the bottom corner pivot.")]
    [SerializeField] float _dismissRotationAngle = 32f;
    [Tooltip("Vertical drop (pixels) while the card exits.")]
    [SerializeField] float _dismissDropAmount = 48f;
    [Tooltip("Fraction of correct dismiss time spent on the rotation phase.")]
    [SerializeField] float _rotationPhaseRatio = 0.38f;
    [Tooltip("Wrong dismiss: partial rotation as a fraction of full outward angle.")]
    [SerializeField] float _wrongPartialMoveRatio = 0.35f;
    [Tooltip("Horizontal slide distance (pixels) in phase 2; left swipe uses negative X.")]
    [SerializeField] float _exitSlideDistance = 520f;

    static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);
    static readonly Vector2 BottomLeftPivot = new Vector2(0f, 0f);
    static readonly Vector2 BottomRightPivot = new Vector2(1f, 0f);

    Vector2 _homeAnchoredPosition;
    Tween _activeTween;

    public PickedCard Data { get; private set; }
    public bool IsAnimating => _activeTween != null && _activeTween.IsActive();

    void Awake()
    {
        if (_cardRoot == null)
            _cardRoot = transform as RectTransform;

        if (_mainImage == null)
            _mainImage = GetComponent<Image>();

        if (_cardRoot != null)
            _homeAnchoredPosition = _cardRoot.anchoredPosition;
    }

    void OnDestroy()
    {
        KillActiveTween();
    }

    public void Bind(PickedCard card)
    {
        KillActiveTween();
        Data = card;
        ResetVisualState();

        if (_titleText != null)
            _titleText.text = string.Empty;

        if (_descriptionText != null)
            _descriptionText.text = string.Empty;

        if (_mainImage != null && card.Sprite != null)
        {
            _mainImage.sprite = card.Sprite;
            _mainImage.color = Color.white;
            _mainImage.enabled = true;
        }
    }

    public void PlayDismiss(SwipeDirection direction, bool wasCorrect, Action onComplete)
    {
        if (_cardRoot == null)
        {
            onComplete?.Invoke();
            return;
        }

        KillActiveTween();

        if (wasCorrect)
            PlayCorrectDismiss(direction, onComplete);
        else
            PlayWrongDismiss(direction, onComplete);
    }

    void PlayCorrectDismiss(SwipeDirection direction, Action onComplete)
    {
        ApplyDismissPivot(direction);

        float signedAngle = GetOutwardRotationAngle(direction);
        Vector2 pivotPos = _cardRoot.anchoredPosition;
        Vector2 rotatePos = pivotPos + new Vector2(0f, -_dismissDropAmount * 0.35f);
        Vector2 exitPos = pivotPos + GetExitAnchoredOffset(direction);

        float rotateDuration = _correctDismissDuration * _rotationPhaseRatio;
        float slideDuration = _correctDismissDuration - rotateDuration;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_cardRoot
            .DORotate(new Vector3(0f, 0f, signedAngle), rotateDuration)
            .SetEase(Ease.OutQuad));
        sequence.Join(_cardRoot
            .DOAnchorPos(rotatePos, rotateDuration)
            .SetEase(Ease.OutQuad));
        sequence.Append(_cardRoot
            .DOAnchorPos(exitPos, slideDuration)
            .SetEase(Ease.InCubic));
        sequence.OnComplete(() => FinishDismiss(onComplete));
        _activeTween = sequence;
    }

    void PlayWrongDismiss(SwipeDirection direction, Action onComplete)
    {
        ApplyDismissPivot(direction);

        float signedAngle = GetOutwardRotationAngle(direction);
        float partialAngle = signedAngle * _wrongPartialMoveRatio;
        Vector2 pivotPos = _cardRoot.anchoredPosition;
        Vector2 partialPos = pivotPos + new Vector2(0f, -_dismissDropAmount * 0.15f);
        Vector2 exitPos = pivotPos + GetExitAnchoredOffset(direction);

        float partialDuration = _wrongDismissDuration * 0.22f;
        float reboundDuration = _wrongDismissDuration * 0.28f;
        float rotateDuration = _wrongDismissDuration * 0.22f;
        float slideDuration = _wrongDismissDuration - partialDuration - reboundDuration - rotateDuration;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_cardRoot
            .DORotate(new Vector3(0f, 0f, partialAngle), partialDuration)
            .SetEase(Ease.OutQuad));
        sequence.Join(_cardRoot
            .DOAnchorPos(partialPos, partialDuration)
            .SetEase(Ease.OutQuad));
        sequence.Append(_cardRoot
            .DOPunchRotation(new Vector3(0f, 0f, -partialAngle * 0.35f), reboundDuration, 8, 0.55f));
        sequence.Append(_cardRoot
            .DORotate(new Vector3(0f, 0f, signedAngle), rotateDuration)
            .SetEase(Ease.OutQuad));
        sequence.Append(_cardRoot
            .DOAnchorPos(exitPos, slideDuration)
            .SetEase(Ease.InCubic));
        sequence.OnComplete(() => FinishDismiss(onComplete));
        _activeTween = sequence;
    }

    void ApplyDismissPivot(SwipeDirection direction)
    {
        Vector2 pivot = direction == SwipeDirection.Right ? BottomRightPivot : BottomLeftPivot;
        SetPivotKeepPosition(pivot);
    }

    /// <summary>
    /// 底角为 pivot 时，让卡片顶部朝滑动方向外侧甩出。
    /// </summary>
    float GetOutwardRotationAngle(SwipeDirection direction)
    {
        return direction == SwipeDirection.Right ? -_dismissRotationAngle : _dismissRotationAngle;
    }

    /// <summary>
    /// 平移量：保证整张卡完全离开 Mask 可视区域。
    /// </summary>
    Vector2 GetExitAnchoredOffset(SwipeDirection direction)
    {
        float horizontalTravel = Mathf.Abs(_exitSlideDistance);
        float verticalDrop = -_dismissDropAmount;

        return direction == SwipeDirection.Right
            ? new Vector2(horizontalTravel, verticalDrop)
            : new Vector2(-horizontalTravel, verticalDrop);
    }

    void ResetVisualState()
    {
        if (_cardRoot == null)
            return;

        SetPivotKeepPosition(CenterPivot);
        _cardRoot.localRotation = Quaternion.identity;
        _cardRoot.localScale = Vector3.one;
        _cardRoot.anchoredPosition = _homeAnchoredPosition;
    }

    void SetPivotKeepPosition(Vector2 newPivot)
    {
        Vector2 size = _cardRoot.rect.size;
        Vector2 deltaPivot = newPivot - _cardRoot.pivot;
        _cardRoot.pivot = newPivot;
        _cardRoot.anchoredPosition += new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
    }

    void FinishDismiss(Action onComplete)
    {
        _activeTween = null;
        onComplete?.Invoke();
    }

    void KillActiveTween()
    {
        if (_activeTween != null && _activeTween.IsActive())
            _activeTween.Kill();

        _activeTween = null;
    }
}
