using TMPro;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class ConsumableButton : MonoBehaviour, IPointerClickHandler
{
    public enum ConsumptionType { FreeUse, Coins, Ad }

    [SerializeField] private string consumableId;
    [SerializeField] private int coinCost;
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

    // Free uses come from the current level's data, not a fixed inspector value —
    // GameController calls this once per level load.
    public void Init(int freeUses)
    {
        freeUsesRemaining = freeUses;
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
        MoveCounter.MovesChanged += RefreshDisplay;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        CoinService.CoinsChanged -= RefreshDisplay;
        MoveCounter.MovesChanged -= RefreshDisplay;
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

    // Out of moves means neither a free use nor coins can actually help — the only
    // path back into the level is a rewarded ad granting more moves — so every
    // consumable button (undo, wild card, ...) forces the ad icon in that state
    // instead of each caller having to special-case its own display.
    private void RefreshDisplay()
    {
        bool outOfMoves = MoveCounter.IsOutOfMoves;
        bool hasFreeUses = freeUsesRemaining > 0;
        bool hasEnoughCoins = CoinService.Instance != null && CoinService.Instance.Coins >= coinCost;

        if (badgeContainer != null) badgeContainer.SetActive(hasFreeUses && !outOfMoves);
        if (badgeText != null) badgeText.text = freeUsesRemaining.ToString();
        if (costContainer != null) costContainer.SetActive(!hasFreeUses && !outOfMoves);
        if (costLabel != null) costLabel.text = coinCost.ToString();
        if (adIcon != null) adIcon.SetActive(outOfMoves || (!hasFreeUses && !hasEnoughCoins));
    }
}
