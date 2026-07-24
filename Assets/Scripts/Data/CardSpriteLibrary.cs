using System;
using UnityEngine;

/// <summary>
/// 单个 Topic 的 Sprite 列表（Jam 最小配置）。
/// </summary>
[Serializable]
public class TopicSpriteEntry
{
    [Tooltip("Topic category for this sprite list.")]
    [SerializeField] TopicId _topic;
    [Tooltip("Pool of card images for this topic.")]
    [SerializeField] Sprite[] _sprites;

    public TopicId Topic => _topic;
    public Sprite[] Sprites => _sprites;
}

/// <summary>
/// 5 个 Topic 的 Sprite 列表总表；Inspector 拖一次即可。
/// </summary>
[CreateAssetMenu(fileName = "CardSpriteLibrary", menuName = "StayLate/Card Sprite Library")]
public class CardSpriteLibrary : ScriptableObject
{
    [Header("Topic Sprite Lists")]
    [Tooltip("One entry per TopicId; each entry holds that topic's sprite pool.")]
    [SerializeField] TopicSpriteEntry[] _topics;

    public TopicSpriteEntry[] Topics => _topics;

    public bool TryGetEntry(TopicId topic, out TopicSpriteEntry entry)
    {
        if (_topics != null)
        {
            for (int i = 0; i < _topics.Length; i++)
            {
                if (_topics[i] != null && _topics[i].Topic == topic)
                {
                    entry = _topics[i];
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }
}
