using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内教程 overlay：首次进入用底部「开始」→ 321 倒计时 → 正式开局；
/// 局内复查时用顶部「退出」关闭并恢复玩法。
/// 须挂在常驻物体（如 Controller），勿挂在 TutorialPage 本体上。
/// </summary>
public class TutorialPageView : MonoBehaviour
{
    const string LabelExit = "退出";

    [Header("System References")]
    [SerializeField] RunController _runController;
    [SerializeField] AudioManager _audioManager;
    [SerializeField] GameAudioConfig _audioConfig;

    [Header("UI References")]
    [Tooltip("教程页根物体（仅切换显隐，脚本不要挂在此物体上）。")]
    [SerializeField] GameObject _pageRoot;
    [Tooltip("顶部退出按钮；仅局内复查教程时显示。")]
    [SerializeField] Button _exitButton;
    [SerializeField] TMP_Text _exitButtonLabel;
    [Tooltip("底部开始按钮；仅首次进入 Scene、尚未开局时显示。")]
    [SerializeField] Button _startGameButton;
    [Tooltip("321 倒计时根物体（可为 CountdownNum 自身）。")]
    [SerializeField] GameObject _countdownRoot;
    [SerializeField] TMP_Text _countdownText;

    [Header("Timing")]
    [SerializeField] float _countdownStepDuration = 1f;

    [Header("Countdown Juice")]
    [SerializeField] float _countdownShakeDuration = 0.35f;
    [SerializeField] float _countdownShakeStrength = 18f;

    bool _runStarted;
    bool _countdownRunning;
    bool _openedForReview;
    RectTransform _countdownRect;
    Vector2 _countdownRestAnchoredPosition;

    void Awake()
    {
        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_audioManager == null)
            _audioManager = FindObjectOfType<AudioManager>();

        ResolvePageRoot();
        ResolveButtons();
        BindButtons();

        if (_exitButtonLabel != null)
            _exitButtonLabel.text = LabelExit;

        if (_countdownRoot != null)
            _countdownRoot.SetActive(false);

        if (_countdownText != null)
            _countdownRect = _countdownText.rectTransform;

        SetExitButtonVisible(false);
        SetStartGameButtonVisible(true);
    }

    void OnDestroy()
    {
        UnbindButtons();
        _countdownRect?.DOKill();
    }

    void Start()
    {
        ShowIntro();
    }

    void ResolvePageRoot()
    {
        if (_pageRoot != null)
            return;

        GameObject page = GameObject.Find("TutorialPage");
        if (page != null)
            _pageRoot = page;
    }

    void ResolveButtons()
    {
        Transform pageTransform = _pageRoot != null ? _pageRoot.transform : null;
        if (pageTransform == null)
            return;

        if (_exitButton == null)
            _exitButton = pageTransform.Find("Back")?.GetComponent<Button>();

        if (_exitButtonLabel == null && _exitButton != null)
            _exitButtonLabel = _exitButton.GetComponentInChildren<TMP_Text>();

        if (_startGameButton == null)
            _startGameButton = pageTransform.Find("StartGame")?.GetComponent<Button>();
    }

    void BindButtons()
    {
        if (_exitButton != null)
            _exitButton.onClick.AddListener(HandleExitButton);

        if (_startGameButton != null)
            _startGameButton.onClick.AddListener(HandleStartGameButton);
    }

    void UnbindButtons()
    {
        if (_exitButton != null)
            _exitButton.onClick.RemoveListener(HandleExitButton);

        if (_startGameButton != null)
            _startGameButton.onClick.RemoveListener(HandleStartGameButton);
    }

    /// <summary>从菜单进入玩法 Scene：教程打开，仅显示底部开始。</summary>
    void ShowIntro()
    {
        _runStarted = false;
        _openedForReview = false;
        SetPageVisible(true);
        SetExitButtonVisible(false);
        SetStartGameButtonVisible(true);
    }

    /// <summary>局内打开教程（可由 HUD「教程」按钮调用）：仅显示顶部退出。</summary>
    public void ShowForReview()
    {
        if (_countdownRunning)
            return;

        if (_runController == null || !_runStarted)
            return;

        GameState state = _runController.CurrentState;
        if (state == GameState.Win || state == GameState.Lose || state == GameState.Intro)
            return;

        _openedForReview = true;
        _runController.PauseForTutorial();
        SetPageVisible(true);
        SetExitButtonVisible(true);
        SetStartGameButtonVisible(false);
    }

    void HandleExitButton()
    {
        if (_countdownRunning || !_openedForReview)
            return;

        CloseReview();
    }

    void HandleStartGameButton()
    {
        if (_countdownRunning || _runStarted)
            return;

        BeginCountdown();
    }

    void CloseReview()
    {
        SetPageVisible(false);
        SetExitButtonVisible(false);
        _openedForReview = false;
        _runController?.ResumeFromTutorial();
    }

    void BeginCountdown()
    {
        if (_countdownRunning)
            return;

        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        _countdownRunning = true;
        SetPageVisible(false);
        SetExitButtonVisible(false);
        SetStartGameButtonVisible(false);

        if (_countdownRoot != null)
            _countdownRoot.SetActive(true);

        CacheCountdownRestPosition();

        for (int i = 3; i >= 1; i--)
        {
            if (_countdownText != null)
                _countdownText.text = i.ToString();

            PlayCountdownSfx(i);
            PlayCountdownShake();
            yield return new WaitForSeconds(_countdownStepDuration);
        }

        StopCountdownShake();

        if (_countdownRoot != null)
            _countdownRoot.SetActive(false);

        _countdownRunning = false;
        _runStarted = true;
        _runController?.BeginRun();
    }

    void CacheCountdownRestPosition()
    {
        if (_countdownRect == null)
            return;

        _countdownRestAnchoredPosition = _countdownRect.anchoredPosition;
    }

    void PlayCountdownSfx(int step)
    {
        if (_audioManager == null || _audioConfig == null)
            return;

        GameAudioId id = step == 1 ? GameAudioId.CountdownStep1 : GameAudioId.CountdownStep32;
        AudioClip clip = _audioConfig.GetClip(id);
        if (clip == null)
            return;

        _audioManager.PlaySfx(clip, _audioConfig.GetVolume(id));
    }

    void PlayCountdownShake()
    {
        if (_countdownRect == null)
            return;

        _countdownRect.DOKill();
        _countdownRect.anchoredPosition = _countdownRestAnchoredPosition;
        _countdownRect.DOShakeAnchorPos(
            _countdownShakeDuration,
            _countdownShakeStrength,
            vibrato: 50,
            randomness: 90f,
            fadeOut: true);
    }

    void StopCountdownShake()
    {
        if (_countdownRect == null)
            return;

        _countdownRect.DOKill();
        _countdownRect.anchoredPosition = _countdownRestAnchoredPosition;
    }

    void SetPageVisible(bool visible)
    {
        if (_pageRoot != null)
            _pageRoot.SetActive(visible);
    }

    void SetExitButtonVisible(bool visible)
    {
        if (_exitButton != null)
            _exitButton.gameObject.SetActive(visible);
    }

    void SetStartGameButtonVisible(bool visible)
    {
        if (_startGameButton != null)
            _startGameButton.gameObject.SetActive(visible);
    }
}
