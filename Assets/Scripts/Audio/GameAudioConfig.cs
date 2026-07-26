using UnityEngine;

/// <summary>
/// 全局音效与 BGM 资源配置；在 Project 中创建资产后挂到 AudioManager / RunAudioController。
/// 每个音效可单独调节音量（0–2，再乘 Master / SFX / BGM 总线）。
/// </summary>
[CreateAssetMenu(fileName = "GameAudioConfig", menuName = "StayLate/Game Audio Config")]
public class GameAudioConfig : ScriptableObject
{
    [Header("Intro Tutorial")]
    [Tooltip("进入玩法 Scene、首次教程 overlay 打开时播放一次。")]
    [SerializeField] AudioClip _tutorialIntro;
    [SerializeField, Range(0f, 2f)] float _tutorialIntroVolume = 1f;
    [Tooltip("首次教程打开期间，主 BGM 相对正常音量的倍率。")]
    [SerializeField, Range(0f, 1f)] float _introTutorialBgmVolumeScale = 0.6f;

    [Header("Intro Countdown")]
    [Tooltip("开局 321 倒计时：3 与 2 共用。")]
    [SerializeField] AudioClip _countdownStep32;
    [SerializeField, Range(0f, 2f)] float _countdownStep32Volume = 1f;
    [Tooltip("开局 321 倒计时：1 专用。")]
    [SerializeField] AudioClip _countdownStep1;
    [SerializeField, Range(0f, 2f)] float _countdownStep1Volume = 1f;

    [Header("Run Outcome")]
    [SerializeField] AudioClip _victory;
    [SerializeField, Range(0f, 2f)] float _victoryVolume = 1f;
    [SerializeField] AudioClip _defeat;
    [SerializeField, Range(0f, 2f)] float _defeatVolume = 1f;

    [Header("Blink")]
    [SerializeField] AudioClip _blinkSuccess;
    [SerializeField, Range(0f, 2f)] float _blinkSuccessVolume = 1f;
    [SerializeField] AudioClip _blinkFail;
    [SerializeField, Range(0f, 2f)] float _blinkFailVolume = 1f;
    [SerializeField] AudioClip _crazyFatigueEnter;
    [SerializeField, Range(0f, 2f)] float _crazyFatigueEnterVolume = 1f;
    [SerializeField] AudioClip _crazyFatigueExit;
    [SerializeField, Range(0f, 2f)] float _crazyFatigueExitVolume = 1f;
    [Tooltip("疯狂疲劳持续 Loop，叠在主 BGM 之上。")]
    [SerializeField] AudioClip _crazyFatigueLoop;
    [SerializeField, Range(0f, 2f)] float _crazyFatigueLoopVolume = 1f;

    [Header("Swipe")]
    [Tooltip("划手机手势；与判定无关。")]
    [SerializeField] AudioClip _phoneSwipe;
    [SerializeField, Range(0f, 2f)] float _phoneSwipeVolume = 1f;
    [Tooltip("快乐值上升时播放。")]
    [SerializeField] AudioClip _swipeCorrect;
    [SerializeField, Range(0f, 2f)] float _swipeCorrectVolume = 1f;
    [Tooltip("快乐值下降时播放。")]
    [SerializeField] AudioClip _swipeWrong;
    [SerializeField, Range(0f, 2f)] float _swipeWrongVolume = 1f;

    [Header("Interest / Hot / Combo")]
    [SerializeField] AudioClip _interestChanged;
    [SerializeField, Range(0f, 2f)] float _interestChangedVolume = 1f;
    [SerializeField] AudioClip _hotTimeEnter;
    [SerializeField, Range(0f, 2f)] float _hotTimeEnterVolume = 1f;
    [SerializeField] AudioClip _hotTimeSwipe;
    [SerializeField, Range(0f, 2f)] float _hotTimeSwipeVolume = 1f;
    [SerializeField] AudioClip _hotTimeExit;
    [SerializeField, Range(0f, 2f)] float _hotTimeExitVolume = 1f;
    [SerializeField] AudioClip _comboTick;
    [SerializeField, Range(0f, 2f)] float _comboTickVolume = 1f;

    [Header("BGM")]
    [Tooltip("主 BGM：开始菜单起播，跨 Scene 续播；入场教程压低，胜负 fade out。")]
    [SerializeField] AudioClip _bgmMain;
    [SerializeField, Range(0f, 2f)] float _bgmMainVolume = 1f;
    [Tooltip("预留：火热时间环境叠层（D 区 polish）。")]
    [SerializeField] AudioClip _ambientHotStreak;
    [SerializeField, Range(0f, 2f)] float _ambientHotStreakVolume = 1f;
    [Tooltip("胜负时主 BGM 淡出时长（秒）。")]
    [SerializeField] float _bgmFallbackFadeDuration = 1.5f;

    [Header("Fatigue Yawn")]
    [Tooltip("疲劳二/三阶段间歇播放的打哈气音效。")]
    [SerializeField] AudioClip _fatigueYawn;
    [SerializeField, Range(0f, 2f)] float _fatigueYawnVolume = 1f;
    [Tooltip("二阶段打哈气间隔（秒）。")]
    [SerializeField] float _fatigueYawnIntervalPhase2 = 10f;
    [Tooltip("三阶段打哈气间隔（秒）。")]
    [SerializeField] float _fatigueYawnIntervalPhase3 = 5f;

    [Header("Volume Buses")]
    [SerializeField, Range(0f, 1f)] float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] float _sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] float _bgmVolume = 0.6f;

    public float BgmFallbackFadeDuration => _bgmFallbackFadeDuration;
    public float IntroTutorialBgmVolumeScale => _introTutorialBgmVolumeScale;
    public float MasterVolume => _masterVolume;
    public float SfxVolume => _sfxVolume;
    public float BgmVolume => _bgmVolume;

    public AudioClip GetClip(GameAudioId id)
    {
        switch (id)
        {
            case GameAudioId.TutorialIntro: return _tutorialIntro;
            case GameAudioId.CountdownStep32: return _countdownStep32;
            case GameAudioId.CountdownStep1: return _countdownStep1;
            case GameAudioId.Victory: return _victory;
            case GameAudioId.Defeat: return _defeat;
            case GameAudioId.BlinkSuccess: return _blinkSuccess;
            case GameAudioId.BlinkFail: return _blinkFail;
            case GameAudioId.CrazyFatigueEnter: return _crazyFatigueEnter;
            case GameAudioId.CrazyFatigueExit: return _crazyFatigueExit;
            case GameAudioId.CrazyFatigueLoop: return _crazyFatigueLoop;
            case GameAudioId.PhoneSwipe: return _phoneSwipe;
            case GameAudioId.SwipeCorrect: return _swipeCorrect;
            case GameAudioId.SwipeWrong: return _swipeWrong;
            case GameAudioId.InterestChanged: return _interestChanged;
            case GameAudioId.HotTimeEnter: return _hotTimeEnter;
            case GameAudioId.HotTimeSwipe: return _hotTimeSwipe;
            case GameAudioId.HotTimeExit: return _hotTimeExit;
            case GameAudioId.ComboTick: return _comboTick;
            case GameAudioId.BgmMain: return _bgmMain;
            case GameAudioId.FatigueYawn: return _fatigueYawn;
            case GameAudioId.AmbientHotStreak: return _ambientHotStreak;
            default: return null;
        }
    }

    public float GetVolume(GameAudioId id)
    {
        switch (id)
        {
            case GameAudioId.TutorialIntro: return _tutorialIntroVolume;
            case GameAudioId.CountdownStep32: return _countdownStep32Volume;
            case GameAudioId.CountdownStep1: return _countdownStep1Volume;
            case GameAudioId.Victory: return _victoryVolume;
            case GameAudioId.Defeat: return _defeatVolume;
            case GameAudioId.BlinkSuccess: return _blinkSuccessVolume;
            case GameAudioId.BlinkFail: return _blinkFailVolume;
            case GameAudioId.CrazyFatigueEnter: return _crazyFatigueEnterVolume;
            case GameAudioId.CrazyFatigueExit: return _crazyFatigueExitVolume;
            case GameAudioId.CrazyFatigueLoop: return _crazyFatigueLoopVolume;
            case GameAudioId.PhoneSwipe: return _phoneSwipeVolume;
            case GameAudioId.SwipeCorrect: return _swipeCorrectVolume;
            case GameAudioId.SwipeWrong: return _swipeWrongVolume;
            case GameAudioId.InterestChanged: return _interestChangedVolume;
            case GameAudioId.HotTimeEnter: return _hotTimeEnterVolume;
            case GameAudioId.HotTimeSwipe: return _hotTimeSwipeVolume;
            case GameAudioId.HotTimeExit: return _hotTimeExitVolume;
            case GameAudioId.ComboTick: return _comboTickVolume;
            case GameAudioId.BgmMain: return _bgmMainVolume;
            case GameAudioId.FatigueYawn: return _fatigueYawnVolume;
            case GameAudioId.AmbientHotStreak: return _ambientHotStreakVolume;
            default: return 1f;
        }
    }

    public float GetEffectiveSfxVolume(GameAudioId id)
    {
        return _masterVolume * _sfxVolume * GetVolume(id);
    }

    public float GetEffectiveBgmVolume(GameAudioId id)
    {
        return _masterVolume * _bgmVolume * GetVolume(id);
    }

    public float GetFatigueYawnInterval(FatigueAudioPhase phase)
    {
        switch (phase)
        {
            case FatigueAudioPhase.Phase3:
                return Mathf.Max(0.1f, _fatigueYawnIntervalPhase3);
            case FatigueAudioPhase.Phase2:
                return Mathf.Max(0.1f, _fatigueYawnIntervalPhase2);
            default:
                return 0f;
        }
    }
}
