using System.Collections.Generic;

[System.Serializable]
public class CardDefinition
{
    public string id;
    public string text;
    public string image;
}

[System.Serializable]
public class LevelConnection
{
    public string card1;
    public string card2;
}

[System.Serializable]
public class ConsumableFreeUses
{
    public string id;
    public int freeUses;
    public int cost;
}

[System.Serializable]
public class LevelDefinition
{
    public int id;
    public string activeCard;
    public string[] hand;
    public string[] deck;
    public int maxMoves;
    public int optimalMoves;
    public CardDefinition[] cards;
    public LevelConnection[] connections;
    public ConsumableFreeUses[] consumableFreeUses;

    public int GetFreeUses(string consumableId)
    {
        if (consumableFreeUses == null) return 0;
        foreach (ConsumableFreeUses c in consumableFreeUses)
            if (c.id == consumableId) return c.freeUses;
        return 0;
    }

    public int GetCost(string consumableId)
    {
        if (consumableFreeUses == null) return 0;
        foreach (ConsumableFreeUses c in consumableFreeUses)
            if (c.id == consumableId) return c.cost;
        return 0;
    }

    public CardDefinition GetCard(string cardId) =>
        System.Array.Find(cards, c => c.id == cardId);
}

// JsonUtility cannot deserialize bare JSON arrays — wrap with this helper at load time.
[System.Serializable]
public class LevelVariantList { public List<LevelDefinition> items; }
