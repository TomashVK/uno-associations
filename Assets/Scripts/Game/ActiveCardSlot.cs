using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ActiveCardSlot : MonoBehaviour, ICardDrop
{
    public static event System.Action CardPlayed;

    [SerializeField] private Vector2 dropOffset;
    [SerializeField] private float rotationMin = -15f;
    [SerializeField] private float rotationMax = 15f;

    private ConnectionGraph graph;
    private readonly List<Card> cardStack = new();

    private Card ActiveCard => cardStack.Count > 0 ? cardStack[^1] : null;

    public void Init(ConnectionGraph connectionGraph)
    {
        graph = connectionGraph;
    }

    public void ReceiveCard(Card card)
    {
        cardStack.Add(card);
        PlaceCard(card, cardStack.Count);
    }

    public CardData[] GetStackCardData()
    {
        var data = new CardData[cardStack.Count];
        for (int i = 0; i < cardStack.Count; i++) data[i] = cardStack[i].Data;
        return data;
    }

    public bool OnCardDrop(Card card)
    {
        if (MoveCounter.IsOutOfMoves) return false;
        if (ActiveCard == null) return false;
        if (graph == null) return false;
        bool isJoker = card.Data.isWild || ActiveCard.Data.isWild;
        if (!isJoker && !graph.CanPlay(ActiveCard.Data.gameId, card.Data.gameId)) return false;

        UndoManager.Instance?.RecordPlayToSlot(card);

        cardStack.Add(card);
        PlaceCard(card, cardStack.Count);

        CardPlayed?.Invoke();
        return true;
    }

    public Card RemoveTopCard()
    {
        if (cardStack.Count == 0) return null;
        Card top = cardStack[^1];
        cardStack.RemoveAt(cardStack.Count - 1);
        return top;
    }

    private void PlaceCard(Card card, int sortOrder)
    {
        RectTransform rt = card.GetComponent<RectTransform>();
        RectTransform slotRT = GetComponent<RectTransform>();
        float angle = sortOrder == 1 ? 0f : Random.Range(rotationMin, rotationMax);
        rt.DOKill();
        rt.DOAnchorPos((Vector2)slotRT.localPosition + dropOffset, 0.25f);
        rt.DOLocalRotate(new Vector3(0f, 0f, angle), 0.25f);
        card.SetHorizontal(true);
        card.SetSortingOrder(sortOrder);
    }
}
