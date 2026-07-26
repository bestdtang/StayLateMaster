using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始菜单 Scene：播放主 BGM，并负责加载主玩法 Scene。
/// </summary>
public class StartMenuController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] string _gameSceneName = GameScenes.Gameplay;

    [Header("Audio")]
    [SerializeField] GameAudioConfig _audioConfig;

    [Header("UI References")]
    [SerializeField] Button _startButton;

    void Awake()
    {
        if (_startButton == null)
            _startButton = FindButtonByName("Start");
    }

    void OnEnable()
    {
        if (_startButton != null)
            _startButton.onClick.AddListener(StartGame);
    }

    void OnDisable()
    {
        if (_startButton != null)
            _startButton.onClick.RemoveListener(StartGame);
    }

    void Start()
    {
        EnsureMenuBgm();
    }

    void EnsureMenuBgm()
    {
        AudioManager audio = AudioManager.GetOrCreate(_audioConfig);
        if (_audioConfig == null)
            _audioConfig = audio != null ? audio.Config : null;

        if (audio == null || _audioConfig == null)
            return;

        AudioClip clip = _audioConfig.GetClip(GameAudioId.BgmMain);
        if (clip == null)
            return;

        audio.StartMainBgm(clip, _audioConfig.GetVolume(GameAudioId.BgmMain));
    }

    void StartGame()
    {
        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[StartMenuController] 未配置主玩法 Scene 名。");
            return;
        }

        SceneManager.LoadScene(_gameSceneName);
    }

    static Button FindButtonByName(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }
}
