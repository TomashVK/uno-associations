using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Solo.MOST_IN_ONE;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    public static event System.Action CardLeftHand;
    public static event System.Action DeckRestarted;
    public static event System.Action<IReadOnlyList<Card>> BeforeDeckRestart;

    [SerializeField] private int maxHandSize;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private CardDeck cardDeck;
    [SerializeField] private float cardSpacing = 80f;
    [SerializeField] private float margin = 50f;
    [SerializeField] private RevealPile revealPile;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private GameObject handSwipeArea;

    private readonly List<Card> handCards = new();
    private readonly HashSet<Card> draggedFromHand = new();
    private bool isDrawing = false;
    private bool isDealing = false;
    private LinearCardLayout layout;
    private float handScrollOffset;

    public static bool IsAnimating { get; set; }

    public int CardCount => handCards.Count;
    public RectTransform CardContainer => cardContainer;

    public CardData[] GetHandCardData()
    {
        var data = new CardData[handCards.Count];
        for (int i = 0; i < handCards.Count; i++) data[i] = handCards[i].Data;
        return data;
    }

    // Plays the same spawn-from-deck flip-in animation used for a normal deal/draw,
    // but for a card whose data is already known (e.g. restoring a save) rather than
    // one pulled fresh off the deck — so a resumed level looks identical to a fresh one.
    public void AnimateCardIn(CardData data, System.Action<Card> onPlaced, System.Action onDone, float delay = 0f)
    {
        StartCoroutine(AnimateCardInRoutine(data, delay, onPlaced, onDone));
    }

    private IEnumerator AnimateCardInRoutine(CardData data, float delay, System.Action<Card> onPlaced, System.Action onDone)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        yield return StartCoroutine(PlayDealAnimation(data, cardDeck.RemainingCount, onPlaced, null, onDone));
    }

    private void Awake()
    {
        layout = new LinearCardLayout(transform, cardSpacing, margin, centerOnSafeArea: true);
    }

    private void OnEnable()
    {
        CardDeck.Tapped += DrawCard;
        Card.Dropped += OnCardDropped;
        Card.SnapBacked += OnCardSnapBacked;
        Card.DragPickedUp += OnCardDragPickedUp;
        HandSwipeArea.Scrolled += OnHandScrolled;
    }

    private void OnDisable()
    {
        CardDeck.Tapped -= DrawCard;
        Card.Dropped -= OnCardDropped;
        Card.SnapBacked -= OnCardSnapBacked;
        Card.DragPickedUp -= OnCardDragPickedUp;
        HandSwipeArea.Scrolled -= OnHandScrolled;
    }

    private void OnCardDropped(Card card)
    {
        if (!draggedFromHand.Remove(card)) return;
        handCards.Remove(card);
        UpdateCardPositions();
        CardLeftHand?.Invoke();
    }

    private void OnCardSnapBacked(Card card)
    {
        draggedFromHand.Remove(card);
        if (handCards.Contains(card))
            UpdateCardPositions();
    }

    private void OnCardDragPickedUp(Card card)
    {
        if (handCards.Contains(card)) draggedFromHand.Add(card);
    }

    public IEnumerator DealInitial(int handSize, ActiveCardSlot activeSlot)
    {
        isDealing = true;
        IsAnimating = true;

        int total = 1 + handSize;
        int completed = 0;

        StartCoroutine(DealCard(activeSlot.ReceiveCard, 0f, () => completed++));
        for (int i = 0; i < handSize; i++)
        {
            int captured = i;
            StartCoroutine(DealCard(card =>
            {
                handCards.Add(card);
                UpdateCardPositions();
            }, (captured + 1) * CardAnimationSettings.Instance.DealStagger, () => completed++));
        }

        yield return new WaitUntil(() => completed >= total);
        isDealing = false;
        IsAnimating = false;
    }

    private IEnumerator DealCard(System.Action<Card> onPlaced, float delay, System.Action onDone)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        int countBeforeDraw = cardDeck.RemainingCount;
        CardData data = cardDeck.DrawNext();
        cardDeck.UpdateDeckVisual();

        yield return StartCoroutine(PlayDealAnimation(data, countBeforeDraw, onPlaced, null, onDone));
    }

    // Shared spawn-from-deck flip-in animation: spawn the card back at the deck,
    // flip to reveal the face partway through the move, then finish unfolding.
    // Used by fresh deals/draws (data pulled from the deck) and by save restore
    // (data already known) so both look identical.
    private IEnumerator PlayDealAnimation(CardData data, int backCount, System.Action<Card> onPlaced, System.Action<Card> onRevealed, System.Action onDone)
    {
        RectTransform container = cardContainer != null ? cardContainer : transform.root.GetComponent<RectTransform>();
        Vector2 spawnPos = cardDeck.GetSpawnPosition(container);
        GameObject newCardObj = Instantiate(PrefabFor(data), container);
        RectTransform newCardRT = newCardObj.GetComponent<RectTransform>();
        newCardRT.anchoredPosition = spawnPos;
        newCardRT.localEulerAngles = cardDeck.transform.localEulerAngles;

        Card card = newCardObj.GetComponent<Card>();
        card.SetHorizontal(true);
        card.ShowBack(backCount);

        onPlaced?.Invoke(card);
        AudioManager.Instance?.PlayDealSound();

        yield return new WaitForSeconds(CardAnimationSettings.Instance.MoveDuration * CardAnimationSettings.Instance.FlipStartPercent);
        yield return newCardObj.transform.DOScaleX(0f, CardAnimationSettings.Instance.FlipHalfDuration).SetEase(Ease.Linear).WaitForCompletion();

        card.HideBack();
        card.Init(data);
        onRevealed?.Invoke(card);

        yield return newCardObj.transform.DOScaleX(1f, CardAnimationSettings.Instance.FlipHalfDuration).SetEase(Ease.Linear).WaitForCompletion();

        onDone?.Invoke();
    }

    public bool CanAcceptCard() => handCards.Count < maxHandSize;

    public void AddCardFromRevealPile(Card card)
    {
        handCards.Add(card);
        UpdateCardPositions();
    }

    public void RemoveCardFromHand(Card card)
    {
        handCards.Remove(card);
        UpdateCardPositions();
    }

    public void InsertCardAtHand(Card card, int index)
    {
        index = Mathf.Clamp(index, 0, handCards.Count);
        handCards.Insert(index, card);
        UpdateCardPositions();
    }

    public int IndexOfCard(Card card) => handCards.IndexOf(card);

    public Card CreateCardFromData(CardData data, Vector2 spawnPos)
    {
        RectTransform container = cardContainer != null ? cardContainer : transform.root.GetComponent<RectTransform>();
        GameObject newCardObj = Instantiate(PrefabFor(data), container);
        newCardObj.GetComponent<RectTransform>().anchoredPosition = spawnPos;
        Card card = newCardObj.GetComponent<Card>();
        card.Init(data);
        card.SetHorizontal(true);
        return card;
    }

    // A wild card's distinct look comes entirely from its prefab (Card.Init only sets
    // text), so any code that recreates a card from saved/recorded data must pick the
    // same prefab WildCardButton uses, not the regular cardPrefab.
    private GameObject PrefabFor(CardData data) =>
        data.isWild && WildCardButton.Instance != null ? WildCardButton.Instance.WildCardPrefab : cardPrefab;

    private void DrawCard()
    {
        if (GameSettings.VibrationEnabled) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);
        if (isDrawing || isDealing) return;
        if (cardDeck == null) return;
        if (MoveCounter.IsOutOfMoves) return;
        if (!cardDeck.HasCards)
        {
            BeforeDeckRestart?.Invoke(revealPile.PileCards);
            var pileData = new List<CardData>();
            foreach (Card c in revealPile.PileCards) pileData.Add(c.Data);
            revealPile.ClearPile();
            cardDeck.RestartDeck(pileData);
            DeckRestarted?.Invoke();
            return;
        }
        StartCoroutine(DrawCardToRevealPile());
    }

    private IEnumerator DrawCardToRevealPile()
    {
        yield return StartCoroutine(DrawTopCard(card => revealPile.ReceiveCard(card)));
    }

    private IEnumerator DrawTopCard(System.Action<Card> onPlaced)
    {
        isDrawing = true;
        IsAnimating = true;

        int countBeforeDraw = cardDeck.RemainingCount;
        CardData data = cardDeck.DrawNext();
        cardDeck.UpdateDeckVisual();

        yield return StartCoroutine(PlayDealAnimation(data, countBeforeDraw, onPlaced, card => UndoManager.Instance?.RecordDraw(card), null));

        isDrawing = false;
        IsAnimating = false;
    }

    private void OnHandScrolled(float delta)
    {
        layout.ScrollOffset = handScrollOffset + delta;
        layout.PlaceCards(handCards, instant: true);
        handScrollOffset = layout.ScrollOffset;
    }

    private void UpdateCardPositions()
    {
        layout.ScrollOffset = handScrollOffset;
        layout.PlaceCards(handCards);
        RefreshSwipeAreaVisibility();
    }

    private void RefreshSwipeAreaVisibility()
    {
        if (handSwipeArea != null) handSwipeArea.SetActive(layout.CanScroll);
    }
}
