/// <summary>
/// 游戏内音效标识，与 GameAudioConfig 中的 Clip + 音量一一对应。
/// </summary>
public enum GameAudioId
{
    CountdownStep32,
    CountdownStep1,
    Victory,
    Defeat,
    BlinkSuccess,
    BlinkFail,
    CrazyFatigueEnter,
    CrazyFatigueExit,
    CrazyFatigueLoop,
    PhoneSwipe,
    SwipeCorrect,
    SwipeWrong,
    InterestChanged,
    HotTimeEnter,
    HotTimeSwipe,
    HotTimeExit,
    ComboTick,
    BgmMain,
    AmbientFatiguePhase2,
    AmbientFatiguePhase3,
    AmbientHotStreak
}
