using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }
    public static event System.Action UndoCompleted;

    [SerializeField] private HandManager handManager;
    [SerializeField] private RevealPile revealPile;
    [SerializeField] private ActiveCardSlot activeCardSlot;
    [SerializeField] private CardDeck cardDeck;

    private enum MoveType { Draw, PileToHand, PlayToSlot, DeckRestart, WildCardSpawn }

    private struct PileCardSnapshot
    {
        public CardData data;
    }

    private struct UndoRecord
    {
        public MoveType moveType;
        public Card card;
        public CardData cardData;
        public int originIndex;
        public bool originWasPile;
        public int originSortOrder;
        public PileCardSnapshot[] pileSnapshot;
        public int preRestartDrawIndex;
        public CardData[] preRestartCards;
        public ConsumableButton sourceButton;
        public ConsumableButton.ConsumptionType consumptionType;
        public RectTransform spawnPoint;
    }

    private readonly Stack<UndoRecord> history = new();

    public bool CanUndo => history.Count > 0;

    private void Awake()
    {
        Instance = this;
        Debug.Log("[UndoManager] Awake — Instance set");
    }

    private void OnEnable()
    {
        HandManager.BeforeDeckRestart += OnBeforeDeckRestart;
    }

    private void OnDisable()
    {
        HandManager.BeforeDeckRestart -= OnBeforeDeckRestart;
    }

    public void RecordDraw(Card card)
    {
        var rec = new UndoRecord
        {
            moveType = MoveType.Draw,
            card = card,
            cardData = card.Data,
            originSortOrder = card.CurrentSortingOrder
        };
        history.Push(rec);
        Debug.Log($"[UndoManager] RecordDraw | card={card.name} data={card.Data?.gameId} | historySize={history.Count}");
    }

    public void RecordPileToHand(Card card, int pileIndex)
    {
        var rec = new UndoRecord
        {
            moveType = MoveType.PileToHand,
            card = card,
            cardData = card.Data,
            originIndex = pileIndex,
            originSortOrder = card.RestingSortOrder
        };
        history.Push(rec);
        Debug.Log($"[UndoManager] RecordPileToHand | card={card.name} pileIndex={pileIndex} | historySize={history.Count}");
    }

    public void RecordPlayToSlot(Card card)
    {
        bool fromPile = revealPile.IsCardInPile(card);
        int originIndex = fromPile
            ? revealPile.IndexOf(card)
            : handManager.IndexOfCard(card);

        var rec = new UndoRecord
        {
            moveType = MoveType.PlayToSlot,
            card = card,
            cardData = card.Data,
            originIndex = originIndex,
            originWasPile = fromPile,
            originSortOrder = card.RestingSortOrder
        };
        history.Push(rec);
        Debug.Log($"[UndoManager] RecordPlayToSlot | card={card.name} fromPile={fromPile} originIndex={originIndex} | historySize={history.Count}");
    }

    public void RecordWildCardSpawn(Card card, ConsumableButton sourceButton, RectTransform spawnPoint)
    {
        var rec = new UndoRecord
        {
            moveType = MoveType.WildCardSpawn,
            card = card,
            sourceButton = sourceButton,
            consumptionType = sourceButton != null ? sourceButton.LastConsumptionType : default,
            spawnPoint = spawnPoint
        };
        history.Push(rec);
        Debug.Log($"[UndoManager] RecordWildCardSpawn | card={card.name} consumptionType={rec.consumptionType} | historySize={history.Count}");
    }

    private void OnBeforeDeckRestart(IReadOnlyList<Card> pileCards)
    {
        var snapshot = new PileCardSnapshot[pileCards.Count];
        for (int i = 0; i < pileCards.Count; i++)
            snapshot[i] = new PileCardSnapshot { data = pileCards[i].Data };

        history.Push(new UndoRecord
        {
            moveType = MoveType.DeckRestart,
            pileSnapshot = snapshot,
            preRestartDrawIndex = cardDeck.DrawIndex,
            preRestartCards = cardDeck.GetCurrentCards()
        });
        Debug.Log($"[UndoManager] OnBeforeDeckRestart — recorded restart, pile={pileCards.Count} cards, drawIndex={cardDeck.DrawIndex} | historySize={history.Count}");
    }

    public void PerformUndo()
    {
        if (history.Count == 0)
        {
            Debug.Log("[UndoManager] PerformUndo blocked — history empty");
            return;
        }
        if (HandManager.IsAnimating)
        {
            Debug.Log("[UndoManager] PerformUndo blocked — IsAnimating");
            return;
        }
        if (MoveCounter.IsOutOfMoves)
        {
            Debug.Log("[UndoManager] PerformUndo blocked — out of moves");
            return;
        }

        UndoRecord record = history.Pop();
        string cardLabel = record.card != null ? record.card.name : $"(destroyed:{record.cardData?.gameId})";
        Debug.Log($"[UndoManager] PerformUndo | type={record.moveType} card={cardLabel} | historyRemaining={history.Count}");
        MoveCounter.Instance.SpendMove();
        StartCoroutine(ExecuteUndo(record));
    }

    private IEnumerator ExecuteUndo(UndoRecord record)
    {
        HandManager.IsAnimating = true;

        switch (record.moveType)
        {
            case MoveType.Draw:
                yield return StartCoroutine(UndoDraw(record));
                break;
            case MoveType.PileToHand:
                yield return StartCoroutine(UndoPileToHand(record));
                break;
            case MoveType.PlayToSlot:
                yield return StartCoroutine(UndoPlayToSlot(record));
                break;
            case MoveType.DeckRestart:
                yield return StartCoroutine(UndoDeckRestart(record));
                break;
            case MoveType.WildCardSpawn:
                yield return StartCoroutine(UndoWildCardSpawn(record));
                break;
        }

        HandManager.IsAnimating = false;
        Debug.Log($"[UndoManager] Undo complete | type={record.moveType}");
        UndoCompleted?.Invoke();
    }

    private IEnumerator UndoDraw(UndoRecord record)
    {
        Card card = record.card;
        Debug.Log($"[UndoManager] UndoDraw | card={card?.name} data={record.cardData?.gameId}");
        if (card == null)
        {
            Debug.Log("[UndoManager] UndoDraw — card ref null, skipping");
            yield break;
        }
        card.SetDraggable(false);

        RectTransform rt = card.GetComponent<RectTransform>();
        RectTransform container = handManager.CardContainer != null
            ? handManager.CardContainer
            : handManager.GetComponent<RectTransform>().root.GetComponent<RectTransform>();

        Vector2 deckPos = cardDeck.GetSpawnPosition(container);

        float undoStart = Time.realtimeSinceStartup;
        rt.DOKill();

        // Move starts immediately and runs in the background, mirroring how the
        // forward draw's hand-layout move tween overlaps with its flip.
        rt.DOAnchorPos(deckPos, CardAnimationSettings.Instance.MoveDuration).SetEase(Ease.Linear);
        AudioManager.Instance?.PlayDealSound();

        yield return new WaitForSeconds(CardAnimationSettings.Instance.MoveDuration * CardAnimationSettings.Instance.FlipStartPercent);

        yield return rt.DOScaleX(0f, CardAnimationSettings.Instance.FlipHalfDuration).SetEase(Ease.Linear).WaitForCompletion();

        card.ShowBack(cardDeck.RemainingCount + 1);

        yield return rt.DOScaleX(1f, CardAnimationSettings.Instance.FlipHalfDuration).SetEase(Ease.Linear).WaitForCompletion();
        float afterFlipIn = Time.realtimeSinceStartup;

        // Make sure the move tween has actually reached deckPos before we destroy the card.
        float remainingMove = CardAnimationSettings.Instance.MoveDuration - (afterFlipIn - undoStart);
        if (remainingMove > 0f) yield return new WaitForSeconds(remainingMove);

        revealPile.RemoveCardSilently(card);
        Destroy(card.gameObject);
        cardDeck.UndrawLast();
        cardDeck.UpdateDeckVisual();
        Debug.Log("[UndoManager] UndoDraw done — card destroyed, deck decremented");
    }

    private IEnumerator UndoPileToHand(UndoRecord record)
    {
        Card card = record.card;
        Debug.Log($"[UndoManager] UndoPileToHand | card={card.name} restoreToIndex={record.originIndex}");
        card.SetSortingOrder(record.originSortOrder);
        handManager.RemoveCardFromHand(card);
        revealPile.InsertCardAt(card, record.originIndex);
        Debug.Log("[UndoManager] UndoPileToHand done");
        yield return null;
    }

    private IEnumerator UndoPlayToSlot(UndoRecord record)
    {
        Card card = record.card;
        Debug.Log($"[UndoManager] UndoPlayToSlot | card={card.name} originWasPile={record.originWasPile} originIndex={record.originIndex}");
        activeCardSlot.RemoveTopCard();
        card.SetSortingOrder(record.originSortOrder);

        if (record.originWasPile)
        {
            revealPile.InsertCardAt(card, record.originIndex);
            Debug.Log($"[UndoManager] UndoPlayToSlot — returned to pile at index {record.originIndex}");
        }
        else
        {
            handManager.InsertCardAtHand(card, record.originIndex);
            Debug.Log($"[UndoManager] UndoPlayToSlot — returned to hand at index {record.originIndex}");
        }

        yield return null;
    }

    private IEnumerator UndoWildCardSpawn(UndoRecord record)
    {
        Card card = record.card;
        Debug.Log($"[UndoManager] UndoWildCardSpawn | card={card?.name} consumptionType={record.consumptionType}");
        if (card != null)
        {
            card.SetDraggable(false);
            handManager.RemoveCardFromHand(card);

            RectTransform rt = card.GetComponent<RectTransform>();
            RectTransform container = handManager.CardContainer != null
                ? handManager.CardContainer
                : handManager.GetComponent<RectTransform>().root.GetComponent<RectTransform>();

            rt.DOKill();
            if (record.spawnPoint != null)
            {
                Vector2 targetPos = container.InverseTransformPoint(record.spawnPoint.position);
                AudioManager.Instance?.PlayDealSound();
                yield return rt.DOAnchorPos(targetPos, CardAnimationSettings.Instance.MoveDuration).SetEase(Ease.Linear).WaitForCompletion();
            }

            Destroy(card.gameObject);
        }
        record.sourceButton?.Refund(record.consumptionType);
    }

    private IEnumerator UndoDeckRestart(UndoRecord record)
    {
        Debug.Log($"[UndoManager] UndoDeckRestart — restoring deck (drawIndex={record.preRestartDrawIndex}), recreating {record.pileSnapshot.Length} pile cards");

        cardDeck.RestoreState(record.preRestartCards, record.preRestartDrawIndex);

        RectTransform container = handManager.CardContainer;
        Vector2 deckPos = cardDeck.GetSpawnPosition(container);

        var dataToNewCard = new Dictionary<CardData, Card>();
        for (int i = 0; i < record.pileSnapshot.Length; i++)
        {
            CardData data = record.pileSnapshot[i].data;
            Card newCard = handManager.CreateCardFromData(data, deckPos);
            revealPile.InsertCardAt(newCard, i);
            dataToNewCard[data] = newCard;
            Debug.Log($"[UndoManager] UndoDeckRestart — recreated pile card [{i}] {data?.gameId}");
        }

        UndoRecord[] all = history.ToArray(); // index 0 = top (newest)
        history.Clear();
        int patched = 0;
        for (int i = all.Length - 1; i >= 0; i--) // push oldest first to preserve order
        {
            UndoRecord rec = all[i];
            if (rec.moveType == MoveType.Draw && rec.card == null && rec.cardData != null
                && dataToNewCard.TryGetValue(rec.cardData, out Card newCard))
            {
                rec.card = newCard;
                patched++;
                history.Push(rec);
            }
            else
            {
                history.Push(rec);
            }
        }

        Debug.Log($"[UndoManager] UndoDeckRestart — patched {patched} Draw records with new card refs | historySize={history.Count}");
        yield return null;
    }
}
