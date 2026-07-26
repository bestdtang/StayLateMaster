using UnityEngine;

/// <summary>
/// 订阅 gameplay 事件，驱动 SFX、主 BGM、疯狂疲劳 Loop 与疲劳阶段打哈气。
/// </summary>
[DisallowMultipleComponent]
public class RunAudioController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] AudioManager _audioManager;
    [SerializeField] GameAudioConfig _audioConfig;

    [Header("System References")]
    [SerializeField] RunController _runController;
    [SerializeField] HappinessMeter _happinessMeter;
    [SerializeField] FatigueMeter _fatigueMeter;
    [SerializeField] BlinkInput _blinkInput;
    [SerializeField] CrazyFatigueController _crazyFatigueController;
    [SerializeField] HotTimeController _hotTimeController;
    [SerializeField] InterestManager _interestManager;
    [SerializeField] ComboStreakCounter _comboStreakCounter;
    [SerializeField] SwipeInput _swipeInput;

    float _lastHappiness;
    int _lastComboStreak;
    bool _interestInitialized;
    FatigueAudioPhase _currentFatiguePhase = FatigueAudioPhase.Phase1;
    float _fatigueYawnTimer;

    void Awake()
    {
        // 优先用跨 Scene 单例（开始菜单已起播的 BGM）；场景内重复组件会被销毁
        _audioManager = AudioManager.GetOrCreate(_audioConfig);

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_fatigueMeter == null)
            _fatigueMeter = FindObjectOfType<FatigueMeter>();

        if (_happinessMeter == null)
            _happinessMeter = FindObjectOfType<HappinessMeter>();

        if (_blinkInput == null)
            _blinkInput = FindObjectOfType<BlinkInput>();

        if (_crazyFatigueController == null)
            _crazyFatigueController = FindObjectOfType<CrazyFatigueController>();

        if (_hotTimeController == null)
            _hotTimeController = FindObjectOfType<HotTimeController>();

        if (_interestManager == null)
            _interestManager = FindObjectOfType<InterestManager>();

        if (_comboStreakCounter == null)
            _comboStreakCounter = FindObjectOfType<ComboStreakCounter>();

        if (_swipeInput == null)
            _swipeInput = FindObjectOfType<SwipeInput>();

        if (_audioManager != null && _audioConfig != null)
            _audioManager.SetConfig(_audioConfig);
    }

    void OnEnable()
    {
        SubscribeAll();
    }

    void Start()
    {
        // 菜单已起播则只改音量；Intro 时压低，避免先满音再突然变小
        TryStartMainBgm(duckForTutorial: IsInIntro());
    }

    void Update()
    {
        TickFatigueYawn();
    }

    void OnDisable()
    {
        UnsubscribeAll();
        // 禁用时对象可能已 inactive，fade 会触发 coroutine 报错，直接停
        _audioManager?.StopAllAmbientLoops(fadeOut: 0f);
    }

    void SubscribeAll()
    {
        if (_runController != null)
        {
            _runController.OnStateChanged += HandleRunStateChanged;
            _runController.OnGameWon += HandleGameWon;
            _runController.OnGameLost += HandleGameLost;
        }

        if (_fatigueMeter != null)
            _fatigueMeter.OnValueChanged += HandleFatigueChanged;

        if (_happinessMeter != null)
            _happinessMeter.OnValueChanged += HandleHappinessChanged;

        if (_blinkInput != null)
        {
            _blinkInput.OnBlinkSuccess += HandleBlinkSuccess;
            _blinkInput.OnBlinkEarlyLate += HandleBlinkEarlyLate;
            _blinkInput.OnBlinkCompleteMiss += HandleBlinkCompleteMiss;
        }

        if (_crazyFatigueController != null)
        {
            _crazyFatigueController.OnEntered += HandleCrazyFatigueEntered;
            _crazyFatigueController.OnExited += HandleCrazyFatigueExited;
        }

        if (_hotTimeController != null)
        {
            _hotTimeController.OnHotTimeStarted += HandleHotTimeStarted;
            _hotTimeController.OnHotTimeEnded += HandleHotTimeEnded;
            _hotTimeController.OnHotSwipe += HandleHotSwipe;
        }

        if (_interestManager != null)
            _interestManager.OnInterestChanged += HandleInterestChanged;

        if (_comboStreakCounter != null)
            _comboStreakCounter.OnStreakChanged += HandleComboStreakChanged;

        if (_swipeInput != null)
            _swipeInput.OnSwipe += HandlePhoneSwipe;
    }

    void UnsubscribeAll()
    {
        if (_runController != null)
        {
            _runController.OnStateChanged -= HandleRunStateChanged;
            _runController.OnGameWon -= HandleGameWon;
            _runController.OnGameLost -= HandleGameLost;
        }

        if (_fatigueMeter != null)
            _fatigueMeter.OnValueChanged -= HandleFatigueChanged;

        if (_happinessMeter != null)
            _happinessMeter.OnValueChanged -= HandleHappinessChanged;

        if (_blinkInput != null)
        {
            _blinkInput.OnBlinkSuccess -= HandleBlinkSuccess;
            _blinkInput.OnBlinkEarlyLate -= HandleBlinkEarlyLate;
            _blinkInput.OnBlinkCompleteMiss -= HandleBlinkCompleteMiss;
        }

        if (_crazyFatigueController != null)
        {
            _crazyFatigueController.OnEntered -= HandleCrazyFatigueEntered;
            _crazyFatigueController.OnExited -= HandleCrazyFatigueExited;
        }

        if (_hotTimeController != null)
        {
            _hotTimeController.OnHotTimeStarted -= HandleHotTimeStarted;
            _hotTimeController.OnHotTimeEnded -= HandleHotTimeEnded;
            _hotTimeController.OnHotSwipe -= HandleHotSwipe;
        }

        if (_interestManager != null)
            _interestManager.OnInterestChanged -= HandleInterestChanged;

        if (_comboStreakCounter != null)
            _comboStreakCounter.OnStreakChanged -= HandleComboStreakChanged;

        if (_swipeInput != null)
            _swipeInput.OnSwipe -= HandlePhoneSwipe;
    }

    void HandleRunStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:
                ResetRunAudio();
                break;
            case GameState.Paused:
                // 教程 overlay：玩法暂停，但 BGM 继续（由 TutorialPageView 压低音量并播 OneShot）
                break;
            case GameState.HotStreak:
                _audioManager?.SetPaused(false);
                break;
            case GameState.Win:
            case GameState.Lose:
                _audioManager?.SetPaused(false);
                break;
        }
    }

    void ResetRunAudio()
    {
        _lastComboStreak = _comboStreakCounter != null ? _comboStreakCounter.CurrentStreak : 0;
        _lastHappiness = _happinessMeter != null ? _happinessMeter.Value : 0f;
        _interestInitialized = false;
        _currentFatiguePhase = FatigueAudioPhase.Phase1;
        _fatigueYawnTimer = 0f;

        _audioManager?.SetPaused(false);
        _audioManager?.StopAllAmbientLoops(fadeOut: 0f);

        TryStartMainBgm();
        RefreshFatigueYawnPhase(force: true);
    }

    void TryStartMainBgm(bool duckForTutorial = false)
    {
        if (_audioConfig == null)
            return;

        AudioClip clip = _audioConfig.GetClip(GameAudioId.BgmMain);
        if (clip == null)
            return;

        float volumeScale = _audioConfig.GetVolume(GameAudioId.BgmMain);
        if (duckForTutorial)
            volumeScale *= _audioConfig.IntroTutorialBgmVolumeScale;

        _audioManager?.StartMainBgm(clip, volumeScale);
    }

    bool IsInIntro()
    {
        return _runController != null && _runController.CurrentState == GameState.Intro;
    }

    void HandleGameWon()
    {
        PlaySfx(GameAudioId.Victory);
        StopRunMusic();
    }

    void HandleGameLost()
    {
        PlaySfx(GameAudioId.Defeat);
        StopRunMusic();
    }

    void StopRunMusic()
    {
        float fade = _audioConfig != null ? _audioConfig.BgmFallbackFadeDuration : 0.5f;
        _audioManager?.StopAllAmbientLoops(fade);
        _audioManager?.StopMainBgm(fade);
        _fatigueYawnTimer = 0f;
        _currentFatiguePhase = FatigueAudioPhase.Phase1;
    }

    void HandleHappinessChanged(float value)
    {
        if (_audioConfig == null)
            return;

        float delta = value - _lastHappiness;
        _lastHappiness = value;

        if (Mathf.Approximately(delta, 0f))
            return;

        if (delta > 0f)
        {
            if (_hotTimeController != null && _hotTimeController.IsActive)
                return;

            PlaySfx(GameAudioId.SwipeCorrect);
            return;
        }

        PlaySfx(GameAudioId.SwipeWrong);
    }

    void HandleFatigueChanged(float value)
    {
        RefreshFatigueYawnPhase(force: false);
    }

    void HandleBlinkSuccess()
    {
        PlayBlinkSfx();
    }

    void HandleBlinkEarlyLate()
    {
        PlayBlinkSfx();
    }

    void HandleBlinkCompleteMiss()
    {
        PlaySfx(GameAudioId.BlinkFail);
    }

    void PlayBlinkSfx()
    {
        PlaySfx(GameAudioId.BlinkSuccess);
    }

    void HandleCrazyFatigueEntered()
    {
        PlaySfx(GameAudioId.CrazyFatigueEnter);
        StartAmbientLoop(
            AudioManager.AmbientCrazyFatigueId,
            GameAudioId.CrazyFatigueLoop,
            GetCrazyFatigueLoopFadeIn());
    }

    void HandleCrazyFatigueExited()
    {
        PlaySfx(GameAudioId.CrazyFatigueExit);
        _audioManager?.StopAmbientLoop(AudioManager.AmbientCrazyFatigueId);
    }

    void HandleHotTimeStarted()
    {
        PlaySfx(GameAudioId.HotTimeEnter);
    }

    void HandleHotTimeEnded()
    {
        PlaySfx(GameAudioId.HotTimeExit);
    }

    void HandleHotSwipe(bool wasCorrect)
    {
        if (!wasCorrect)
            return;

        PlaySfx(GameAudioId.HotTimeSwipe);
    }

    void HandleInterestChanged(TopicId topic)
    {
        if (!_interestInitialized)
        {
            _interestInitialized = true;
            return;
        }

        PlaySfx(GameAudioId.InterestChanged);
    }

    void HandleComboStreakChanged(int streak)
    {
        if (_audioConfig == null)
            return;

        if (streak > _lastComboStreak && streak >= 1)
            PlaySfx(GameAudioId.ComboTick);

        _lastComboStreak = streak;
    }

    void HandlePhoneSwipe(SwipeDirection direction)
    {
        PlaySfx(GameAudioId.PhoneSwipe);
    }

    void RefreshFatigueYawnPhase(bool force)
    {
        if (_audioConfig == null || _runController == null)
            return;

        GameState state = _runController.CurrentState;
        if (state == GameState.Win || state == GameState.Lose)
            return;

        FatigueAudioPhase targetPhase = ResolveFatiguePhase();
        if (!force && targetPhase == _currentFatiguePhase)
            return;

        bool enteredYawnPhase = targetPhase > _currentFatiguePhase
                                && targetPhase >= FatigueAudioPhase.Phase2;

        _currentFatiguePhase = targetPhase;
        _fatigueYawnTimer = 0f;

        // 跨入二/三阶段时立刻打一次哈气，之后按间隔重复
        if (enteredYawnPhase)
            PlaySfx(GameAudioId.FatigueYawn);
    }

    void TickFatigueYawn()
    {
        if (_audioConfig == null || _runController == null)
            return;

        GameState state = _runController.CurrentState;
        if (state != GameState.Playing && state != GameState.HotStreak)
            return;

        if (_currentFatiguePhase < FatigueAudioPhase.Phase2)
            return;

        float interval = _audioConfig.GetFatigueYawnInterval(_currentFatiguePhase);
        if (interval <= 0f)
            return;

        _fatigueYawnTimer += Time.deltaTime;
        if (_fatigueYawnTimer < interval)
            return;

        _fatigueYawnTimer = 0f;
        PlaySfx(GameAudioId.FatigueYawn);
    }

    void StartAmbientLoop(string layerId, GameAudioId audioId, float fadeIn)
    {
        if (_audioConfig == null || _audioManager == null)
            return;

        AudioClip clip = _audioConfig.GetClip(audioId);
        if (clip == null)
            return;

        _audioManager.StartAmbientLoop(layerId, clip, fadeIn, _audioConfig.GetVolume(audioId));
    }

    FatigueAudioPhase ResolveFatiguePhase()
    {
        GameBalanceConfig balance = _runController != null ? _runController.BalanceConfig : null;
        float phase2At = balance != null ? balance.ZonePhase2AtFatigue : 50f;
        float phase3At = balance != null ? balance.ZonePhase3AtFatigue : 80f;
        float fatigue = _fatigueMeter != null ? _fatigueMeter.Value : 0f;

        if (fatigue >= phase3At)
            return FatigueAudioPhase.Phase3;

        if (fatigue >= phase2At)
            return FatigueAudioPhase.Phase2;

        return FatigueAudioPhase.Phase1;
    }

    float GetCrazyFatigueLoopFadeIn()
    {
        GameBalanceConfig balance = _runController != null ? _runController.BalanceConfig : null;
        if (balance == null)
            return _audioConfig != null ? _audioConfig.BgmFallbackFadeDuration : 1.5f;

        return balance.GetBgmCrossfadeDuration(BgmTrack.Phase3);
    }

    void PlaySfx(GameAudioId id)
    {
        if (_audioConfig == null || _audioManager == null)
            return;

        AudioClip clip = _audioConfig.GetClip(id);
        if (clip == null)
            return;

        _audioManager.PlaySfx(clip, _audioConfig.GetVolume(id));
    }
}
