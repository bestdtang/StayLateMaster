using UnityEngine;

/// <summary>
/// Hot Time / Crazy Fatigue 期间切换 Bed、Character、RightMask 下对应视觉子物体。
/// </summary>
public class HotCrazyVisualLayers : MonoBehaviour
{
    [Header("Hot Layers")]
    [SerializeField] GameObject[] _hotLayers;

    [Header("Crazy Layers")]
    [SerializeField] GameObject[] _crazyLayers;

    [Header("System References")]
    [SerializeField] HotTimeController _hotTimeController;
    [SerializeField] CrazyFatigueController _crazyFatigueController;
    [SerializeField] RunController _runController;

    void Awake()
    {
        if (_hotTimeController == null)
            _hotTimeController = FindObjectOfType<HotTimeController>();

        if (_crazyFatigueController == null)
            _crazyFatigueController = FindObjectOfType<CrazyFatigueController>();

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        SetLayersActive(_hotLayers, false);
        SetLayersActive(_crazyLayers, false);
    }

    void OnEnable()
    {
        if (_hotTimeController != null)
        {
            _hotTimeController.OnHotTimeStarted += HandleHotTimeStarted;
            _hotTimeController.OnHotTimeEnded += HandleHotTimeEnded;
        }

        if (_crazyFatigueController != null)
        {
            _crazyFatigueController.OnEntered += HandleCrazyEntered;
            _crazyFatigueController.OnExited += HandleCrazyExited;
        }

        if (_runController != null)
            _runController.OnStateChanged += HandleRunStateChanged;
    }

    void OnDisable()
    {
        if (_hotTimeController != null)
        {
            _hotTimeController.OnHotTimeStarted -= HandleHotTimeStarted;
            _hotTimeController.OnHotTimeEnded -= HandleHotTimeEnded;
        }

        if (_crazyFatigueController != null)
        {
            _crazyFatigueController.OnEntered -= HandleCrazyEntered;
            _crazyFatigueController.OnExited -= HandleCrazyExited;
        }

        if (_runController != null)
            _runController.OnStateChanged -= HandleRunStateChanged;

        SetLayersActive(_hotLayers, false);
        SetLayersActive(_crazyLayers, false);
    }

    void HandleHotTimeStarted()
    {
        SetLayersActive(_hotLayers, true);
    }

    void HandleHotTimeEnded()
    {
        SetLayersActive(_hotLayers, false);
    }

    void HandleCrazyEntered()
    {
        SetLayersActive(_crazyLayers, true);
    }

    void HandleCrazyExited()
    {
        SetLayersActive(_crazyLayers, false);
    }

    void HandleRunStateChanged(GameState state)
    {
        if (state == GameState.Win || state == GameState.Lose || state == GameState.Intro)
        {
            SetLayersActive(_hotLayers, false);
            SetLayersActive(_crazyLayers, false);
        }
    }

    static void SetLayersActive(GameObject[] layers, bool active)
    {
        if (layers == null)
            return;

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
                layers[i].SetActive(active);
        }
    }
}
