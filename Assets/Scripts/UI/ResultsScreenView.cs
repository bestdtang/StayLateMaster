using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 胜/负全屏结算：响应 RunController 胜负事件；重开走 321 倒计时或回主菜单。
/// </summary>
public class ResultsScreenView : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] string _menuSceneName = GameScenes.StartMenu;

    [Header("UI References")]
    [SerializeField] GameObject _winRoot;
    [SerializeField] GameObject _loseRoot;
    [SerializeField] Button _winRestartButton;
    [SerializeField] Button _winExitButton;
    [SerializeField] Button _loseRestartButton;
    [SerializeField] Button _loseExitButton;
    [Tooltip("失败页展示当下快乐值；未绑定时跳过。")]
    [SerializeField] TMP_Text _loseHappinessText;

    [Header("Show Delay")]
    [Tooltip("胜负判定到弹出结算页之间的延迟（秒）；留时间看清快乐条冲满 / 疲劳条见底。")]
    [SerializeField] float _showDelay = 0.5f;

    [Header("System References")]
    [SerializeField] RunController _runController;
    [SerializeField] HappinessMeter _happinessMeter;
    [SerializeField] TutorialPageView _tutorialPageView;

    Coroutine _showRoutine;
    GameObject _pendingRoot;

    void Awake()
    {
        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_happinessMeter == null)
            _happinessMeter = FindObjectOfType<HappinessMeter>();

        if (_tutorialPageView == null)
            _tutorialPageView = FindObjectOfType<TutorialPageView>();

        ResolveRoots();
        ResolveButtons();
        HideAll();
    }

    void OnEnable()
    {
        if (_runController != null)
        {
            _runController.OnGameWon += HandleGameWon;
            _runController.OnGameLost += HandleGameLost;
            _runController.OnStateChanged += HandleStateChanged;
        }

        BindButtons();
    }

    void OnDisable()
    {
        if (_runController != null)
        {
            _runController.OnGameWon -= HandleGameWon;
            _runController.OnGameLost -= HandleGameLost;
            _runController.OnStateChanged -= HandleStateChanged;
        }

        UnbindButtons();
        CancelPendingShow();
    }

    void ResolveRoots()
    {
        if (_winRoot == null)
            _winRoot = GameObject.Find("Win");

        if (_loseRoot == null)
            _loseRoot = GameObject.Find("Lose");
    }

    void ResolveButtons()
    {
        if (_winRoot != null)
        {
            if (_winRestartButton == null)
                _winRestartButton = _winRoot.transform.Find("Restart")?.GetComponent<Button>();
            if (_winExitButton == null)
                _winExitButton = _winRoot.transform.Find("Exit")?.GetComponent<Button>();
        }

        if (_loseRoot != null)
        {
            if (_loseRestartButton == null)
                _loseRestartButton = _loseRoot.transform.Find("Restart")?.GetComponent<Button>();
            if (_loseExitButton == null)
                _loseExitButton = _loseRoot.transform.Find("Exit")?.GetComponent<Button>();

            if (_loseHappinessText == null)
            {
                Transform happy = _loseRoot.transform.Find("HappinessValue");
                if (happy == null)
                    happy = _loseRoot.transform.Find("HappyNum");
                if (happy != null)
                    _loseHappinessText = happy.GetComponent<TMP_Text>();
            }
        }
    }

    void BindButtons()
    {
        if (_winRestartButton != null)
            _winRestartButton.onClick.AddListener(HandleRestart);
        if (_winExitButton != null)
            _winExitButton.onClick.AddListener(HandleReturnToMenu);
        if (_loseRestartButton != null)
            _loseRestartButton.onClick.AddListener(HandleRestart);
        if (_loseExitButton != null)
            _loseExitButton.onClick.AddListener(HandleReturnToMenu);
    }

    void UnbindButtons()
    {
        if (_winRestartButton != null)
            _winRestartButton.onClick.RemoveListener(HandleRestart);
        if (_winExitButton != null)
            _winExitButton.onClick.RemoveListener(HandleReturnToMenu);
        if (_loseRestartButton != null)
            _loseRestartButton.onClick.RemoveListener(HandleRestart);
        if (_loseExitButton != null)
            _loseExitButton.onClick.RemoveListener(HandleReturnToMenu);
    }

    void HandleStateChanged(GameState state)
    {
        if (state == GameState.Win)
            HandleGameWon();
        else if (state == GameState.Lose)
            HandleGameLost();
    }

    void HandleGameWon()
    {
        ScheduleShow(_winRoot, _loseRoot);
    }

    void HandleGameLost()
    {
        RefreshLoseHappiness();
        ScheduleShow(_loseRoot, _winRoot);
    }

    /// <summary>
    /// 延迟弹结算页。OnGameWon/OnGameLost 与 OnStateChanged 会各触发一次，这里做去重。
    /// </summary>
    void ScheduleShow(GameObject rootToShow, GameObject rootToHide)
    {
        if (rootToHide != null)
            rootToHide.SetActive(false);

        if (rootToShow == null || rootToShow == _pendingRoot || rootToShow.activeSelf)
            return;

        _pendingRoot = rootToShow;

        if (_showDelay <= 0f || !isActiveAndEnabled)
        {
            ShowPendingRoot();
            return;
        }

        _showRoutine = StartCoroutine(ShowPendingRootAfterDelay());
    }

    IEnumerator ShowPendingRootAfterDelay()
    {
        yield return new WaitForSeconds(_showDelay);

        _showRoutine = null;
        ShowPendingRoot();
    }

    void ShowPendingRoot()
    {
        if (_pendingRoot != null)
            _pendingRoot.SetActive(true);

        _pendingRoot = null;
    }

    void CancelPendingShow()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        _pendingRoot = null;
    }

    void RefreshLoseHappiness()
    {
        if (_loseHappinessText == null)
            return;

        float happiness = _happinessMeter != null ? _happinessMeter.Value : 0f;
        _loseHappinessText.text = Mathf.RoundToInt(happiness).ToString();
    }

    void HandleRestart()
    {
        HideAll();

        if (_tutorialPageView != null)
        {
            _tutorialPageView.BeginRestartCountdown();
            return;
        }

        // 无倒计时组件时回退：直接再开一局
        _runController?.PrepareRestartCountdown();
        _runController?.BeginRun();
    }

    void HandleReturnToMenu()
    {
        if (string.IsNullOrEmpty(_menuSceneName))
        {
            Debug.LogError("[ResultsScreenView] 未配置菜单 Scene 名。");
            return;
        }

        SceneManager.LoadScene(_menuSceneName);
    }

    void HideAll()
    {
        CancelPendingShow();

        if (_winRoot != null)
            _winRoot.SetActive(false);

        if (_loseRoot != null)
            _loseRoot.SetActive(false);
    }
}
