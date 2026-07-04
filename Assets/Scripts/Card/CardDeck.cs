using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDeck : MonoBehaviour, IPointerClickHandler
{
    public static event System.Action Tapped;
    public static event System.Action DeckChanged;

    [SerializeField] private CardData[] cards;
    [SerializeField] private Image deckVisual;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private TMP_Text deckCountText;
    [SerializeField] private Transform spawnContainer;
    [SerializeField] private int maxStackLayers = 4;
    [SerializeField] private Vector2 stackLayerOffset = new Vector2(0f, 3f);

    private int drawIndex;
    private CardData[] originalCards;
    private GameObject[] stackLayers;
    private Vector2 deckCountTextBasePosition;

    public bool HasCards => drawIndex < cards.Length;
    public int RemainingCount => cards.Length - drawIndex;
    public int DrawIndex => drawIndex;
    public CardData[] GetCurrentCards() => (CardData[])cards.Clone();
    // fullPool re-anchors originalCards to the CardData instances actually in play after a
    // restore. Without it, originalCards still points at the fresh instances SetCards made
    // on this LoadLevel, which are reference-distinct from the deserialized save data, so
    // RestartDeck's reference-equality recycle check would never match anything.
    public void RestoreState(CardData[] savedCards, int savedDrawIndex, CardData[] fullPool = null)
    {
        cards = savedCards;
        drawIndex = savedDrawIndex;
        if (fullPool != null) originalCards = fullPool;
        UpdateDeckVisual();
    }
    private void Awake()
    {
        if (cards == null || cards.Length == 0)
            cards = CreateFakeDeck();
        originalCards = cards;
        if (deckCountText != null) deckCountTextBasePosition = deckCountText.rectTransform.anchoredPosition;
        CreateStackLayers();
        ShiftDeckUpForStack();
        RefreshCountText();
    }

    // The stack layers grow upward from the front card, so the whole deck needs to
    // shift up by their total reach to keep the front card's apparent base
    // anchored where a single un-stacked card would sit.
    private void ShiftDeckUpForStack()
    {
        RectTransform deckContainerRT = transform as RectTransform;
        if (deckContainerRT == null) return;
        float shiftUp = maxStackLayers * stackLayerOffset.y;
        deckContainerRT.anchoredPosition -= new Vector2(0f, shiftUp);
    }

    private void CreateStackLayers()
    {
        if (deckVisual == null || cardBackSprite == null || maxStackLayers <= 0) return;

        stackLayers = new GameObject[maxStackLayers];
        RectTransform deckRT = deckVisual.rectTransform;

        for (int i = maxStackLayers - 1; i >= 0; i--)
        {
            GameObject layer = new GameObject($"DeckStackLayer{i}", typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(deckRT.parent, worldPositionStays: false);
            layer.transform.SetSiblingIndex(deckRT.GetSiblingIndex());

            RectTransform layerRT = layer.GetComponent<RectTransform>();
            layerRT.pivot            = deckRT.pivot;
            layerRT.anchorMin        = deckRT.anchorMin;
            layerRT.anchorMax        = deckRT.anchorMax;
            layerRT.sizeDelta        = deckRT.sizeDelta;
            layerRT.anchoredPosition = deckRT.anchoredPosition + stackLayerOffset * (i + 1);
            layer.GetComponent<Image>().sprite = cardBackSprite;

            stackLayers[i] = layer;
        }
    }

    public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke();

    public CardData DrawNext()
    {
        if (!HasCards) return null;
        var data = cards[drawIndex++];
        RefreshCountText();
        return data;
    }

    public void UndrawLast()
    {
        if (drawIndex > 0) drawIndex--;
    }

    public void HideDeckVisual()
    {
        if (deckVisual != null)
            deckVisual.gameObject.SetActive(false);
    }

    public Vector2 GetSpawnPosition(RectTransform relativeTo)
    {
        if (relativeTo == null) return transform.localPosition;
        return relativeTo.InverseTransformPoint(transform.position);
    }

    public void UpdateDeckVisual()
    {
        int remaining = RemainingCount;
        bool hasCards = remaining > 0;

        if (deckVisual != null)
            deckVisual.gameObject.SetActive(hasCards);

        UpdateStackLayers(remaining);

        if (deckCountText != null)
            deckCountText.gameObject.SetActive(hasCards);

        RefreshCountText();
        DeckChanged?.Invoke();
    }

    private void UpdateStackLayers(int remaining)
    {
        if (stackLayers == null) return;
        int visibleLayers = Mathf.Clamp(remaining, 0, stackLayers.Length);
        int hiddenLayers = stackLayers.Length - visibleLayers;
        for (int i = 0; i < stackLayers.Length; i++)
            if (stackLayers[i] != null) stackLayers[i].SetActive(i >= hiddenLayers);

        // The near-front layers disappear first as cards are drawn, dropping the
        // visible top of the pile by one offset step each time — the counter badge
        // needs to follow that drop instead of staying pinned to the full-stack spot.
        if (deckCountText != null)
            deckCountText.rectTransform.anchoredPosition = deckCountTextBasePosition + stackLayerOffset * hiddenLayers;
    }

    public void RestartDeck(IEnumerable<CardData> recycleFromPile)
    {
        var recycleSet = new HashSet<CardData>(recycleFromPile);
        var drawable = new List<CardData>();
        foreach (CardData c in originalCards)
            if (recycleSet.Contains(c))
                drawable.Add(c);
        cards = drawable.ToArray();
        drawIndex = 0;
        UpdateDeckVisual();
        RefreshCountText();
    }

    public void SetCards(List<CardData> newCards)
    {
        cards = newCards.ToArray();
        originalCards = cards;
        drawIndex = 0;
        UpdateDeckVisual();
        RefreshCountText();
    }

    public void SetCountDisplay(int count)
    {
        if (deckCountText != null)
            deckCountText.text = count.ToString();
    }

    public GameObject CreateBackgroundSpawn()
    {
        if (spawnContainer == null || RemainingCount <= 0) return null;

        GameObject bg = Instantiate(spawnContainer.gameObject, transform.parent);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        RectTransform scRT = spawnContainer.GetComponent<RectTransform>();
        if (bgRT != null && scRT != null)
        {
            bgRT.position = scRT.position;
            bg.transform.SetSiblingIndex(transform.GetSiblingIndex());
        }

        if (deckCountText != null)
        {
            GameObject textClone = Instantiate(deckCountText.gameObject, bg.transform);
            textClone.GetComponent<RectTransform>().position = deckCountText.GetComponent<RectTransform>().position;
            TMP_Text t = textClone.GetComponent<TMP_Text>();
            if (t != null) t.text = RemainingCount.ToString();
        }

        foreach (Canvas c in bg.GetComponentsInChildren<Canvas>(true))
            c.sortingOrder = -5;

        return bg;
    }

    private void RefreshCountText()
    {
        if (deckCountText != null)
            deckCountText.text = RemainingCount.ToString();
    }

    public void SetVisible(bool visible)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
        }
        else
        {
            foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
                g.enabled = visible;
            foreach (Canvas c in GetComponentsInChildren<Canvas>(true))
                c.enabled = visible;
        }
    }

    private static CardData[] CreateFakeDeck()
    {
        string[] names = { "Apple", "Banana", "Cherry", "Dog", "Elephant", "Flower", "Guitar", "House", "Ice Cream", "Jellyfish" };
        var deck = new CardData[names.Length];
        for (int i = 0, n = names.Length; i < n; i++)
            deck[i] = new CardData { cardName = names[i] };
        return deck;
    }
}
