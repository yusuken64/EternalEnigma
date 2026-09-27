using System;
using TWC;
using TWC.Actions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Seeded small patches clipped to the input plateau. Does not consume gameplay RNG.</summary>
[Serializable, ActionCategory(Category = ActionCategoryAttribute.CategoryTypes.Modifiers), ActionName(Name = "Mountain top patch noise")]
public sealed class MountainTopNoise : TWCBlueprintAction, ITWCAction
{
    public float Frequency = .65f;
    public float Threshold = .46f;
    public ITWCAction Clone() => new MountainTopNoise { Frequency = Frequency, Threshold = Threshold };
    public bool[,] Execute(bool[,] map, TileWorldCreator creator)
    {
        var result = new bool[map.GetLength(0), map.GetLength(1)];
        uint seed = OverworldCosmetics.Hash(creator.currentSeed, 371, 911);
        float ox = seed % 10000 * .1f, oy = (seed >> 16) % 10000 * .1f;
        for (int y = 0; y < map.GetLength(1); y++)
        for (int x = 0; x < map.GetLength(0); x++)
            // Periodic one-cell breaks bound clusters to at most 3 by 3 cells.
            result[x,y] = map[x,y] && (x + seed % 4) % 4 != 0 && (y + (seed >> 8) % 4) % 4 != 0
                && Mathf.PerlinNoise(x * Frequency + ox, y * Frequency + oy) > Threshold;
        return result;
    }
    public float GetGUIHeight() => 40;
#if UNITY_EDITOR
    public override void DrawGUI(Rect rect, int index, TileWorldCreatorAsset asset, TileWorldCreator creator)
    {
        rect.height = 18; Frequency = EditorGUI.Slider(rect, "Patch frequency", Frequency, .1f, 1.5f);
        rect.y += 20; Threshold = EditorGUI.Slider(rect, "Noise threshold", Threshold, .1f, .9f);
    }
#endif
}
