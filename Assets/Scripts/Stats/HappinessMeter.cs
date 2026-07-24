using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 快乐值 0–100；满 100 触发胜利条件。
/// </summary>
public class HappinessMeter : MonoBehaviour
{
    public const float MaxValue = 100f;

    [Header("UI References")]
    [Tooltip("Filled Image showing happiness progress (0–1 fillAmount).")]
    [SerializeField] Image fillImage;

    float value;
    bool hasReachedMax;

    public float Value => value;

    public event Action<float> OnValueChanged;
    public event Action OnReachedMax;

    public void Reset()
    {
        hasReachedMax = false;
        SetValue(0f, forceNotify: true);
    }

    public void Add(float amount)
    {
        if (amount <= 0f || hasReachedMax)
            return;

        SetValue(value + amount);
    }

    public void Subtract(float amount)
    {
        if (amount <= 0f || hasReachedMax)
            return;

        SetValue(value - amount);
    }

    void SetValue(float newValue, bool forceNotify = false)
    {
        float clamped = Mathf.Clamp(newValue, 0f, MaxValue);
        if (!forceNotify && Mathf.Approximately(clamped, value))
            return;

        value = clamped;
        UpdateFill();
        OnValueChanged?.Invoke(value);

        if (!hasReachedMax && value >= MaxValue)
        {
            hasReachedMax = true;
            OnReachedMax?.Invoke();
        }
    }

    void UpdateFill()
    {
        if (fillImage != null)
            fillImage.fillAmount = value / MaxValue;
    }
}
