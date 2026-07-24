using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始菜单 Scene：仅负责加载主玩法 Scene。
/// </summary>
public class StartMenuController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] string _gameSceneName = GameScenes.Gameplay;

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
