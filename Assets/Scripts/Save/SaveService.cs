using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveService : MonoBehaviour
{
    private const float SaveDebounceSeconds = 0.5f;

    public static SaveService Instance { get; private set; }

    // Fired right before a pause/quit flush so gameplay code can push a final,
    // up-to-the-moment snapshot instead of whatever was last debounced in.
    public static event System.Action CaptureRequested;

    public SaveGameData Data { get; private set; }

    private ISaveStorage storage;
    private Coroutine pendingSave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SaveService");
        go.AddComponent<SaveService>();
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

        storage = new LocalJsonFileStorage();
        storage.Load(json =>
        {
            Data = string.IsNullOrEmpty(json) ? new SaveGameData() : JsonUtility.FromJson<SaveGameData>(json);

            // JsonUtility can't round-trip a null reference field: a null inProgressLevel
            // gets written out as (and read back as) a default-valued instance — levelId 0 —
            // instead of staying null. Normalize that back to null here so every other
            // reader can trust "non-null inProgressLevel" to mean a real resumable level.
            if (Data.inProgressLevel != null && Data.inProgressLevel.levelId <= 0)
                Data.inProgressLevel = null;
        });
    }

    public void BeginLevel(int levelId, int maxMoves)
    {
        Data.inProgressLevel = new InProgressLevelState
        {
            levelId = levelId,
            movesRemaining = maxMoves,
            totalMovesSpent = 0
        };
        RequestSave();
    }

    public void CaptureInProgress(InProgressLevelState snapshot)
    {
        Data.inProgressLevel = snapshot;
        RequestSave();
    }

    public void CompleteLevel(int levelId, int stars, int moves)
    {
        LevelProgressData entry = Data.levelProgress.FirstOrDefault(p => p.levelId == levelId);
        if (entry == null)
        {
            entry = new LevelProgressData { levelId = levelId };
            Data.levelProgress.Add(entry);
        }

        entry.bestStars = Mathf.Max(entry.bestStars, stars);
        entry.bestMoves = entry.completed ? Mathf.Min(entry.bestMoves, moves) : moves;
        entry.completed = true;

        Data.inProgressLevel = null;
        RequestSave();
    }

    public void ClearInProgress()
    {
        Data.inProgressLevel = null;
        RequestSave();
    }

    public void SetCurrentLevel(int levelId)
    {
        Data.currentLevelId = levelId;
        RequestSave();
    }

    public void SetCoins(int amount)
    {
        Data.coins = amount;
        RequestSave();
    }

    public SettingsData GetSettings() => Data.settings ??= new SettingsData();

    public void SetSettings(SettingsData settings)
    {
        Data.settings = settings;
        RequestSave();
    }

    // Debug-only full reset (see DebugResetTrigger) — simulates a fresh install and
    // reloads immediately so the effect is visible right away.
    public void ResetAll()
    {
        Data = new SaveGameData();
        FlushSave();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void RequestSave()
    {
        if (pendingSave != null) StopCoroutine(pendingSave);
        pendingSave = StartCoroutine(SaveAfterDelay());
    }

    private IEnumerator SaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(SaveDebounceSeconds);
        pendingSave = null;
        FlushSave();
    }

    private void FlushSave()
    {
        if (pendingSave != null)
        {
            StopCoroutine(pendingSave);
            pendingSave = null;
        }
        storage.Save(JsonUtility.ToJson(Data));
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause) return;
        CaptureRequested?.Invoke();
        FlushSave();
    }

    private void OnApplicationQuit()
    {
        CaptureRequested?.Invoke();
        FlushSave();
    }
}
