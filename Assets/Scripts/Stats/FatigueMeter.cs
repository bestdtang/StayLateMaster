using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 疲劳增速修饰的来源；同一来源在任意时刻只保留一个（最新覆盖），不同来源之间相乘叠加。
/// </summary>
public enum FatigueModifierSource
{
    Blink,
    CrazyFatigue
}

/// <summary>
/// 疲劳值 0–100，随时间加速增长，只增不减；满 100 触发失败条件。
/// </summary>
public class FatigueMeter : MonoBehaviour
{
    public const float MaxValue = 100f;

    [Header("UI References")]
    [Tooltip("Filled Image showing fatigue progress (0–1 fillAmount).")]
    [SerializeField] Image fillImage;

    float baseRate = 2f;
    float acceleration = 0.055f;
    float maxRate;
    float lateDampenStart = 50f;
    float lateDampenEndMultiplier = 0.78f;

    float value;
    float elapsedSeconds;
    bool isRunning;
    bool hasReachedMax;
    readonly Dictionary<FatigueModifierSource, RateModifier> activeModifiers =
        new Dictionary<FatigueModifierSource, RateModifier>();
    readonly List<FatigueModifierSource> expiredScratch = new List<FatigueModifierSource>();

    struct RateModifier
    {
        public float Multiplier;
        public float ExpiresAt;
    }

    public float Value => value;
    public float ElapsedSeconds => elapsedSeconds;
    public float CurrentBaseRate => baseRate;
    public float CurrentAcceleration => acceleration;

    public event Action<float> OnValueChanged;
    public event Action OnReachedMax;

    public void Reset()
    {
        value = 0f;
        elapsedSeconds = 0f;
        isRunning = false;
        hasReachedMax = false;
        activeModifiers.Clear();
        UpdateFill();
        OnValueChanged?.Invoke(value);
    }

    public void StartRunning()
    {
        isRunning = true;
    }

    public void StopRunning()
    {
        isRunning = false;
    }

    /// <summary>
    /// 施加临时疲劳增速倍率。同一来源只保留一个（最新覆盖，避免连续失败 ×2→×4→×8 螺旋）；
    /// 不同来源之间相乘叠加。
    /// </summary>
    public void ApplyRateModifier(FatigueModifierSource source, float multiplier, float duration)
    {
        if (duration <= 0f)
            return;

        activeModifiers[source] = new RateModifier
        {
            Multiplier = multiplier,
            ExpiresAt = Time.time + duration
        };
    }

    public void ClearModifiers()
    {
        activeModifiers.Clear();
    }

    /// <summary>
    /// 从全局配置注入疲劳曲线参数。
    /// </summary>
    public void ApplyBalance(GameBalanceConfig config)
    {
        if (config == null)
            return;

        baseRate = config.FatigueBaseRate;
        acceleration = config.FatigueAcceleration;
        maxRate = config.FatigueMaxRate;
        lateDampenStart = config.FatigueLateDampenStart;
        lateDampenEndMultiplier = config.FatigueLateDampenEndMultiplier;
    }

    void Update()
    {
        if (!isRunning || hasReachedMax)
            return;

        elapsedSeconds += Time.deltaTime;
        RemoveExpiredModifiers();

        float rate = GetEffectiveRate();
        AddInternal(rate * Time.deltaTime);
    }

    float GetEffectiveRate()
    {
        float baseGrowth = GetNaturalGrowthRate();
        float modifierProduct = 1f;

        foreach (RateModifier modifier in activeModifiers.Values)
            modifierProduct *= modifier.Multiplier;

        return baseGrowth * modifierProduct;
    }

    /// <summary>
    /// 自然增速：随时间线性加速，带上限；疲劳 ≥50 后按当前值衰减，避免尾段过快填满。
    /// </summary>
    float GetNaturalGrowthRate()
    {
        float rate = baseRate + acceleration * elapsedSeconds;

        if (maxRate > 0f)
            rate = Mathf.Min(rate, maxRate);

        if (value >= lateDampenStart && lateDampenEndMultiplier < 1f)
        {
            float t = Mathf.InverseLerp(lateDampenStart, MaxValue, value);
            rate *= Mathf.Lerp(1f, lateDampenEndMultiplier, t);
        }

        return rate;
    }

    void RemoveExpiredModifiers()
    {
        float now = Time.time;
        expiredScratch.Clear();

        foreach (KeyValuePair<FatigueModifierSource, RateModifier> entry in activeModifiers)
        {
            if (entry.Value.ExpiresAt <= now)
                expiredScratch.Add(entry.Key);
        }

        for (int i = 0; i < expiredScratch.Count; i++)
            activeModifiers.Remove(expiredScratch[i]);
    }

    void AddInternal(float amount)
    {
        if (amount <= 0f || hasReachedMax)
            return;

        float clamped = Mathf.Clamp(value + amount, 0f, MaxValue);
        if (Mathf.Approximately(clamped, value))
            return;

        value = clamped;
        UpdateFill();
        OnValueChanged?.Invoke(value);

        if (!hasReachedMax && value >= MaxValue)
        {
            hasReachedMax = true;
            isRunning = false;
            OnReachedMax?.Invoke();
        }
    }

    void UpdateFill()
    {
        if (fillImage != null)
            fillImage.fillAmount = value / MaxValue;
    }
}
