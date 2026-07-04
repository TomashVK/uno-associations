using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    private int currentLevelId = 1;
    private bool levelWon;
    private bool outOfMovesPending;

    [SerializeField] private HandManager handManager;
    [SerializeField] private CardDeck cardDeck;
    [SerializeField] private RevealPile revealPile;
    [SerializeField] private ActiveCardSlot activeCardSlot;
    [SerializeField] private MoveCounter moveCounter;
    [SerializeField] private TMP_Text moveCountText;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private HudStarDisplay hudStarDisplay;
    [SerializeField] private ConsumableButton[] consumableButtons;
    [SerializeField] private GameObject resetDeckVisual;

#if UNITY_EDITOR
    [Tooltip("Editor-only: skip resuming an in-progress save and always deal a fresh hand. Has no effect in builds.")]
    [SerializeField] private bool alwaysDealFreshInEditor;
#endif

    private ConnectionGraph graph;
    private LevelDefinition level;

    private void Start()
    {
        LevelLoader.LoadAll();

        SaveGameData save = SaveService.Instance.Data;
        currentLevelId = save.inProgressLevel?.levelId ?? save.currentLevelId;

        LoadLevel(currentLevelId);
    }

    private void OnEnable()
    {
        HandManager.CardLeftHand += CheckWin;
        ActiveCardSlot.CardPlayed += CheckWin;
        MoveCounter.MovesChanged += RefreshMoveHUD;
        MoveCounter.MovesExhausted += ShowOutOfMovesSettings;

        RevealPile.CardDrawnToRevealPile += CaptureState;
        HandDropZone.CardTakenToHand += CaptureState;
        HandManager.DeckRestarted += CaptureState;
        UndoManager.UndoCompleted += CaptureState;
        WildCardButton.WildCardSpawned += CaptureState;
        SaveService.CaptureRequested += CaptureState;

        RevealPile.PileChanged += RefreshResetDeckVisual;
        CardDeck.DeckChanged += RefreshResetDeckVisual;
    }

    private void OnDisable()
    {
        HandManager.CardLeftHand -= CheckWin;
        ActiveCardSlot.CardPlayed -= CheckWin;
        MoveCounter.MovesChanged -= RefreshMoveHUD;
        MoveCounter.MovesExhausted -= ShowOutOfMovesSettings;

        RevealPile.CardDrawnToRevealPile -= CaptureState;
        HandDropZone.CardTakenToHand -= CaptureState;
        HandManager.DeckRestarted -= CaptureState;
        UndoManager.UndoCompleted -= CaptureState;
        WildCardButton.WildCardSpawned -= CaptureState;
        SaveService.CaptureRequested -= CaptureState;

        RevealPile.PileChanged -= RefreshResetDeckVisual;
        CardDeck.DeckChanged -= RefreshResetDeckVisual;
    }

    private void LoadLevel(int id)
    {
        levelWon = false;
        level = LevelLoader.GetLevel(id);
        if (level == null)
        {
            Debug.LogError($"GameController: level {id} not found.");
            return;
        }

        graph = new ConnectionGraph();
        graph.Build(level.connections);

        moveCounter.Init(level.maxMoves);
        if (hudStarDisplay != null) hudStarDisplay.Init(level.maxMoves, level.optimalMoves);
        RefreshMoveHUD();

        foreach (ConsumableButton button in consumableButtons)
            if (button != null) button.Init(level.GetFreeUses(button.ConsumableId), level.GetCost(button.ConsumableId));

        if (winPanel != null) winPanel.SetActive(false);

        var allCards = new[] { MakeCardData(level.activeCard) }
            .Concat(level.hand.Select(MakeCardData))
            .Concat(level.deck.Select(MakeCardData))
            .ToList();

        cardDeck.SetCards(allCards);
        activeCardSlot.Init(graph);
        RefreshResetDeckVisual();

        bool skipResume = false;
#if UNITY_EDITOR
        skipResume = alwaysDealFreshInEditor;
#endif

        InProgressLevelState resume = SaveService.Instance.Data.inProgressLevel;
        if (!skipResume && resume != null && resume.levelId == id && IsValidResume(resume))
        {
            StartCoroutine(RestoreFromSave(resume));
        }
        else
        {
            StartCoroutine(handManager.DealInitial(level.hand.Length, activeCardSlot));
            SaveService.Instance.BeginLevel(id, level.maxMoves);
        }
    }

    // Any genuinely in-progress level always has at least the level's starting active
    // card sitting in the active slot — an empty active stack means the snapshot was
    // captured at a bad moment (e.g. a win/quit race) and should not be trusted.
    private static bool IsValidResume(InProgressLevelState saved) =>
        saved.activeStackCards != null && saved.activeStackCards.Length > 0;

    // Restores deck/moves/consumables instantly (they're not visual card entities), then
    // animates every saved card (active stack, hand, pile) in with the exact same
    // spawn-from-deck flip-in used for a fresh deal/draw — a resumed level should look
    // identical to a fresh one, just dealing different cards.
    private IEnumerator RestoreFromSave(InProgressLevelState saved)
    {
        CardData[] fullPool = saved.deckCards
            .Concat(saved.handCards)
            .Concat(saved.pileCards)
            .Concat(saved.activeStackCards)
            .ToArray();
        cardDeck.RestoreState(saved.deckCards, saved.deckDrawIndex, fullPool);
        moveCounter.RestoreState(saved.movesRemaining, saved.totalMovesSpent);

        foreach (ConsumableButton button in consumableButtons)
        {
            if (button == null) continue;
            ConsumableSaveState state = saved.consumables.Find(c => c.id == button.ConsumableId);
            if (state != null) button.RestoreFreeUses(state.freeUsesRemaining);
        }

        int total = saved.activeStackCards.Length + saved.handCards.Length + saved.pileCards.Length;
        int completed = 0;
        int slot = 0;
        float stagger = CardAnimationSettings.Instance.DealStagger;

        foreach (CardData data in saved.activeStackCards)
        {
            float delay = slot++ * stagger;
            handManager.AnimateCardIn(data, card => activeCardSlot.ReceiveCard(card), () => completed++, delay);
        }
        foreach (CardData data in saved.handCards)
        {
            float delay = slot++ * stagger;
            handManager.AnimateCardIn(data, card => handManager.AddCardFromRevealPile(card), () => completed++, delay);
        }
        for (int i = 0; i < saved.pileCards.Length; i++)
        {
            CardData data = saved.pileCards[i];
            float delay = slot++ * stagger;
            int pileIndex = i;
            handManager.AnimateCardIn(data, card => revealPile.InsertCardAt(card, pileIndex), () => completed++, delay);
        }

        HandManager.IsAnimating = true;
        yield return new WaitUntil(() => completed >= total);
        HandManager.IsAnimating = false;
    }

    private void RefreshResetDeckVisual()
    {
        if (resetDeckVisual == null) return;
        int cardsLeftToDraw = cardDeck.RemainingCount + revealPile.PileCards.Count;
        resetDeckVisual.SetActive(!MoveCounter.IsOutOfMoves && cardsLeftToDraw > 0);
    }

    private void CaptureState()
    {
        if (level == null || levelWon) return;

        // This runs synchronously inside gameplay event handlers (including mid-coroutine,
        // e.g. the draw animation) — an exception here must never propagate and break the
        // caller's flow, so failures are logged and swallowed instead of thrown.
        try
        {
            var snapshot = new InProgressLevelState
            {
                levelId = currentLevelId,
                movesRemaining = moveCounter.MovesRemaining,
                totalMovesSpent = moveCounter.TotalMovesSpent,
                deckCards = cardDeck.GetCurrentCards(),
                deckDrawIndex = cardDeck.DrawIndex,
                handCards = handManager.GetHandCardData(),
                pileCards = revealPile.GetPileCardData(),
                activeStackCards = activeCardSlot.GetStackCardData()
            };

            foreach (ConsumableButton button in consumableButtons)
                if (button != null)
                    snapshot.consumables.Add(new ConsumableSaveState { id = button.ConsumableId, freeUsesRemaining = button.FreeUsesRemaining });

            SaveService.Instance.CaptureInProgress(snapshot);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"GameController: CaptureState failed, save skipped this move: {e}");
        }
    }

    private CardData MakeCardData(string cardId)
    {
        CardDefinition def = level.GetCard(cardId);
        return new CardData { cardName = def.text, gameId = def.id };
    }

    private void CheckWin()
    {
        if (handManager.CardCount == 0) ShowWin();
        else CaptureState();
    }

    private void ShowWin()
    {
        levelWon = true;
        if (winPanel != null) winPanel.SetActive(true);
        int stars = hudStarDisplay != null ? hudStarDisplay.StarsEarned : 0;
        SaveService.Instance.CompleteLevel(currentLevelId, stars, moveCounter.TotalMovesSpent);
    }

    private void RefreshMoveHUD()
    {
        if (moveCountText != null && moveCounter != null)
            moveCountText.text = $"{moveCounter.MovesRemaining}";
        RefreshResetDeckVisual();
    }

    // MovesExhausted fires mid-drop, from ActiveCardSlot.CardPlayed — before the dropped
    // card has actually left the hand (that happens later via Card.Dropped -> CardLeftHand
    // -> CheckWin, still within the same frame). So levelWon/CardCount aren't settled yet
    // here; defer the actual check to LateUpdate, by which point a same-frame win has
    // already been resolved.
    private void ShowOutOfMovesSettings()
    {
        outOfMovesPending = true;
    }

    private void LateUpdate()
    {
        if (!outOfMovesPending) return;
        outOfMovesPending = false;
        if (levelWon) return;

        SettingsPanel panel = FindObjectOfType<SettingsPanel>(true);
        if (panel != null) panel.ShowOutOfMoves();
    }

    public void RestartLevel()
    {
        SaveService.Instance.ClearInProgress();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ContinueToLobby()
    {
        currentLevelId++;
        SaveService.Instance.SetCurrentLevel(currentLevelId);
        SceneManager.LoadScene("LobbyScene");
    }
}
