using System.Collections.Generic;

[System.Serializable]
public class SettingsData
{
    public bool soundEnabled = true;
    public bool musicEnabled = true;
    public bool vibrationEnabled = true;
}

[System.Serializable]
public class SaveGameData
{
    public int version = 1;
    public int currentLevelId = 1;
    public int coins = 1500;
    public SettingsData settings = new();
    public List<LevelProgressData> levelProgress = new();
    public InProgressLevelState inProgressLevel; // null = no resumable level
}

[System.Serializable]
public class LevelProgressData
{
    public int levelId;
    public int bestStars;
    public int bestMoves;
    public bool completed;
}

[System.Serializable]
public class ConsumableSaveState
{
    public string id;
    public int freeUsesRemaining;
}

[System.Serializable]
public class InProgressLevelState
{
    public int levelId;
    public int movesRemaining;
    public int totalMovesSpent;
    public CardData[] deckCards = System.Array.Empty<CardData>();
    public int deckDrawIndex;
    public CardData[] handCards = System.Array.Empty<CardData>();
    public CardData[] pileCards = System.Array.Empty<CardData>();
    public CardData[] activeStackCards = System.Array.Empty<CardData>();
    public List<ConsumableSaveState> consumables = new();
}
