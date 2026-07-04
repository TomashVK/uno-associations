using TMPro;
using UnityEngine;

public class CoinHud : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;

    private void OnEnable()
    {
        CoinService.CoinsChanged += Refresh;
    }

    private void OnDisable()
    {
        CoinService.CoinsChanged -= Refresh;
    }

    // Start (not OnEnable) so it runs after CoinService.Awake has set Instance.
    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (coinText == null) return;
        int coins = CoinService.Instance != null
            ? CoinService.Instance.Coins
            : (SaveService.Instance?.Data.coins ?? 0);
        coinText.text = coins.ToString();
    }
}
