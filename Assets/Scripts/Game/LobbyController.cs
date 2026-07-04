using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyController : MonoBehaviour
{
    [SerializeField] private TMP_Text playButtonLabel;
    [SerializeField] private SettingsPanel settingsPanel;

    private void Start()
    {
        int levelId = SaveService.Instance.Data.currentLevelId;
        if (playButtonLabel != null) playButtonLabel.text = $"Level {levelId}";
    }

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.Show();
    }
}
