using UnityEngine;

/// <summary>
/// 维护各 Topic 推荐权重；训练权重 × 当前兴趣亲和倍率，并用硬保底最大间隔确保当前兴趣短时间必现。
/// </summary>
public class TopicWeights : MonoBehaviour
{
    const int TopicCount = 5;
    const float MaxStoredWeight = 3f;

    [System.Serializable]
    struct TopicWeightSlot
    {
        public TopicId Topic;
        public float TrainedWeight;
        public float PickWeight;
    }

    [Header("System References")]
    [Tooltip("Current interest source; changes never clear trained weights.")]
    [SerializeField] InterestManager _interestManager;

    [Header("Topic Weights (Runtime)")]
    [Tooltip("TrainedWeight per Topic; PickWeight = trained x interest affinity.")]
    [SerializeField] TopicWeightSlot[] _topicSlots =
    {
        new TopicWeightSlot { Topic = TopicId.Pets, TrainedWeight = 1f, PickWeight = 1f },
        new TopicWeightSlot { Topic = TopicId.People, TrainedWeight = 1f, PickWeight = 1f },
        new TopicWeightSlot { Topic = TopicId.Foods, TrainedWeight = 1f, PickWeight = 1f },
        new TopicWeightSlot { Topic = TopicId.Cars, TrainedWeight = 1f, PickWeight = 1f },
        new TopicWeightSlot { Topic = TopicId.Football, TrainedWeight = 1f, PickWeight = 1f },
    };

    [Header("Interest Guarantee (Runtime)")]
    [Tooltip("Current interest Topic getting the affinity multiplier.")]
    [SerializeField] TopicId _currentInterest;
    [Tooltip("Cards drawn since the current interest last appeared.")]
    [SerializeField] int _cardsSinceInterest;

    float minWeight = 0.25f;
    float correctInterestRightDelta = 0.35f;
    float correctSkipLeftDelta = -0.08f;
    float wrongInterestLeftDelta = -0.25f;
    float wrongAcceptRightDelta = 0.15f;
    float interestAffinity = 4f;
    int interestGuaranteeGap = 3;

    public void Reset()
    {
        EnsureSlotCount();
        for (int i = 0; i < TopicCount; i++)
            SetTrainedWeightAt(i, 1f);

        _cardsSinceInterest = 0;
        SyncPickWeights();
    }

    public void ApplyBalance(GameBalanceConfig config)
    {
        if (config == null)
            return;

        minWeight = config.MinTopicWeight;
        correctInterestRightDelta = config.CorrectInterestRightWeightDelta;
        correctSkipLeftDelta = config.CorrectSkipLeftWeightDelta;
        wrongInterestLeftDelta = config.WrongInterestLeftWeightDelta;
        wrongAcceptRightDelta = config.WrongAcceptRightWeightDelta;
        interestAffinity = config.InterestAffinity;
        interestGuaranteeGap = config.InterestGuaranteeGap;
        SyncPickWeights();
    }

    public float GetPickWeight(TopicId topic)
    {
        int index = (int)topic;
        if (index < 0 || index >= TopicCount)
            return minWeight;

        return GetPickWeightAt(index);
    }

    public void ApplySwipe(TopicId cardTopic, TopicId currentInterest, SwipeDirection direction)
    {
        bool isInterest = cardTopic == currentInterest;

        if (isInterest && direction == SwipeDirection.Right)
            AdjustWeight(cardTopic, correctInterestRightDelta);
        else if (!isInterest && direction == SwipeDirection.Left)
            AdjustWeight(cardTopic, correctSkipLeftDelta);
        else if (isInterest && direction == SwipeDirection.Left)
            AdjustWeight(cardTopic, wrongInterestLeftDelta);
        else if (!isInterest && direction == SwipeDirection.Right)
            AdjustWeight(cardTopic, wrongAcceptRightDelta);
    }

    public void NotifyInterestChanged(TopicId newInterest)
    {
        _currentInterest = newInterest;
        _cardsSinceInterest = 0;
        SyncPickWeights();
    }

    /// <summary>硬保底：无兴趣间隔达到阈值时强制下一张为当前兴趣。</summary>
    public bool TryGetForcedInterestTopic(out TopicId topic)
    {
        topic = _currentInterest;
        return interestGuaranteeGap > 0 && _cardsSinceInterest >= interestGuaranteeGap;
    }

    public void NotifyTopicPicked(TopicId pickedTopic)
    {
        if (pickedTopic == _currentInterest)
            _cardsSinceInterest = 0;
        else
            _cardsSinceInterest++;
    }

    void AdjustWeight(TopicId topic, float delta)
    {
        int index = (int)topic;
        if (index < 0 || index >= TopicCount)
            return;

        float next = GetTrainedWeightAt(index) + delta;
        next = Mathf.Clamp(next, minWeight, MaxStoredWeight);
        SetTrainedWeightAt(index, next);
        SyncPickWeights();
    }

    void EnsureSlotCount()
    {
        if (_topicSlots != null && _topicSlots.Length == TopicCount)
            return;

        TopicWeightSlot[] previous = _topicSlots;
        _topicSlots = new TopicWeightSlot[TopicCount];
        for (int i = 0; i < TopicCount; i++)
        {
            float trained = 1f;
            if (previous != null && i < previous.Length)
                trained = previous[i].TrainedWeight;

            _topicSlots[i] = new TopicWeightSlot
            {
                Topic = (TopicId)i,
                TrainedWeight = trained,
                PickWeight = trained,
            };
        }
    }

    float GetTrainedWeightAt(int index)
    {
        EnsureSlotCount();
        return _topicSlots[index].TrainedWeight;
    }

    float GetPickWeightAt(int index)
    {
        EnsureSlotCount();
        TopicWeightSlot slot = _topicSlots[index];
        float value = slot.TrainedWeight;
        if (slot.Topic == _currentInterest)
            value *= interestAffinity;

        return Mathf.Max(minWeight, value);
    }

    void SetTrainedWeightAt(int index, float value)
    {
        EnsureSlotCount();
        TopicWeightSlot slot = _topicSlots[index];
        slot.TrainedWeight = value;
        _topicSlots[index] = slot;
    }

    void SyncPickWeights()
    {
        EnsureSlotCount();
        for (int i = 0; i < TopicCount; i++)
        {
            TopicWeightSlot slot = _topicSlots[i];
            slot.PickWeight = GetPickWeightAt(i);
            _topicSlots[i] = slot;
        }
    }

    void OnEnable()
    {
        EnsureSlotCount();
        SyncPickWeights();

        if (_interestManager == null)
            _interestManager = FindObjectOfType<InterestManager>();

        if (_interestManager != null)
            _interestManager.OnInterestChanged += NotifyInterestChanged;
    }

    void OnDisable()
    {
        if (_interestManager != null)
            _interestManager.OnInterestChanged -= NotifyInterestChanged;
    }
}
