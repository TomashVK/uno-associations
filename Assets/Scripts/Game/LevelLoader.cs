using System.Collections.Generic;
using UnityEngine;

public static class LevelLoader
{
    public static List<LevelDefinition> Levels;

    public static void LoadAll()
    {
        Levels = LoadLevels("Data/Levels");
    }

    public static LevelDefinition GetLevel(int id) =>
        Levels?.Find(l => l.id == id);

    private static List<LevelDefinition> LoadLevels(string folder)
    {
        var levels = new List<LevelDefinition>();
        int id = 1;
        while (true)
        {
            TextAsset asset = Resources.Load<TextAsset>($"{folder}/level_{id}");
            if (asset == null) break;
            string wrapped = "{\"items\":" + asset.text + "}";
            var wrapper = JsonUtility.FromJson<LevelVariantList>(wrapped);
            var variants = (List<LevelDefinition>)typeof(LevelVariantList).GetField("items").GetValue(wrapper);
            if (variants != null && variants.Count > 0)
            {
                var picked = variants[Random.Range(0, variants.Count)];
                picked.id = id;
                levels.Add(picked);
            }
            id++;
        }
        return levels;
    }
}
