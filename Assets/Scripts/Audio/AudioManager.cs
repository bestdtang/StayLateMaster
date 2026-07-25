using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 底层音频播放：SFX OneShot、单一主 BGM、命名环境音 Loop 叠层。
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public const string AmbientPhase2Id = "fatigue_phase2";
    public const string AmbientPhase3Id = "fatigue_phase3";
    public const string AmbientCrazyFatigueId = "crazy_fatigue";

    [Header("Config")]
    [SerializeField] GameAudioConfig _config;

    AudioSource _sfxSource;
    AudioSource _mainBgmSource;
    readonly Dictionary<string, AudioSource> _ambientSources = new Dictionary<string, AudioSource>();
    readonly Dictionary<string, Coroutine> _ambientFadeRoutines = new Dictionary<string, Coroutine>();

    Coroutine _mainBgmFadeRoutine;
    bool _isPaused;
    bool _mainBgmActive;

    public GameAudioConfig Config => _config;
    public bool IsMainBgmPlaying => _mainBgmActive && _mainBgmSource != null && _mainBgmSource.isPlaying;

    void Awake()
    {
        EnsureSources();
    }

    public void SetConfig(GameAudioConfig config)
    {
        _config = config;
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _config == null)
            return;

        EnsureSources();
        float volume = _config.MasterVolume * _config.SfxVolume * volumeScale;
        _sfxSource.PlayOneShot(clip, volume);
    }

    public void StartMainBgm(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _config == null)
            return;

        EnsureSources();

        if (_mainBgmActive
            && _mainBgmSource.isPlaying
            && _mainBgmSource.clip == clip)
        {
            _mainBgmSource.volume = _config.MasterVolume * _config.BgmVolume * volumeScale;
            return;
        }

        if (_mainBgmFadeRoutine != null)
        {
            StopCoroutine(_mainBgmFadeRoutine);
            _mainBgmFadeRoutine = null;
        }

        float volume = _config.MasterVolume * _config.BgmVolume * volumeScale;
        _mainBgmSource.clip = clip;
        _mainBgmSource.volume = volume;
        _mainBgmSource.loop = true;
        _mainBgmSource.Play();
        _mainBgmActive = true;
    }

    public void StopMainBgm(float fadeOut = 0.5f)
    {
        if (!_mainBgmActive || _mainBgmSource == null)
            return;

        if (_mainBgmFadeRoutine != null)
        {
            StopCoroutine(_mainBgmFadeRoutine);
            _mainBgmFadeRoutine = null;
        }

        _mainBgmActive = false;

        if (fadeOut <= 0f || !_mainBgmSource.isPlaying)
        {
            _mainBgmSource.Stop();
            return;
        }

        _mainBgmFadeRoutine = StartCoroutine(FadeOutAndStopSource(_mainBgmSource, fadeOut, () =>
        {
            _mainBgmFadeRoutine = null;
        }));
    }

    public bool IsAmbientLoopPlaying(string layerId)
    {
        return _ambientSources.TryGetValue(layerId, out AudioSource source)
               && source != null
               && source.isPlaying;
    }

    /// <summary>叠加环境音 Loop（疲劳阶段 / 疯狂疲劳等），与主 BGM 并行。</summary>
    public void StartAmbientLoop(string layerId, AudioClip clip, float fadeIn = 0.3f, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(layerId) || clip == null || _config == null)
            return;

        EnsureSources();

        if (IsAmbientLoopPlaying(layerId))
            return;

        AudioSource source = GetOrCreateAmbientSource(layerId);
        CancelAmbientFade(layerId);

        float targetVolume = _config.MasterVolume * _config.BgmVolume * volumeScale;
        source.clip = clip;
        source.loop = true;

        if (fadeIn <= 0f)
        {
            source.volume = targetVolume;
            source.Play();
            return;
        }

        source.volume = 0f;
        source.Play();
        _ambientFadeRoutines[layerId] = StartCoroutine(FadeAmbientVolume(source, 0f, targetVolume, fadeIn, layerId));
    }

    public void StopAmbientLoop(string layerId, float fadeOut = 0.3f)
    {
        if (string.IsNullOrEmpty(layerId)
            || !_ambientSources.TryGetValue(layerId, out AudioSource source)
            || source == null)
        {
            return;
        }

        CancelAmbientFade(layerId);

        if (fadeOut <= 0f || !source.isPlaying)
        {
            source.Stop();
            return;
        }

        float startVolume = source.volume;
        _ambientFadeRoutines[layerId] = StartCoroutine(FadeAmbientVolume(source, startVolume, 0f, fadeOut, layerId, stopAfter: true));
    }

    public void StopAllAmbientLoops(float fadeOut = 0.3f)
    {
        var layerIds = new List<string>(_ambientSources.Keys);
        for (int i = 0; i < layerIds.Count; i++)
            StopAmbientLoop(layerIds[i], fadeOut);
    }

    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        EnsureSources();

        SetSourcePaused(_mainBgmSource, paused, resumeWhenUnpaused: _mainBgmActive);

        foreach (KeyValuePair<string, AudioSource> entry in _ambientSources)
        {
            if (entry.Value != null && entry.Value.isPlaying)
                SetSourcePaused(entry.Value, paused, resumeWhenUnpaused: true);
        }
    }

    void EnsureSources()
    {
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
        }

        if (_mainBgmSource == null)
        {
            _mainBgmSource = gameObject.AddComponent<AudioSource>();
            _mainBgmSource.playOnAwake = false;
            _mainBgmSource.loop = true;
        }
    }

    AudioSource GetOrCreateAmbientSource(string layerId)
    {
        if (_ambientSources.TryGetValue(layerId, out AudioSource existing) && existing != null)
            return existing;

        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        _ambientSources[layerId] = source;
        return source;
    }

    void CancelAmbientFade(string layerId)
    {
        if (!_ambientFadeRoutines.TryGetValue(layerId, out Coroutine routine) || routine == null)
            return;

        StopCoroutine(routine);
        _ambientFadeRoutines.Remove(layerId);
    }

    static void SetSourcePaused(AudioSource source, bool paused, bool resumeWhenUnpaused)
    {
        if (source == null)
            return;

        if (paused)
            source.Pause();
        else if (resumeWhenUnpaused)
            source.UnPause();
    }

    IEnumerator FadeAmbientVolume(
        AudioSource source,
        float fromVolume,
        float toVolume,
        float duration,
        string layerId,
        bool stopAfter = false)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_isPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            source.volume = Mathf.Lerp(fromVolume, toVolume, t);
            yield return null;
        }

        source.volume = toVolume;

        if (stopAfter)
            source.Stop();

        _ambientFadeRoutines.Remove(layerId);
    }

    IEnumerator FadeOutAndStopSource(AudioSource source, float fadeOut, System.Action onComplete)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeOut > 0f ? Mathf.Clamp01(elapsed / fadeOut) : 1f;
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        source.Stop();
        onComplete?.Invoke();
    }
}
