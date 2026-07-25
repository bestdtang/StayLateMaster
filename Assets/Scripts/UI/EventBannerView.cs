using System.Collections;
using UnityEngine;

/// <summary>
/// 超级疲劳 / 火热时间进入时短暂弹出对应 Banner，固定时长后隐藏，等待下次触发。
/// </summary>
public class EventBannerView : MonoBehaviour
{
    [Header("Banners")]
    [SerializeField] GameObject _heavyEyesBanner;
    [SerializeField] GameObject _perfectFeedBanner;

    [Header("System References")]
    [SerializeField] CrazyFatigueController _crazyFatigueController;
    [SerializeField] HotTimeController _hotTimeController;
    [SerializeField] RunController _runController;

    [Header("Timing")]
    [SerializeField] float _displayDuration = 1.5f;

    Coroutine _heavyEyesRoutine;
    Coroutine _perfectFeedRoutine;

    void Awake()
    {
        if (_crazyFatigueController == null)
            _crazyFatigueController = FindObjectOfType<CrazyFatigueController>();

        if (_hotTimeController == null)
            _hotTimeController = FindObjectOfType<HotTimeController>();

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        HideBanner(_heavyEyesBanner);
        HideBanner(_perfectFeedBanner);
    }

    void OnEnable()
    {
        if (_crazyFatigueController != null)
            _crazyFatigueController.OnEntered += HandleCrazyFatigueEntered;

        if (_hotTimeController != null)
            _hotTimeController.OnHotTimeStarted += HandleHotTimeStarted;

        if (_runController != null)
            _runController.OnStateChanged += HandleRunStateChanged;
    }

    void OnDisable()
    {
        if (_crazyFatigueController != null)
            _crazyFatigueController.OnEntered -= HandleCrazyFatigueEntered;

        if (_hotTimeController != null)
            _hotTimeController.OnHotTimeStarted -= HandleHotTimeStarted;

        if (_runController != null)
            _runController.OnStateChanged -= HandleRunStateChanged;

        StopAllBannerRoutines();
        HideBanner(_heavyEyesBanner);
        HideBanner(_perfectFeedBanner);
    }

    void HandleCrazyFatigueEntered()
    {
        ShowBannerForDuration(_heavyEyesBanner);
    }

    void HandleHotTimeStarted()
    {
        ShowBannerForDuration(_perfectFeedBanner);
    }

    void HandleRunStateChanged(GameState state)
    {
        if (state == GameState.Win || state == GameState.Lose || state == GameState.Intro)
        {
            StopAllBannerRoutines();
            HideBanner(_heavyEyesBanner);
            HideBanner(_perfectFeedBanner);
        }
    }

    void ShowBannerForDuration(GameObject banner)
    {
        if (banner == null)
            return;

        StopBannerRoutine(banner);
        banner.SetActive(true);
        SetBannerRoutine(banner, StartCoroutine(HideAfterDelay(banner)));
    }

    IEnumerator HideAfterDelay(GameObject banner)
    {
        yield return new WaitForSeconds(_displayDuration);
        HideBanner(banner);
        ClearBannerRoutine(banner);
    }

    void StopBannerRoutine(GameObject banner)
    {
        Coroutine routine = GetBannerRoutine(banner);
        if (routine == null)
            return;

        StopCoroutine(routine);
        ClearBannerRoutine(banner);
    }

    Coroutine GetBannerRoutine(GameObject banner)
    {
        if (banner == _heavyEyesBanner)
            return _heavyEyesRoutine;

        if (banner == _perfectFeedBanner)
            return _perfectFeedRoutine;

        return null;
    }

    void SetBannerRoutine(GameObject banner, Coroutine routine)
    {
        if (banner == _heavyEyesBanner)
            _heavyEyesRoutine = routine;
        else if (banner == _perfectFeedBanner)
            _perfectFeedRoutine = routine;
    }

    void ClearBannerRoutine(GameObject banner)
    {
        if (banner == _heavyEyesBanner)
            _heavyEyesRoutine = null;
        else if (banner == _perfectFeedBanner)
            _perfectFeedRoutine = null;
    }

    static void HideBanner(GameObject banner)
    {
        if (banner != null)
            banner.SetActive(false);
    }

    void StopAllBannerRoutines()
    {
        if (_heavyEyesRoutine != null)
        {
            StopCoroutine(_heavyEyesRoutine);
            _heavyEyesRoutine = null;
        }

        if (_perfectFeedRoutine != null)
        {
            StopCoroutine(_perfectFeedRoutine);
            _perfectFeedRoutine = null;
        }
    }
}
