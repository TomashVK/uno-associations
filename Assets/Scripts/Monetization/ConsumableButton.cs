using TMPro;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class ConsumableButton : MonoBehaviour, IPointerClickHandler
{
    public enum ConsumptionType { FreeUse, Coins, Ad }

    [SerializeField] private string consumableId;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private GameObject badgeContainer;
    [SerializeField] private GameObject adIcon;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private GameObject costContainer;
    [SerializeField] private UnityEvent onGranted;

    public System.Func<bool> CanActivate;
    public string ConsumableId => consumableId;
    public int FreeUsesRemaining => freeUsesRemaining;

    // Set right before onGranted fires — callers that need undo support (e.g. WildCardButton)
    // read this synchronously to record which resource to refund later.
    public ConsumptionType LastConsumptionType { get; private set; }

    private int freeUsesRemaining;
    private int coinCost;

    // Free uses and cost come from the current level's data, not fixed inspector values —
    // GameController calls this once per level load.
    public void Init(int freeUses, int cost)
    {
        freeUsesRemaining = freeUses;
        coinCost = cost;
        RefreshDisplay();
    }

    public void RestoreFreeUses(int value)
    {
        freeUsesRemaining = value;
        RefreshDisplay();
    }

    private void OnEnable()
    {
        CoinService.CoinsChanged += RefreshDisplay;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        CoinService.CoinsChanged -= RefreshDisplay;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameSettings.VibrationEnabled) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);
        RequestUse();
    }

    private void RequestUse()
    {
        if (CanActivate != null && !CanActivate()) return;

        if (freeUsesRemaining > 0)
        {
            freeUsesRemaining--;
            LastConsumptionType = ConsumptionType.FreeUse;
            RefreshDisplay();
            onGranted?.Invoke();
            return;
        }

        if (CoinService.Instance != null && CoinService.Instance.TrySpend(coinCost))
        {
            LastConsumptionType = ConsumptionType.Coins;
            RefreshDisplay();
            onGranted?.Invoke();
            return;
        }

        AdService.ShowRewardedAd(() =>
        {
            LastConsumptionType = ConsumptionType.Ad;
            onGranted?.Invoke();
        });
    }

    // Reverses whichever resource a past use spent — the caller (e.g. an undo record)
    // must pass back the ConsumptionType it captured at the time, not rely on
    // LastConsumptionType, since further uses may have happened since.
    public void Refund(ConsumptionType type)
    {
        switch (type)
        {
            case ConsumptionType.FreeUse:
                freeUsesRemaining++;
                break;
            case ConsumptionType.Coins:
                CoinService.Instance?.Refund(coinCost);
                break;
        }
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        bool hasFreeUses = freeUsesRemaining > 0;
        bool hasEnoughCoins = CoinService.Instance != null && CoinService.Instance.Coins >= coinCost;

        if (badgeContainer != null) badgeContainer.SetActive(hasFreeUses);
        if (badgeText != null) badgeText.text = freeUsesRemaining.ToString();
        if (costContainer != null) costContainer.SetActive(!hasFreeUses);
        if (costLabel != null) costLabel.text = coinCost.ToString();
        if (adIcon != null) adIcon.SetActive(!hasFreeUses && !hasEnoughCoins);
    }
}
