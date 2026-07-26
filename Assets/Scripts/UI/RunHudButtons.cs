using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主玩法 Scene 常驻 HUD：教程（局内复查 overlay）与退出（回菜单 Scene）。
/// Intro（含开局/重开 321）、胜负全屏页、火热结束缓冲期间隐藏，避免误点。
/// </summary>
public class RunHudButtons : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] string _menuSceneName = GameScenes.StartMenu;

    [Header("UI References")]
    [SerializeField] Button _exitButton;
    [SerializeField] Button _tutorialButton;
    [SerializeField] TutorialPageView _tutorialPageView;

    [Header("System References")]
    [SerializeField] RunController _runController;
    [SerializeField] HotTimeController _hotTimeController;

    bool _wasPostHotBuffer;

    void Awake()
    {
        if (_exitButton == null)
            _exitButton = FindButtonByName("Exit");

        if (_tutorialButton == null)
            _tutorialButton = FindButtonByName("Tutorial");

        if (_tutorialPageView == null)
            _tutorialPageView = FindObjectOfType<TutorialPageView>();

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_hotTimeController == null)
            _hotTimeController = FindObjectOfType<HotTimeController>();
    }

    void OnEnable()
    {
        if (_exitButton != null)
            _exitButton.onClick.AddListener(ReturnToMenu);

        if (_tutorialButton != null)
            _tutorialButton.onClick.AddListener(OpenTutorial);

        if (_runController != null)
            _runController.OnStateChanged += HandleStateChanged;

        RefreshHudButtonVisibility();
    }

    void OnDisable()
    {
        if (_exitButton != null)
            _exitButton.onClick.RemoveListener(ReturnToMenu);

        if (_tutorialButton != null)
            _tutorialButton.onClick.RemoveListener(OpenTutorial);

        if (_runController != null)
            _runController.OnStateChanged -= HandleStateChanged;
    }

    void Update()
    {
        bool postHot = _hotTimeController != null && _hotTimeController.IsPostHotBufferActive;
        if (postHot == _wasPostHotBuffer)
            return;

        _wasPostHotBuffer = postHot;
        RefreshHudButtonVisibility();
    }

    void HandleStateChanged(GameState state)
    {
        RefreshHudButtonVisibility();
    }

    void RefreshHudButtonVisibility()
    {
        SetHudButtonsVisible(ShouldShowHudButtons());
    }

    bool ShouldShowHudButtons()
    {
        if (_runController == null)
            return true;

        GameState state = _runController.CurrentState;
        // Intro 含开局/重开 321；胜负页同样隐藏，避免点到结算层下的按钮
        if (state == GameState.Win || state == GameState.Lose || state == GameState.Intro)
            return false;

        if (_hotTimeController != null && _hotTimeController.IsPostHotBufferActive)
            return false;

        return true;
    }

    void SetHudButtonsVisible(bool visible)
    {
        if (_exitButton != null)
            _exitButton.gameObject.SetActive(visible);

        if (_tutorialButton != null)
            _tutorialButton.gameObject.SetActive(visible);
    }

    void ReturnToMenu()
    {
        if (string.IsNullOrEmpty(_menuSceneName))
        {
            Debug.LogError("[RunHudButtons] 未配置菜单 Scene 名。");
            return;
        }

        SceneManager.LoadScene(_menuSceneName);
    }

    void OpenTutorial()
    {
        _tutorialPageView?.ShowForReview();
    }

    static Button FindButtonByName(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }
}
