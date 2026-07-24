using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 兴趣气泡：仅显示当前 Topic 图标。预告/切换动画留待 Phase 7 polish。
/// </summary>
public class InterestBubbleView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Icon image inside the bubble.")]
    [SerializeField] Image _iconImage;

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

    [Header("System References")]
    [Tooltip("Source of current interest and preview events.")]
    [SerializeField] InterestManager _interestManager;

    void OnEnable()
    {
        if (_interestManager == null)
            _interestManager = FindObjectOfType<InterestManager>();

        if (_interestManager == null)
            return;

        _interestManager.OnInterestChanged += HandleInterestChanged;
        _interestManager.OnSwitchPreview += HandleSwitchPreview;
        HandleInterestChanged(_interestManager.CurrentTopic);
    }

    void OnDisable()
    {
        if (_interestManager == null)
            return;

        _interestManager.OnInterestChanged -= HandleInterestChanged;
        _interestManager.OnSwitchPreview -= HandleSwitchPreview;
    }

    void HandleInterestChanged(TopicId topic)
    {
        ApplyIcon(topic);
    }

    // TODO Phase 7：预告抖动/闪烁、切换 scale pop
    void HandleSwitchPreview(TopicId upcomingTopic) { }

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
}
