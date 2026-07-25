using DG.Tweening;
using UnityEngine;

/// <summary>
/// 监听眨眼结果，对钟摆区域播放方向性抖动：成功/偏早偏晚上下抖，完全失败左右抖。
/// </summary>
public class PendulumFeedbackView : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] BlinkInput _blinkInput;
    [SerializeField] RectTransform _areaRoot;

    [Header("Success Shake (vertical)")]
    [SerializeField] float _successShakeDuration = 0.18f;
    [SerializeField] float _successShakeStrength = 10f;
    [SerializeField] int _successShakeVibrato = 20;

    [Header("Failure Shake (horizontal)")]
    [SerializeField] float _failureShakeDuration = 0.28f;
    [SerializeField] float _failureShakeStrength = 18f;
    [SerializeField] int _failureShakeVibrato = 24;

    Vector2 _restAnchoredPosition;

    void Awake()
    {
        if (_areaRoot == null)
            _areaRoot = transform as RectTransform;

        if (_blinkInput == null)
            _blinkInput = GetComponent<BlinkInput>();

        if (_areaRoot != null)
            _restAnchoredPosition = _areaRoot.anchoredPosition;
    }

    void OnEnable()
    {
        if (_blinkInput == null)
            return;

        _blinkInput.OnBlinkSuccess += PlaySuccessShake;
        _blinkInput.OnBlinkEarlyLate += PlaySuccessShake;
        _blinkInput.OnBlinkCompleteMiss += PlayFailureShake;
    }

    void OnDisable()
    {
        if (_blinkInput != null)
        {
            _blinkInput.OnBlinkSuccess -= PlaySuccessShake;
            _blinkInput.OnBlinkEarlyLate -= PlaySuccessShake;
            _blinkInput.OnBlinkCompleteMiss -= PlayFailureShake;
        }

        StopShake();
    }

    void OnDestroy()
    {
        StopShake();
    }

    void PlaySuccessShake()
    {
        if (_areaRoot == null || _successShakeDuration <= 0f || _successShakeStrength <= 0f)
            return;

        StopShake();
        _areaRoot.DOShakeAnchorPos(
                _successShakeDuration,
                new Vector2(0f, _successShakeStrength),
                vibrato: _successShakeVibrato,
                randomness: 0f,
                fadeOut: true)
            .OnComplete(ResetPosition);
    }

    void PlayFailureShake()
    {
        if (_areaRoot == null || _failureShakeDuration <= 0f || _failureShakeStrength <= 0f)
            return;

        StopShake();
        _areaRoot.DOShakeAnchorPos(
                _failureShakeDuration,
                new Vector2(_failureShakeStrength, 0f),
                vibrato: _failureShakeVibrato,
                randomness: 0f,
                fadeOut: true)
            .OnComplete(ResetPosition);
    }

    void StopShake()
    {
        if (_areaRoot == null)
            return;

        _areaRoot.DOKill(complete: false);
        ResetPosition();
    }

    void ResetPosition()
    {
        if (_areaRoot == null)
            return;

        _areaRoot.anchoredPosition = _restAnchoredPosition;
    }
}
