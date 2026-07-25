using System;
using UnityEngine;

/// <summary>
/// 实例化卡片 prefab，滑走时立刻生成下一张，旧卡飞出后销毁。
/// </summary>
public class FeedSpawner : MonoBehaviour
{
    [Header("Prefab & Container")]
    [Tooltip("Card prefab with CardView; instantiated per swipe.")]
    [SerializeField] CardView cardPrefab;
    [Tooltip("Parent inside RectMask2D; spawned cards are parented here.")]
    [SerializeField] RectTransform cardSpawnRoot;

    [Header("System References")]
    [Tooltip("Picks the next Topic + Sprite from CardSpriteLibrary.")]
    [SerializeField] CardPicker cardPicker;
    [Tooltip("Listens for Left/Right arrow swipes.")]
    [SerializeField] SwipeInput swipeInput;
    [Tooltip("Swipe judgment, weights, happiness, and combo.")]
    [SerializeField] SwipeFilterHandler swipeFilterHandler;
    [Tooltip("Run lifecycle; auto-found if empty.")]
    [SerializeField] RunController runController;

    CardView currentCard;
    bool isActive;

    public CardView CurrentCardView => currentCard;

    public event Action<PickedCard, SwipeDirection> OnCardSwiped;
    public event Action<bool> OnSwipeJudged;

    void Awake()
    {
        if (cardPicker == null)
            cardPicker = GetComponent<CardPicker>();

        if (cardPicker == null)
            cardPicker = FindObjectOfType<CardPicker>();

        if (swipeInput == null)
            swipeInput = FindObjectOfType<SwipeInput>();

        if (swipeFilterHandler == null)
            swipeFilterHandler = GetComponent<SwipeFilterHandler>();

        if (swipeFilterHandler == null)
            swipeFilterHandler = FindObjectOfType<SwipeFilterHandler>();

        if (runController == null)
            runController = FindObjectOfType<RunController>();
    }

    void OnEnable()
    {
        if (swipeInput != null)
            swipeInput.OnSwipe += HandleSwipe;

        if (runController != null)
            runController.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        if (swipeInput != null)
            swipeInput.OnSwipe -= HandleSwipe;

        if (runController != null)
            runController.OnStateChanged -= HandleStateChanged;
    }

    public void ResetAndSpawn()
    {
        isActive = true;
        ClearSpawnedCards();
        cardPicker?.ResetHistory();
        swipeInput?.SetInputEnabled(true);
        currentCard = SpawnCard();
    }

    public void StopFeed()
    {
        isActive = false;
        swipeInput?.SetInputEnabled(false);
        ClearSpawnedCards();
        currentCard = null;
    }

    void HandleStateChanged(GameState state)
    {
        if (state == GameState.Win || state == GameState.Lose)
            StopFeed();
    }

    void HandleSwipe(SwipeDirection direction)
    {
        if (!isActive || currentCard == null)
            return;

        CardView outgoing = currentCard;
        PickedCard swipedCard = outgoing.Data;

        // 立刻生成下一张，屏幕不留空
        currentCard = SpawnCard();
        if (currentCard != null)
            currentCard.transform.SetAsFirstSibling();

        outgoing.transform.SetAsLastSibling();
        OnCardSwiped?.Invoke(swipedCard, direction);

        bool wasCorrect = swipeFilterHandler != null
            ? swipeFilterHandler.ProcessSwipe(swipedCard, direction)
            : true;

        OnSwipeJudged?.Invoke(wasCorrect);

        // 触发火热时，下一张已在 SetForcedTopic 之前生成，需立刻换成当前兴趣卡
        if (runController != null && runController.CurrentState == GameState.HotStreak)
            ReplaceCurrentCardForHotTime();

        outgoing.PlayDismiss(direction, wasCorrect, () =>
        {
            if (outgoing != null)
                Destroy(outgoing.gameObject);
        });
    }

    CardView SpawnCard()
    {
        if (!isActive)
        {
            Debug.LogWarning("[FeedSpawner] 未激活，跳过生成卡片。");
            return null;
        }

        if (cardPrefab == null)
        {
            Debug.LogWarning("[FeedSpawner] 未设置 Card Prefab。");
            return null;
        }

        if (cardSpawnRoot == null)
        {
            Debug.LogWarning("[FeedSpawner] 未设置 Card Spawn Root。");
            return null;
        }

        if (cardPicker == null)
        {
            Debug.LogWarning("[FeedSpawner] 未找到 CardPicker。");
            return null;
        }

        if (!cardPicker.TryPickNext(out PickedCard picked))
            return null;

        CardView instance = Instantiate(cardPrefab, cardSpawnRoot);
        RectTransform rect = instance.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        instance.Bind(picked);
        return instance;
    }

    void ReplaceCurrentCardForHotTime()
    {
        if (!isActive)
            return;

        if (currentCard != null)
            Destroy(currentCard.gameObject);

        currentCard = SpawnCard();
        if (currentCard != null)
            currentCard.transform.SetAsFirstSibling();
    }

    void ClearSpawnedCards()
    {
        if (cardSpawnRoot == null)
            return;

        for (int i = cardSpawnRoot.childCount - 1; i >= 0; i--)
            Destroy(cardSpawnRoot.GetChild(i).gameObject);
    }
}
