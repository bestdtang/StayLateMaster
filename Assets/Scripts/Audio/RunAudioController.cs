using UnityEngine;

/// <summary>
/// 订阅 gameplay 事件，驱动 SFX、主 BGM 与疲劳阶段环境音叠层。
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

    void Awake()
    {
        if (_audioManager == null)
            _audioManager = GetComponent<AudioManager>();

        if (_audioManager == null)
            _audioManager = FindObjectOfType<AudioManager>();

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
        TryStartMainBgm();
    }

    void OnDisable()
    {
        UnsubscribeAll();
        _audioManager?.StopAllAmbientLoops();
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
                _audioManager?.SetPaused(true);
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

        _audioManager?.SetPaused(false);
        _audioManager?.StopAllAmbientLoops(fadeOut: 0f);

        TryStartMainBgm();
        RefreshFatigueAmbients(force: true);
    }

    void TryStartMainBgm()
    {
        if (_audioConfig == null)
            return;

        AudioClip clip = _audioConfig.GetClip(GameAudioId.BgmMain);
        if (clip == null)
            return;

        _audioManager?.StartMainBgm(clip, _audioConfig.GetVolume(GameAudioId.BgmMain));
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
        RefreshFatigueAmbients(force: false);
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
            GetAmbientFadeIn(FatigueAudioPhase.Phase3));
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

    void RefreshFatigueAmbients(bool force)
    {
        if (_audioManager == null || _fatigueMeter == null || _runController == null || _audioConfig == null)
            return;

        GameState state = _runController.CurrentState;
        if (state == GameState.Win || state == GameState.Lose)
            return;

        FatigueAudioPhase targetPhase = ResolveFatiguePhase();

        if (!force && targetPhase == _currentFatiguePhase)
            return;

        _currentFatiguePhase = targetPhase;

        if (targetPhase >= FatigueAudioPhase.Phase2)
            TryStartPhaseAmbient(FatigueAudioPhase.Phase2, AudioManager.AmbientPhase2Id);

        if (targetPhase >= FatigueAudioPhase.Phase3)
            TryStartPhaseAmbient(FatigueAudioPhase.Phase3, AudioManager.AmbientPhase3Id);
    }

    void TryStartPhaseAmbient(FatigueAudioPhase phase, string layerId)
    {
        if (_audioManager.IsAmbientLoopPlaying(layerId))
            return;

        GameAudioId audioId = _audioConfig.GetAmbientAudioId(phase);
        StartAmbientLoop(layerId, audioId, GetAmbientFadeIn(phase));
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

    float GetAmbientFadeIn(FatigueAudioPhase phase)
    {
        GameBalanceConfig balance = _runController != null ? _runController.BalanceConfig : null;
        if (balance == null)
            return _audioConfig != null ? _audioConfig.BgmFallbackFadeDuration : 1.5f;

        BgmTrack track = phase switch
        {
            FatigueAudioPhase.Phase2 => BgmTrack.Phase2,
            FatigueAudioPhase.Phase3 => BgmTrack.Phase3,
            _ => BgmTrack.LowFatigue
        };

        return balance.GetBgmCrossfadeDuration(track);
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
