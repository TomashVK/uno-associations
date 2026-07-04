using UnityEngine;

public class WildCardButton : MonoBehaviour
{
    public static event System.Action WildCardSpawned;

    // Other code restoring/recreating a wild card (e.g. save restore) needs the same
    // prefab used here — exposed statically rather than duplicating this reference
    // elsewhere in the scene.
    public static WildCardButton Instance { get; private set; }
    public GameObject WildCardPrefab => wildCardPrefab;

    [SerializeField] private GameObject wildCardPrefab;
    [SerializeField] private HandManager handManager;
    [SerializeField] private RectTransform spawnPoint;
    [SerializeField] private ConsumableButton consumableButton;

    private void Awake()
    {
        Instance = this;
        if (consumableButton != null)
            consumableButton.CanActivate = () => handManager.CanAcceptCard();
    }

    public void SpawnWildCard()
    {
        if (!handManager.CanAcceptCard()) return;

        RectTransform container = handManager.CardContainer != null
            ? handManager.CardContainer
            : handManager.GetComponent<RectTransform>().root.GetComponent<RectTransform>();

        GameObject newCardObj = Instantiate(wildCardPrefab, container);
        RectTransform newCardRT = newCardObj.GetComponent<RectTransform>();
        newCardRT.anchoredPosition = container.InverseTransformPoint(spawnPoint.position);

        Card card = newCardObj.GetComponent<Card>();
        card.Init(new CardData { cardName = "", isWild = true });
        card.SetHorizontal(true);

        handManager.AddCardFromRevealPile(card);
        AudioManager.Instance?.PlayDealSound();

        UndoManager.Instance?.RecordWildCardSpawn(card, consumableButton, spawnPoint);
        WildCardSpawned?.Invoke();
    }
}
