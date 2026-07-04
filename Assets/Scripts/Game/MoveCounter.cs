using UnityEngine;

public class MoveCounter : MonoBehaviour
{
    public static event System.Action MovesChanged;
    public static event System.Action MovesExhausted;

    public static MoveCounter Instance { get; private set; }

    public int MovesRemaining { get; private set; }
    public int TotalMovesSpent { get; private set; }

    public static bool IsOutOfMoves => Instance != null && Instance.MovesRemaining <= 0;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        RevealPile.CardDrawnToRevealPile += OnMoveSpent;
        HandDropZone.CardTakenToHand += OnMoveSpent;
        ActiveCardSlot.CardPlayed += OnMoveSpent;
        HandManager.DeckRestarted += OnMoveSpent;
        WildCardButton.WildCardSpawned += OnMoveSpent;
    }

    private void OnDisable()
    {
        RevealPile.CardDrawnToRevealPile -= OnMoveSpent;
        HandDropZone.CardTakenToHand -= OnMoveSpent;
        ActiveCardSlot.CardPlayed -= OnMoveSpent;
        HandManager.DeckRestarted -= OnMoveSpent;
        WildCardButton.WildCardSpawned -= OnMoveSpent;
    }

    public void Init(int maxMoves)
    {
        MovesRemaining = maxMoves;
        TotalMovesSpent = 0;
        MovesChanged?.Invoke();
    }

    public void RestoreState(int movesRemaining, int totalMovesSpent)
    {
        MovesRemaining = movesRemaining;
        TotalMovesSpent = totalMovesSpent;
        MovesChanged?.Invoke();
    }

    public void AddMoves(int amount)
    {
        MovesRemaining += amount;
        MovesChanged?.Invoke();
    }

    public void SpendMove() => OnMoveSpent();

    private void OnMoveSpent()
    {
        MovesRemaining = Mathf.Max(0, MovesRemaining - 1);
        TotalMovesSpent++;
        MovesChanged?.Invoke();
        if (MovesRemaining == 0) MovesExhausted?.Invoke();
    }
}
