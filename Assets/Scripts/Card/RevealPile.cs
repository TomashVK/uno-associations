using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class RevealPile : MonoBehaviour
{
    public static event System.Action CardDrawnToRevealPile;
    public static event System.Action PileChanged;

    private readonly List<Card> pileCards = new();

    public bool HasCards => pileCards.Count > 0;
    public bool IsCardInPile(Card card) => pileCards.Contains(card);
    public IReadOnlyList<Card> PileCards => pileCards;

    public CardData[] GetPileCardData()
    {
        var data = new CardData[pileCards.Count];
        for (int i = 0; i < pileCards.Count; i++) data[i] = pileCards[i].Data;
        return data;
    }

    private void OnEnable()
    {
        Card.Dropped += OnCardDropped;
        Card.SnapBacked += OnCardSnapBacked;
    }

    private void OnDisable()
    {
        Card.Dropped -= OnCardDropped;
        Card.SnapBacked -= OnCardSnapBacked;
    }

    public void ClearPile()
    {
        foreach (Card card in pileCards)
            if (card != null) Destroy(card.gameObject);
        pileCards.Clear();
        PileChanged?.Invoke();
    }

    public void ReceiveCard(Card card)
    {
        pileCards.Add(card);
        UpdateDraggability();
        UpdateCardPositions();
        CardDrawnToRevealPile?.Invoke();
        PileChanged?.Invoke();
    }

    public void InsertCardAt(Card card, int index)
    {
        index = Mathf.Clamp(index, 0, pileCards.Count);
        pileCards.Insert(index, card);
        UpdateDraggability();
        UpdateCardPositions();
        PileChanged?.Invoke();
    }

    public int IndexOf(Card card) => pileCards.IndexOf(card);

    public void RemoveCardSilently(Card card)
    {
        if (!pileCards.Remove(card)) return;
        UpdateDraggability();
        UpdateCardPositions();
        PileChanged?.Invoke();
    }

    private void OnCardDropped(Card card)
    {
        if (!pileCards.Remove(card)) return;
        UpdateDraggability();
        UpdateCardPositions();
        PileChanged?.Invoke();
    }

    private void OnCardSnapBacked(Card card)
    {
        if (pileCards.Contains(card))
            UpdateCardPositions();
    }

    private void UpdateDraggability()
    {
        int count = pileCards.Count;
        for (int i = 0; i < count; i++)
            pileCards[i].SetDraggable(i == count - 1);
    }

    private void UpdateCardPositions()
    {
        if (pileCards.Count == 0) return;

        RectTransform anchorRT = transform as RectTransform;
        RectTransform cardParent = pileCards[0].GetComponent<RectTransform>().parent as RectTransform;
        Vector2 stackPos = cardParent != null
            ? (Vector2)cardParent.InverseTransformPoint(anchorRT.position)
            : (Vector2)anchorRT.localPosition;

        for (int i = 0; i < pileCards.Count; i++)
        {
            if (pileCards[i].IsDragging) continue;
            RectTransform rt = pileCards[i].GetComponent<RectTransform>();
            string tweenId = "pile:" + rt.GetInstanceID();
            DOTween.Kill(tweenId);
            rt.DOAnchorPos(stackPos, 0.25f).SetId(tweenId);
            rt.DOLocalRotateQuaternion(Quaternion.identity, 0.25f).SetId(tweenId);
            pileCards[i].SetSortingOrder(i + 1);
        }
    }
}
