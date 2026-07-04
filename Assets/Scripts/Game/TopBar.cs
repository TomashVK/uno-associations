using UnityEngine;

public class TopBar : MonoBehaviour
{
    [SerializeField] private SettingsPanel settingsPanel;

    public void OnSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.Show();
    }
}
