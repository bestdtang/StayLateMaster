using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 数值条进入最后 15%（默认 ≥85）时在基础色与高光色之间持续闪烁；掉出区间则停止。
/// </summary>
public class StatBarBlinkView : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Image _targetImage;
    [SerializeField] HappinessMeter _happinessMeter;
    [SerializeField] FatigueMeter _fatigueMeter;

    [Header("Blink")]
    [Tooltip("达到此值（含）开始闪烁；100 尺度的最后 15% = 85。")]
    [SerializeField] float _criticalThreshold = 85f;
    [SerializeField] float _blinkHalfDuration = 0.22f;
    [SerializeField] Color _highlightColor = Color.white;

    Color _baseColor;
    Tweener _blinkTween;

    void Awake()
    {
        if (_targetImage != null)
            _baseColor = _targetImage.color;
    }

    void OnEnable()
    {
        if (_happinessMeter != null)
        {
            _happinessMeter.OnValueChanged += HandleValueChanged;
            HandleValueChanged(_happinessMeter.Value);
        }

        if (_fatigueMeter != null)
        {
            _fatigueMeter.OnValueChanged += HandleValueChanged;
            HandleValueChanged(_fatigueMeter.Value);
        }
    }

    void OnDisable()
    {
        if (_happinessMeter != null)
            _happinessMeter.OnValueChanged -= HandleValueChanged;

        if (_fatigueMeter != null)
            _fatigueMeter.OnValueChanged -= HandleValueChanged;

        StopBlink();
    }

    void OnDestroy()
    {
        StopBlink();
    }

    void HandleValueChanged(float value)
    {
        if (value >= _criticalThreshold)
            StartBlink();
        else
            StopBlink();
    }

    void StartBlink()
    {
        if (_targetImage == null)
            return;

        if (_blinkTween != null && _blinkTween.IsActive())
            return;

        StopBlink();
        _targetImage.color = _baseColor;
        _blinkTween = _targetImage
            .DOColor(_highlightColor, _blinkHalfDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void StopBlink()
    {
        if (_blinkTween != null)
        {
            _blinkTween.Kill();
            _blinkTween = null;
        }

        if (_targetImage != null)
            _targetImage.color = _baseColor;
    }
}
