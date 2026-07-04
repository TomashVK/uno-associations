public static class GameSettings
{
    public static event System.Action<bool> SoundEnabledChanged;
    public static event System.Action<bool> MusicEnabledChanged;

    public static bool SoundEnabled
    {
        get => SaveService.Instance.GetSettings().soundEnabled;
        set
        {
            SaveService.Instance.GetSettings().soundEnabled = value;
            SaveService.Instance.SetSettings(SaveService.Instance.GetSettings());
            SoundEnabledChanged?.Invoke(value);
        }
    }

    public static bool MusicEnabled
    {
        get => SaveService.Instance.GetSettings().musicEnabled;
        set
        {
            SaveService.Instance.GetSettings().musicEnabled = value;
            SaveService.Instance.SetSettings(SaveService.Instance.GetSettings());
            MusicEnabledChanged?.Invoke(value);
        }
    }

    public static bool VibrationEnabled
    {
        get => SaveService.Instance.GetSettings().vibrationEnabled;
        set
        {
            SaveService.Instance.GetSettings().vibrationEnabled = value;
            SaveService.Instance.SetSettings(SaveService.Instance.GetSettings());
        }
    }
}
