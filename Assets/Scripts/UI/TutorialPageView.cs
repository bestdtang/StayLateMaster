using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内教程 overlay：两页翻页；入场看完两页后底部「开始」→ 321 倒计时 → 正式开局；
/// 结算重开同样走 321（跳过教程）；局内复查时用顶部「退出」关闭并恢复玩法。
/// 须挂在常驻物体（如 Controller），勿挂在 TutorialPage 本体上。
/// </summary>
public class TutorialPageView : MonoBehaviour
{
    const int PageCount = 2;

    [Header("System References")]
    [SerializeField] RunController _runController;
    [SerializeField] AudioManager _audioManager;
    [SerializeField] GameAudioConfig _audioConfig;

    [Header("UI References")]
    [Tooltip("教程页根物体（仅切换显隐，脚本不要挂在此物体上）。")]
    [SerializeField] GameObject _pageRoot;
    [Tooltip("顶部退出按钮；仅局内复查教程时显示。")]
    [SerializeField] Button _exitButton;
    [Tooltip("底部开始按钮；入场看完两页后才显示。")]
    [SerializeField] Button _startGameButton;
    [Tooltip("教程内容第 1 页（Page_1）。")]
    [SerializeField] GameObject _page1Root;
    [Tooltip("教程内容第 2 页（Page_2）。")]
    [SerializeField] GameObject _page2Root;
    [Tooltip("左箭头；仅第 2 页显示。")]
    [SerializeField] Button _leftArrowButton;
    [Tooltip("右箭头；仅第 1 页显示。")]
    [SerializeField] Button _rightArrowButton;
    [Tooltip("321 倒计时根物体（可为 CountdownNum 自身）。")]
    [SerializeField] GameObject _countdownRoot;
    [SerializeField] TMP_Text _countdownText;

    [Header("Countdown Reveal")]
    [Tooltip("「3」时显示：钟摆 UI 根（如 Arc）。")]
    [SerializeField] GameObject _pendulumUiRoot;
    [Tooltip("「2」时显示：疲惫条根（TiredBar）。")]
    [SerializeField] GameObject _fatigueBarRoot;
    [Tooltip("「1」时显示：快乐条根（HappinessBar）。")]
    [SerializeField] GameObject _happinessBarRoot;
    [Tooltip("倒计时期间隐藏，开局后显示（InterestBubble）。")]
    [SerializeField] GameObject _interestBubbleRoot;
    [SerializeField] HappinessMeter _happinessMeter;
    [SerializeField] FatigueMeter _fatigueMeter;
    [SerializeField] PendulumController _pendulumController;

    [Header("Countdown Reveal Tweens")]
    [Tooltip("录自场景 DOTweenAnimation（Arc Intro1 / TiredBar Intro2 / HappinessBar Intro3）；之后可删组件，由此处驱动。")]
    [SerializeField] float _revealDuration = 0.45f;
    [SerializeField] Ease _revealEase = Ease.OutBounce;
    [Tooltip("Arc / Intro1：From relative Move。")]
    [SerializeField] Vector2 _pendulumRevealFromOffset = new Vector2(0f, 500f);
    [Tooltip("TiredBar / Intro2：From relative Move。")]
    [SerializeField] Vector2 _fatigueRevealFromOffset = new Vector2(0f, -300f);
    [Tooltip("HappinessBar / Intro3：From relative Move。")]
    [SerializeField] Vector2 _happinessRevealFromOffset = new Vector2(800f, 0f);

    [Header("Timing")]
    [SerializeField] float _countdownStepDuration = 1f;

    [Header("Countdown Juice")]
    [SerializeField] float _countdownShakeDuration = 0.35f;
    [SerializeField] float _countdownShakeStrength = 18f;

    [Header("Start Button Appear")]
    [Tooltip("看完两页后「开始」弹出时长。")]
    [SerializeField] float _startAppearDuration = 0.4f;
    [Tooltip("弹出过冲倍率（>1 更 duang）。")]
    [SerializeField] float _startAppearOvershoot = 1.18f;
    [SerializeField] Ease _startAppearEase = Ease.OutBack;

    bool _runStarted;
    bool _countdownRunning;
    bool _openedForReview;
    bool _hasViewedAllPages;
    int _currentPageIndex;
    bool _revealRestCached;
    bool _startRestScaleCached;
    RectTransform _countdownRect;
    Vector2 _countdownRestAnchoredPosition;
    RectTransform _pendulumRect;
    RectTransform _fatigueBarRect;
    RectTransform _happinessBarRect;
    RectTransform _startGameRect;
    Vector3 _startGameRestScale = Vector3.one;
    Tween _startAppearTween;
    Vector2 _pendulumRestAnchoredPosition;
    Vector2 _fatigueRestAnchoredPosition;
    Vector2 _happinessRestAnchoredPosition;

    void Awake()
    {
        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        ResolveAudioRefs();

        ResolvePageRoot();
        ResolveButtons();
        ResolveCountdownRevealTargets();
        BindButtons();

        if (_countdownRoot != null)
            _countdownRoot.SetActive(false);

        if (_countdownText != null)
            _countdownRect = _countdownText.rectTransform;

        SetExitButtonVisible(false);
        SetStartGameButtonVisible(false);
        ApplyTutorialPageUi();
    }

    void OnDestroy()
    {
        UnbindButtons();
        KillStartAppearTween();
        _countdownRect?.DOKill();
        _pendulumRect?.DOKill();
        _fatigueBarRect?.DOKill();
        _happinessBarRect?.DOKill();
    }

    void Start()
    {
        ShowIntro();
        // 直接以压低音量启 BGM + OneShot；若 RunAudio 已启过，仅更新音量不重开
        ApplyTutorialOverlayAudio();
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

        if (_startGameButton == null)
            _startGameButton = pageTransform.Find("StartGame")?.GetComponent<Button>();

        if (_page1Root == null)
            _page1Root = pageTransform.Find("Page_1")?.gameObject;

        if (_page2Root == null)
            _page2Root = pageTransform.Find("Page_2")?.gameObject;

        if (_leftArrowButton == null)
            _leftArrowButton = EnsureArrowButton(pageTransform.Find("Left"));

        if (_rightArrowButton == null)
            _rightArrowButton = EnsureArrowButton(pageTransform.Find("Right"));
    }

    static Button EnsureArrowButton(Transform arrowTransform)
    {
        if (arrowTransform == null)
            return null;

        Button button = arrowTransform.GetComponent<Button>();
        if (button == null)
            button = arrowTransform.gameObject.AddComponent<Button>();

        return button;
    }

    void BindButtons()
    {
        if (_exitButton != null)
            _exitButton.onClick.AddListener(HandleExitButton);

        if (_startGameButton != null)
            _startGameButton.onClick.AddListener(HandleStartGameButton);

        if (_leftArrowButton != null)
            _leftArrowButton.onClick.AddListener(HandleLeftArrow);

        if (_rightArrowButton != null)
            _rightArrowButton.onClick.AddListener(HandleRightArrow);
    }

    void UnbindButtons()
    {
        if (_exitButton != null)
            _exitButton.onClick.RemoveListener(HandleExitButton);

        if (_startGameButton != null)
            _startGameButton.onClick.RemoveListener(HandleStartGameButton);

        if (_leftArrowButton != null)
            _leftArrowButton.onClick.RemoveListener(HandleLeftArrow);

        if (_rightArrowButton != null)
            _rightArrowButton.onClick.RemoveListener(HandleRightArrow);
    }

    /// <summary>从菜单进入玩法 Scene：教程打开；看完两页后才显示底部开始。</summary>
    void ShowIntro()
    {
        _runStarted = false;
        _openedForReview = false;
        _hasViewedAllPages = false;
        _currentPageIndex = 0;
        SetPageVisible(true);
        SetExitButtonVisible(false);
        ApplyTutorialPageUi();
        // Start 阶段再藏 HUD，避免 Awake 里禁用导致钟摆等组件未初始化
        PrepareHudForCountdown();
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
        _currentPageIndex = 0;
        _runController.PauseForTutorial();
        SetPageVisible(true);
        SetExitButtonVisible(true);
        ApplyTutorialPageUi();
        ApplyTutorialOverlayAudio();
    }

    void HandleExitButton()
    {
        if (_countdownRunning || !_openedForReview)
            return;

        CloseReview();
    }

    void HandleStartGameButton()
    {
        if (_countdownRunning || _runStarted || !_hasViewedAllPages || _openedForReview)
            return;

        BeginCountdown();
    }

    void HandleLeftArrow()
    {
        if (_countdownRunning || _currentPageIndex <= 0)
            return;

        _currentPageIndex--;
        ApplyTutorialPageUi();
    }

    void HandleRightArrow()
    {
        if (_countdownRunning || _currentPageIndex >= PageCount - 1)
            return;

        _currentPageIndex++;
        if (_currentPageIndex >= PageCount - 1)
            _hasViewedAllPages = true;

        ApplyTutorialPageUi();
    }

    void ApplyTutorialPageUi()
    {
        if (_page1Root != null)
            _page1Root.SetActive(_currentPageIndex == 0);

        if (_page2Root != null)
            _page2Root.SetActive(_currentPageIndex == 1);

        SetArrowVisible(_leftArrowButton, _currentPageIndex == 1);
        SetArrowVisible(_rightArrowButton, _currentPageIndex == 0);

        bool showStart = !_openedForReview && _hasViewedAllPages && !_runStarted && !_countdownRunning;
        SetStartGameButtonVisible(showStart);
    }

    static void SetArrowVisible(Button arrow, bool visible)
    {
        if (arrow != null)
            arrow.gameObject.SetActive(visible);
    }

    void CloseReview()
    {
        SetPageVisible(false);
        SetExitButtonVisible(false);
        _openedForReview = false;
        RestoreMainBgmVolume();
        _runController?.ResumeFromTutorial();
    }

    void ApplyTutorialOverlayAudio()
    {
        ApplyMainBgmVolumeScale(GetIntroBgmVolumeScale());
        PlayTutorialIntroSfx();
    }

    void BeginCountdown()
    {
        if (_countdownRunning)
            return;

        StartCoroutine(CountdownRoutine());
    }

    /// <summary>结算页重开：跳过教程，直接 321 → BeginRun。</summary>
    public void BeginRestartCountdown()
    {
        if (_countdownRunning)
            return;

        _openedForReview = false;
        SetPageVisible(false);
        SetExitButtonVisible(false);
        SetStartGameButtonVisible(false);
        _runController?.PrepareRestartCountdown();
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        _countdownRunning = true;
        SetPageVisible(false);
        SetExitButtonVisible(false);
        SetStartGameButtonVisible(false);
        RestoreMainBgmVolume();
        PrepareHudForCountdown();

        if (_countdownRoot != null)
            _countdownRoot.SetActive(true);

        CacheCountdownRestPosition();

        for (int i = 3; i >= 1; i--)
        {
            if (_countdownText != null)
                _countdownText.text = i.ToString();

            RevealHudForCountdownStep(i);
            PlayCountdownSfx(i);
            PlayCountdownShake();
            yield return new WaitForSeconds(_countdownStepDuration);
        }

        StopCountdownShake();

        if (_countdownRoot != null)
            _countdownRoot.SetActive(false);

        SetInterestBubbleVisible(true);
        _countdownRunning = false;
        _runStarted = true;
        _runController?.BeginRun();
    }

    void ResolveCountdownRevealTargets()
    {
        if (_pendulumUiRoot == null)
            _pendulumUiRoot = GameObject.Find("Arc");

        if (_fatigueBarRoot == null)
            _fatigueBarRoot = GameObject.Find("TiredBar");

        if (_happinessBarRoot == null)
            _happinessBarRoot = GameObject.Find("HappinessBar");

        if (_interestBubbleRoot == null)
            _interestBubbleRoot = GameObject.Find("InterestBubble");

        if (_happinessMeter == null && _happinessBarRoot != null)
            _happinessMeter = _happinessBarRoot.GetComponent<HappinessMeter>();

        if (_fatigueMeter == null && _fatigueBarRoot != null)
            _fatigueMeter = _fatigueBarRoot.GetComponent<FatigueMeter>();

        if (_happinessMeter == null)
            _happinessMeter = FindObjectOfType<HappinessMeter>();

        if (_fatigueMeter == null)
            _fatigueMeter = FindObjectOfType<FatigueMeter>();

        if (_pendulumController == null && _pendulumUiRoot != null)
            _pendulumController = _pendulumUiRoot.GetComponent<PendulumController>();

        if (_pendulumController == null)
            _pendulumController = FindObjectOfType<PendulumController>();

        CacheRevealRestPositions();
        // 入场改由本脚本播放；场景里若仍挂着 DOTweenAnimation，关掉 autoPlay 避免 Enable 时双播
        DisableLegacyRevealAutoPlay(_pendulumUiRoot);
        DisableLegacyRevealAutoPlay(_fatigueBarRoot);
        DisableLegacyRevealAutoPlay(_happinessBarRoot);
    }

    void CacheRevealRestPositions()
    {
        if (_revealRestCached)
            return;

        _pendulumRect = _pendulumUiRoot != null
            ? _pendulumUiRoot.transform as RectTransform
            : null;
        _fatigueBarRect = _fatigueBarRoot != null
            ? _fatigueBarRoot.transform as RectTransform
            : null;
        _happinessBarRect = _happinessBarRoot != null
            ? _happinessBarRoot.transform as RectTransform
            : null;

        if (_pendulumRect != null)
            _pendulumRestAnchoredPosition = _pendulumRect.anchoredPosition;

        if (_fatigueBarRect != null)
            _fatigueRestAnchoredPosition = _fatigueBarRect.anchoredPosition;

        if (_happinessBarRect != null)
            _happinessRestAnchoredPosition = _happinessBarRect.anchoredPosition;

        _revealRestCached = _pendulumRect != null
            || _fatigueBarRect != null
            || _happinessBarRect != null;
    }

    void PrepareHudForCountdown()
    {
        CacheRevealRestPositions();
        SnapRevealRootHidden(_pendulumUiRoot, _pendulumRect, _pendulumRestAnchoredPosition);
        SnapRevealRootHidden(_fatigueBarRoot, _fatigueBarRect, _fatigueRestAnchoredPosition);
        SnapRevealRootHidden(_happinessBarRoot, _happinessBarRect, _happinessRestAnchoredPosition);
        SetInterestBubbleVisible(false);

        _happinessMeter?.Reset();
        _fatigueMeter?.Reset();
        _pendulumController?.ClearJudgmentZone();
    }

    void RevealHudForCountdownStep(int step)
    {
        switch (step)
        {
            case 3:
                PlayRevealTween(
                    _pendulumUiRoot,
                    _pendulumRect,
                    _pendulumRestAnchoredPosition,
                    _pendulumRevealFromOffset);
                _pendulumController?.ClearJudgmentZone();
                break;
            case 2:
                PlayRevealTween(
                    _fatigueBarRoot,
                    _fatigueBarRect,
                    _fatigueRestAnchoredPosition,
                    _fatigueRevealFromOffset);
                break;
            case 1:
                PlayRevealTween(
                    _happinessBarRoot,
                    _happinessBarRect,
                    _happinessRestAnchoredPosition,
                    _happinessRevealFromOffset);
                break;
        }
    }

    void PlayRevealTween(
        GameObject root,
        RectTransform rect,
        Vector2 restAnchoredPosition,
        Vector2 fromOffset)
    {
        if (root == null)
            return;

        root.SetActive(true);

        if (rect == null)
            return;

        rect.DOKill();
        rect.anchoredPosition = restAnchoredPosition + fromOffset;
        rect.DOAnchorPos(restAnchoredPosition, _revealDuration)
            .SetEase(_revealEase);
    }

    static void SnapRevealRootHidden(GameObject root, RectTransform rect, Vector2 restAnchoredPosition)
    {
        if (rect != null)
        {
            rect.DOKill();
            rect.anchoredPosition = restAnchoredPosition;
        }

        if (root != null)
            root.SetActive(false);
    }

    void SetInterestBubbleVisible(bool visible)
    {
        SetHudRootVisible(_interestBubbleRoot, visible);
    }

    static void SetHudRootVisible(GameObject root, bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    static void DisableLegacyRevealAutoPlay(GameObject root)
    {
        if (root == null)
            return;

        DOTweenAnimation[] animations = root.GetComponents<DOTweenAnimation>();
        for (int i = 0; i < animations.Length; i++)
        {
            if (animations[i] == null)
                continue;

            animations[i].autoPlay = false;
            animations[i].isActive = false;
        }
    }

    void CacheCountdownRestPosition()
    {
        if (_countdownRect == null)
            return;

        _countdownRestAnchoredPosition = _countdownRect.anchoredPosition;
    }

    void PlayCountdownSfx(int step)
    {
        GameAudioId id = step == 1 ? GameAudioId.CountdownStep1 : GameAudioId.CountdownStep32;
        PlayConfiguredSfx(id);
    }

    void PlayTutorialIntroSfx()
    {
        PlayConfiguredSfx(GameAudioId.TutorialIntro);
    }

    /// <summary>
    /// 优先用跨 Scene 单例。菜单进玩法时场景内 AudioManager 会被销毁，
    /// 若只在 Awake 缓存序列化引用，321 / 教程 SFX 会静默失败。
    /// </summary>
    void ResolveAudioRefs()
    {
        if (AudioManager.Instance != null)
            _audioManager = AudioManager.Instance;
        else if (_audioManager == null)
            _audioManager = FindObjectOfType<AudioManager>();

        if (_audioConfig == null && _audioManager != null)
            _audioConfig = _audioManager.Config;
    }

    void PlayConfiguredSfx(GameAudioId id)
    {
        ResolveAudioRefs();
        if (_audioManager == null || _audioConfig == null)
            return;

        AudioClip clip = _audioConfig.GetClip(id);
        if (clip == null)
            return;

        _audioManager.PlaySfx(clip, _audioConfig.GetVolume(id));
    }

    float GetIntroBgmVolumeScale()
    {
        ResolveAudioRefs();
        if (_audioConfig == null)
            return 0.6f;

        return _audioConfig.GetVolume(GameAudioId.BgmMain) * _audioConfig.IntroTutorialBgmVolumeScale;
    }

    void RestoreMainBgmVolume()
    {
        ResolveAudioRefs();
        if (_audioConfig == null)
            return;

        ApplyMainBgmVolumeScale(_audioConfig.GetVolume(GameAudioId.BgmMain));
    }

    void ApplyMainBgmVolumeScale(float volumeScale)
    {
        ResolveAudioRefs();
        if (_audioManager == null || _audioConfig == null)
            return;

        AudioClip clip = _audioConfig.GetClip(GameAudioId.BgmMain);
        if (clip == null)
            return;

        _audioManager.StartMainBgm(clip, volumeScale);
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
        if (_startGameButton == null)
            return;

        bool wasActive = _startGameButton.gameObject.activeSelf;
        if (visible)
        {
            if (wasActive)
                return;

            PlayStartGameAppear();
            return;
        }

        KillStartAppearTween();
        CacheStartGameRestScale();
        if (_startGameRect != null)
            _startGameRect.localScale = _startGameRestScale;

        _startGameButton.gameObject.SetActive(false);
    }

    void PlayStartGameAppear()
    {
        CacheStartGameRestScale();
        KillStartAppearTween();

        _startGameButton.gameObject.SetActive(true);
        if (_startGameRect == null || _startAppearDuration <= 0f)
            return;

        _startGameRect.localScale = Vector3.zero;
        Vector3 peak = _startGameRestScale * _startAppearOvershoot;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(_startGameRect.DOScale(peak, _startAppearDuration * 0.7f).SetEase(_startAppearEase));
        sequence.Append(_startGameRect.DOScale(_startGameRestScale, _startAppearDuration * 0.3f).SetEase(Ease.OutSine));
        sequence.SetUpdate(true);
        sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _startAppearTween = sequence;
    }

    void CacheStartGameRestScale()
    {
        if (_startRestScaleCached || _startGameButton == null)
            return;

        _startGameRect = _startGameButton.transform as RectTransform;
        if (_startGameRect == null)
            return;

        _startGameRestScale = _startGameRect.localScale;
        if (_startGameRestScale == Vector3.zero)
            _startGameRestScale = Vector3.one;

        _startRestScaleCached = true;
    }

    void KillStartAppearTween()
    {
        if (_startAppearTween != null)
        {
            _startAppearTween.Kill();
            _startAppearTween = null;
        }

        _startGameRect?.DOKill();
    }
}
