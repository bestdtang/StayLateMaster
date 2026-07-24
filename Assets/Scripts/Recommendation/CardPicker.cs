using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 从 Sprite 库抽取下一张卡；Topic 加权随机 + Sprite 防重复。
/// </summary>
public class CardPicker : MonoBehaviour
{
    [Header("Sprite Library")]
    [Tooltip("All Topic sprite lists; one ScriptableObject asset.")]
    [SerializeField] CardSpriteLibrary _spriteLibrary;

    [Header("Pick Settings")]
    [Tooltip("Weighted Topic selection; uniform random if unset.")]
    [SerializeField] TopicWeights _topicWeights;
    [Tooltip("Recent sprites excluded from the next pick to reduce repeats.")]
    [SerializeField] int _recentHistorySize = 8;

    readonly List<Sprite> _recentSprites = new List<Sprite>();

    bool _hasForcedTopic;
    TopicId _forcedTopic;

    void Awake()
    {
        if (_topicWeights == null)
            _topicWeights = FindObjectOfType<TopicWeights>();
    }

    public void ResetHistory()
    {
        _recentSprites.Clear();
    }

    /// <summary>火热时间：强制只出指定 Topic，且不改动长期权重/保底。</summary>
    public void SetForcedTopic(TopicId topic)
    {
        _hasForcedTopic = true;
        _forcedTopic = topic;
    }

    /// <summary>火热时间结束：恢复正常加权抽卡。</summary>
    public void ClearForcedTopic()
    {
        _hasForcedTopic = false;
    }

    public bool TryPickNext(out PickedCard picked)
    {
        picked = default;

        if (_spriteLibrary == null || _spriteLibrary.Topics == null || _spriteLibrary.Topics.Length == 0)
        {
            Debug.LogWarning("[CardPicker] Sprite 库未配置。");
            return false;
        }

        TopicSpriteEntry topicEntry = PickTopicEntry();
        if (topicEntry == null || topicEntry.Sprites == null || topicEntry.Sprites.Length == 0)
        {
            Debug.LogWarning("[CardPicker] 没有可用的 Topic Sprite 列表。");
            return false;
        }

        Sprite sprite = PickSpriteExcludingRecent(topicEntry.Sprites);
        if (sprite == null)
            sprite = topicEntry.Sprites[UnityEngine.Random.Range(0, topicEntry.Sprites.Length)];

        RegisterSprite(sprite);
        picked = new PickedCard(topicEntry.Topic, sprite);
        return true;
    }

    TopicSpriteEntry PickTopicEntry()
    {
        TopicSpriteEntry[] topics = _spriteLibrary.Topics;
        var valid = new List<TopicSpriteEntry>(topics.Length);

        for (int i = 0; i < topics.Length; i++)
        {
            TopicSpriteEntry entry = topics[i];
            if (entry != null && entry.Sprites != null && entry.Sprites.Length > 0)
                valid.Add(entry);
        }

        if (valid.Count == 0)
            return null;

        // 火热时间：强制当前兴趣 Topic，不调用 NotifyTopicPicked（长期权重/保底保持不变）
        if (_hasForcedTopic)
        {
            for (int i = 0; i < valid.Count; i++)
            {
                if (valid[i].Topic == _forcedTopic)
                    return valid[i];
            }
        }

        if (_topicWeights == null)
            return valid[UnityEngine.Random.Range(0, valid.Count)];

        // 硬保底：无兴趣间隔达阈值时强制当前兴趣（仅在其有 sprite 时）
        if (_topicWeights.TryGetForcedInterestTopic(out TopicId forced))
        {
            for (int i = 0; i < valid.Count; i++)
            {
                if (valid[i].Topic == forced)
                {
                    _topicWeights.NotifyTopicPicked(valid[i].Topic);
                    return valid[i];
                }
            }
        }

        float totalWeight = 0f;
        var pickWeights = new float[valid.Count];
        for (int i = 0; i < valid.Count; i++)
        {
            pickWeights[i] = _topicWeights.GetPickWeight(valid[i].Topic);
            totalWeight += pickWeights[i];
        }

        if (totalWeight <= 0f)
            return valid[UnityEngine.Random.Range(0, valid.Count)];

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < valid.Count; i++)
        {
            roll -= pickWeights[i];
            if (roll <= 0f)
            {
                TopicSpriteEntry picked = valid[i];
                _topicWeights.NotifyTopicPicked(picked.Topic);
                return picked;
            }
        }

        TopicSpriteEntry fallback = valid[valid.Count - 1];
        _topicWeights.NotifyTopicPicked(fallback.Topic);
        return fallback;
    }

    Sprite PickSpriteExcludingRecent(Sprite[] sprites)
    {
        int count = sprites.Length;
        if (count == 1)
            return sprites[0];

        int maxAttempts = count * 2;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Sprite candidate = sprites[UnityEngine.Random.Range(0, count)];
            if (candidate != null && !IsRecentlyPicked(candidate))
                return candidate;
        }

        if (_recentSprites.Count > 0)
        {
            Sprite last = _recentSprites[_recentSprites.Count - 1];
            var candidates = new List<Sprite>(count);
            for (int i = 0; i < count; i++)
            {
                if (sprites[i] != null && sprites[i] != last)
                    candidates.Add(sprites[i]);
            }

            if (candidates.Count > 0)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        return null;
    }

    bool IsRecentlyPicked(Sprite sprite)
    {
        if (sprite == null || _recentSprites.Count == 0)
            return false;

        int historyCount = Mathf.Min(_recentHistorySize, _recentSprites.Count);
        int startIndex = _recentSprites.Count - historyCount;
        for (int i = startIndex; i < _recentSprites.Count; i++)
        {
            if (_recentSprites[i] == sprite)
                return true;
        }

        return false;
    }

    void RegisterSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        _recentSprites.Add(sprite);
        while (_recentSprites.Count > _recentHistorySize)
            _recentSprites.RemoveAt(0);
    }

}
