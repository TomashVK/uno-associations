using UnityEngine;

public class HandDropZone : MonoBehaviour, ICardDrop
{
    public static event System.Action CardTakenToHand;

    [SerializeField] private RevealPile revealPile;
    [SerializeField] private HandManager handManager;

    public bool OnCardDrop(Card card)
    {
        if (MoveCounter.IsOutOfMoves) return false;
        if (!revealPile.IsCardInPile(card)) return false;
        if (!handManager.CanAcceptCard()) return false;
        UndoManager.Instance?.RecordPileToHand(card, revealPile.IndexOf(card));
        handManager.AddCardFromRevealPile(card);
        CardTakenToHand?.Invoke();
        return true;
    }
}
