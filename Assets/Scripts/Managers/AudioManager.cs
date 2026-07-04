using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioClip dealClip;
    private bool soundEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("Prefabs/AudioManager");
        var go = prefab != null ? Instantiate(prefab) : new GameObject("AudioManager");
        go.name = "AudioManager";
        if (prefab == null) go.AddComponent<AudioManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;

        dealClip = Resources.Load<AudioClip>("Audio/deal");

        AudioClip musicClip = Resources.Load<AudioClip>("Audio/music");
        if (musicClip != null) musicSource.clip = musicClip;
    }

    private void Start()
    {
        soundEnabled = GameSettings.SoundEnabled;
        if (musicSource.clip != null && GameSettings.MusicEnabled) musicSource.Play();
    }

    private void OnEnable()
    {
        GameSettings.MusicEnabledChanged += OnMusicEnabledChanged;
        GameSettings.SoundEnabledChanged += OnSoundEnabledChanged;
    }

    private void OnDisable()
    {
        GameSettings.MusicEnabledChanged -= OnMusicEnabledChanged;
        GameSettings.SoundEnabledChanged -= OnSoundEnabledChanged;
    }

    public void PlayDealSound()
    {
        if (!soundEnabled || dealClip == null) return;
        sfxSource.PlayOneShot(dealClip);
    }

    private void OnMusicEnabledChanged(bool enabled)
    {
        if (enabled) musicSource.Play();
        else musicSource.Pause();
    }

    private void OnSoundEnabledChanged(bool enabled)
    {
        soundEnabled = enabled;
    }
}
