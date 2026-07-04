using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private GameObject restartLevelButton;
    [SerializeField] private GameObject goToLobbyButton;
    [SerializeField] private GameObject closeButton;

    private void OnEnable()
    {
        soundToggle.SetIsOnWithoutNotify(GameSettings.SoundEnabled);
        musicToggle.SetIsOnWithoutNotify(GameSettings.MusicEnabled);
        vibrationToggle.SetIsOnWithoutNotify(GameSettings.VibrationEnabled);

        soundToggle.onValueChanged.AddListener(OnSoundChanged);
        musicToggle.onValueChanged.AddListener(OnMusicChanged);
        vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
    }

    private void OnDisable()
    {
        soundToggle.onValueChanged.RemoveListener(OnSoundChanged);
        musicToggle.onValueChanged.RemoveListener(OnMusicChanged);
        vibrationToggle.onValueChanged.RemoveListener(OnVibrationChanged);
    }

    public void Show()
    {
        SetSecondaryControlsActive(true);
        gameObject.SetActive(true);
    }

    // Out of moves: there's nothing left to resume to, so only Restart / Lobby make sense.
    public void ShowOutOfMoves()
    {
        SetSecondaryControlsActive(false);
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    public void OnRestartLevel()
    {
        Hide();
        GameController gameController = FindObjectOfType<GameController>();
        if (gameController != null) gameController.RestartLevel();
    }

    public void OnGoToLobby()
    {
        Hide();
        SceneManager.LoadScene("LobbyScene");
    }

    private void SetSecondaryControlsActive(bool active)
    {
        soundToggle.gameObject.SetActive(active);
        musicToggle.gameObject.SetActive(active);
        vibrationToggle.gameObject.SetActive(active);
        if (closeButton != null) closeButton.SetActive(active);
    }

    private void OnSoundChanged(bool value) => GameSettings.SoundEnabled = value;
    private void OnMusicChanged(bool value) => GameSettings.MusicEnabled = value;
    private void OnVibrationChanged(bool value) => GameSettings.VibrationEnabled = value;
}
