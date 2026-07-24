using UnityEngine;

/// <summary>
/// 运行时抽到的单张卡（无 per-card SO）。
/// </summary>
public struct PickedCard
{
    public TopicId Topic;
    public Sprite Sprite;

    public PickedCard(TopicId topic, Sprite sprite)
    {
        Topic = topic;
        Sprite = sprite;
    }
}
