using TMPro;
using UnityEngine;

/// <summary>
/// 连击数 TextMeshPro 显示；格式为「当前/阈值」。连击为 0 时隐藏文字。
/// </summary>
public class ComboStreakView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("显示连击进度 TextMeshPro，格式 x/x。")]
    [SerializeField] TMP_Text _streakText;

    [Header("System References")]
    [SerializeField] ComboStreakCounter _comboStreakCounter;
    [SerializeField] RunController _runController;

    void Awake()
    {
        if (_comboStreakCounter == null)
            _comboStreakCounter = FindObjectOfType<ComboStreakCounter>();

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();
    }

    void OnEnable()
    {
        if (_comboStreakCounter == null)
            return;

        _comboStreakCounter.OnStreakChanged += HandleStreakChanged;
        HandleStreakChanged(_comboStreakCounter.CurrentStreak);
    }

    void OnDisable()
    {
        if (_comboStreakCounter == null)
            return;

        _comboStreakCounter.OnStreakChanged -= HandleStreakChanged;
    }

    void HandleStreakChanged(int streak)
    {
        if (_streakText == null)
            return;

        if (streak <= 0)
        {
            _streakText.gameObject.SetActive(false);
            return;
        }

        _streakText.gameObject.SetActive(true);
        int trigger = GetTriggerStreak();
        _streakText.text = $"{streak}/{trigger}";
    }

    int GetTriggerStreak()
    {
        GameBalanceConfig config = _runController != null ? _runController.BalanceConfig : null;
        return config != null ? Mathf.Max(1, config.HotTriggerStreak) : 5;
    }
}
