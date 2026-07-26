using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 兴趣气泡：显示当前 Topic 图标；切换前约 2 秒小气泡长大预告，切换时主气泡抖动并收起小气泡。
/// </summary>
public class InterestBubbleView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Icon image inside the bubble (BubbleImage).")]
    [SerializeField] Image _iconImage;
    [Tooltip("Small preview bubble that grows before an interest switch (BubbleSmall).")]
    [SerializeField] RectTransform _previewBubble;

    [Header("Topic Icons")]
    [Tooltip("Icon for Pets interest.")]
    [SerializeField] Sprite _petsIcon;
    [Tooltip("Icon for People interest.")]
    [SerializeField] Sprite _peopleIcon;
    [Tooltip("Icon for Foods interest.")]
    [SerializeField] Sprite _foodsIcon;
    [Tooltip("Icon for Cars interest.")]
    [SerializeField] Sprite _carsIcon;
    [Tooltip("Icon for Football interest.")]
    [SerializeField] Sprite _footballIcon;

    [Header("Preview Grow")]
    [Tooltip("Seconds for the first Duang pop (0 → overshoot).")]
    [SerializeField] float _previewPopDuration = 0.22f;
    [Tooltip("Seconds for each settle bounce after the first pop.")]
    [SerializeField] float _previewBounceDuration = 0.12f;
    [Tooltip("First Duang peak scale relative to rest (e.g. 1.45).")]
    [SerializeField] float _previewFirstOvershoot = 1.45f;
    [Tooltip("Dip scale between the two Duangs relative to rest.")]
    [SerializeField] float _previewBounceDip = 0.82f;
    [Tooltip("Second Duang peak scale relative to rest (e.g. 1.2).")]
    [SerializeField] float _previewSecondOvershoot = 1.2f;

    [Header("Switch Shake")]
    [Tooltip("Seconds for BubbleImage position shake on interest switch.")]
    [SerializeField] float _switchShakeDuration = 0.35f;
    [Tooltip("AnchorPos shake strength on interest switch.")]
    [SerializeField] float _switchShakeStrength = 14f;
    [SerializeField] int _switchShakeVibrato = 22;

    [Header("System References")]
    [Tooltip("Source of current interest and preview events.")]
    [SerializeField] InterestManager _interestManager;

    RectTransform _bubbleImageRect;
    Vector2 _bubbleImageRestPos;
    Vector3 _previewRestScale;
    bool _interestInitialized;
    Tween _previewGrowTween;
    Tween _switchShakeTween;

    void Awake()
    {
        if (_iconImage != null)
        {
            _bubbleImageRect = _iconImage.rectTransform;
            _bubbleImageRestPos = _bubbleImageRect.anchoredPosition;
        }

        if (_previewBubble != null)
            _previewRestScale = _previewBubble.localScale;

        HidePreviewBubbleImmediate();
    }

    void OnEnable()
    {
        // 倒计时结束后气泡重新显示：下一帧 ResetAndStart 的首次切换不抖。
        _interestInitialized = false;

        if (_interestManager == null)
            _interestManager = FindObjectOfType<InterestManager>();

        if (_interestManager == null)
            return;

        _interestManager.OnInterestChanged += HandleInterestChanged;
        _interestManager.OnSwitchPreview += HandleSwitchPreview;
        ApplyIcon(_interestManager.CurrentTopic);
        HidePreviewBubbleImmediate();
    }

    void OnDisable()
    {
        if (_interestManager != null)
        {
            _interestManager.OnInterestChanged -= HandleInterestChanged;
            _interestManager.OnSwitchPreview -= HandleSwitchPreview;
        }

        KillTweens(restoreRest: true);
        HidePreviewBubbleImmediate();
    }

    void HandleInterestChanged(TopicId topic)
    {
        ApplyIcon(topic);
        HidePreviewBubbleImmediate();

        if (!_interestInitialized)
        {
            _interestInitialized = true;
            return;
        }

        PlaySwitchShake();
    }

    void HandleSwitchPreview(TopicId upcomingTopic)
    {
        PlayPreviewGrow();
    }

    void PlayPreviewGrow()
    {
        if (_previewBubble == null)
            return;

        KillPreviewGrow();
        _previewBubble.gameObject.SetActive(true);
        _previewBubble.localScale = Vector3.zero;

        Vector3 firstPeak = _previewRestScale * _previewFirstOvershoot;
        Vector3 dip = _previewRestScale * _previewBounceDip;
        Vector3 secondPeak = _previewRestScale * _previewSecondOvershoot;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_previewBubble.DOScale(firstPeak, _previewPopDuration).SetEase(Ease.OutBack));
        sequence.Append(_previewBubble.DOScale(dip, _previewBounceDuration).SetEase(Ease.InSine));
        sequence.Append(_previewBubble.DOScale(secondPeak, _previewBounceDuration).SetEase(Ease.OutBack));
        sequence.Append(_previewBubble.DOScale(_previewRestScale, _previewBounceDuration).SetEase(Ease.OutSine));
        sequence.SetUpdate(true);
        sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _previewGrowTween = sequence;
    }

    void HidePreviewBubbleImmediate()
    {
        KillPreviewGrow();
        if (_previewBubble == null)
            return;

        _previewBubble.localScale = Vector3.zero;
        _previewBubble.gameObject.SetActive(false);
    }

    void PlaySwitchShake()
    {
        if (_bubbleImageRect == null || _switchShakeDuration <= 0f || _switchShakeStrength <= 0f)
            return;

        KillSwitchShake();
        _bubbleImageRect.anchoredPosition = _bubbleImageRestPos;
        _switchShakeTween = _bubbleImageRect
            .DOShakeAnchorPos(
                _switchShakeDuration,
                _switchShakeStrength,
                vibrato: _switchShakeVibrato,
                randomness: 90f,
                fadeOut: true)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() =>
            {
                if (_bubbleImageRect != null)
                    _bubbleImageRect.anchoredPosition = _bubbleImageRestPos;
            });
    }

    void ApplyIcon(TopicId topic)
    {
        if (_iconImage == null)
            return;

        Sprite icon = ResolveIcon(topic);
        _iconImage.sprite = icon;
        _iconImage.enabled = icon != null;
    }

    Sprite ResolveIcon(TopicId topic)
    {
        switch (topic)
        {
            case TopicId.Pets: return _petsIcon;
            case TopicId.People: return _peopleIcon;
            case TopicId.Foods: return _foodsIcon;
            case TopicId.Cars: return _carsIcon;
            case TopicId.Football: return _footballIcon;
            default: return null;
        }
    }

    void KillTweens(bool restoreRest)
    {
        KillPreviewGrow();
        KillSwitchShake();

        if (!restoreRest)
            return;

        if (_bubbleImageRect != null)
            _bubbleImageRect.anchoredPosition = _bubbleImageRestPos;
    }

    void KillPreviewGrow()
    {
        if (_previewGrowTween == null)
            return;

        _previewGrowTween.Kill();
        _previewGrowTween = null;
    }

    void KillSwitchShake()
    {
        if (_switchShakeTween == null)
            return;

        _switchShakeTween.Kill();
        _switchShakeTween = null;
    }
}
