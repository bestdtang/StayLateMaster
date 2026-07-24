using UnityEngine;

/// <summary>
/// 滑卡判定，并更新权重、快乐、连击。
/// </summary>
public class SwipeFilterHandler : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] InterestManager _interestManager;
    [SerializeField] TopicWeights _topicWeights;
    [SerializeField] ComboStreakCounter _comboStreakCounter;
    [SerializeField] HappinessMeter _happinessMeter;
    [SerializeField] FatigueMeter _fatigueMeter;
    [SerializeField] RunController _runController;
    [SerializeField] HotTimeController _hotTimeController;

    float happinessCorrectInterestRight = 8f;
    float happinessWrongBase = 3f;
    float happinessWrongFatigueScale = 0.02f;

    void Awake()
    {
        if (_interestManager == null)
            _interestManager = FindObjectOfType<InterestManager>();

        if (_topicWeights == null)
            _topicWeights = FindObjectOfType<TopicWeights>();

        if (_comboStreakCounter == null)
            _comboStreakCounter = FindObjectOfType<ComboStreakCounter>();

        if (_happinessMeter == null)
            _happinessMeter = FindObjectOfType<HappinessMeter>();

        if (_fatigueMeter == null)
            _fatigueMeter = FindObjectOfType<FatigueMeter>();

        if (_runController == null)
            _runController = FindObjectOfType<RunController>();

        if (_hotTimeController == null)
            _hotTimeController = FindObjectOfType<HotTimeController>();
    }

    public void ApplyBalance(GameBalanceConfig config)
    {
        if (config == null)
            return;

        happinessCorrectInterestRight = config.HappinessCorrectInterestRight;
        happinessWrongBase = config.HappinessWrongBase;
        happinessWrongFatigueScale = config.HappinessWrongFatigueScale;
        _topicWeights?.ApplyBalance(config);
    }

    public bool ProcessSwipe(PickedCard card, SwipeDirection direction)
    {
        GameState state = _runController != null ? _runController.CurrentState : GameState.Playing;

        // 火热时间：推荐流全是当前兴趣，右滑=正确固定加快乐，左滑无事发生；不动权重/连击/兴趣
        if (state == GameState.HotStreak)
        {
            bool hotCorrect = direction == SwipeDirection.Right;
            _hotTimeController?.RegisterHotSwipe(hotCorrect);
            return hotCorrect;
        }

        if (state != GameState.Playing)
            return true;

        if (_interestManager == null)
            return true;

        TopicId interest = _interestManager.CurrentTopic;
        bool wasCorrect = IsCorrectSwipe(card.Topic, interest, direction);

        _topicWeights?.ApplySwipe(card.Topic, interest, direction);
        _comboStreakCounter?.RegisterSwipe(wasCorrect);
        ApplyHappiness(card.Topic, interest, direction, wasCorrect);

        // 达到连续正确阈值则触发火热时间
        if (wasCorrect)
            _hotTimeController?.RegisterCorrectSwipe();

        return wasCorrect;
    }

    public static bool IsCorrectSwipe(TopicId cardTopic, TopicId interest, SwipeDirection direction)
    {
        bool isInterest = cardTopic == interest;
        if (isInterest && direction == SwipeDirection.Right)
            return true;

        if (!isInterest && direction == SwipeDirection.Left)
            return true;

        return false;
    }

    void ApplyHappiness(TopicId cardTopic, TopicId interest, SwipeDirection direction, bool wasCorrect)
    {
        if (_happinessMeter == null)
            return;

        if (wasCorrect)
        {
            if (cardTopic == interest && direction == SwipeDirection.Right)
                _happinessMeter.Add(happinessCorrectInterestRight);

            return;
        }

        float fatigue = _fatigueMeter != null ? _fatigueMeter.Value : 0f;
        float penalty = happinessWrongBase + fatigue * happinessWrongFatigueScale;
        _happinessMeter.Subtract(penalty);
    }
}
