/// <summary>
/// 空格眨眼按键的判定结果。
/// </summary>
public enum BlinkResult
{
    /// <summary>内圈：完美时机。</summary>
    Success,

    /// <summary>黄圈（外圈）：偏早/偏晚，疲劳 ×0.85。</summary>
    EarlyLate,

    /// <summary>红圈按键，或判定区缩至 0 仍未按：完全失败。</summary>
    CompleteMiss
}
