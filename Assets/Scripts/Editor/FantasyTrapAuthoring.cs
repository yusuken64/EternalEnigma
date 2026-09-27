using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FantasyTrapAuthoring
{
    const string Folder = "Assets/Resources/FantasyTraps";
    [MenuItem("Tools/Eternal Enigma/Art/Import Fantasy Traps")]
    public static void Import()
    {
        DungeonPropModelImporter.Import();
        Directory.CreateDirectory(Folder + "/Effects");
        Directory.CreateDirectory("Assets/Resources/TrapFood");
        AssetDatabase.Refresh();
        var stuck = Status<StuckStatusEffect>("ShadowBind");
        var confusion = Status<ConfusionStatusEffect>("Hallucination");
        var weaken = Status<WeakenStatusEffect>("WitheringWeaken");
        var silence = Status<SilenceStatusEffect>("WitheringSilence");
        foreach (FantasyTrapKind kind in Enum.GetValues(typeof(FantasyTrapKind)))
        {
            var root = new GameObject(kind.ToString());
            try
            {
                var trap = root.AddComponent<FantasyTrap>(); trap.Kind = kind;
                trap.ActivationChance = .5f;
                trap.DamagePercent = kind is FantasyTrapKind.IronArrow or FantasyTrapKind.FallingRock or FantasyTrapKind.Bomb ? 15 : 10;
                trap.Distance = kind == FantasyTrapKind.RollingLog ? 2 : 3;
                trap.Effects = kind switch
                {
                    FantasyTrapKind.ShadowBind => new StatusEffect[] { stuck },
                    FantasyTrapKind.Hallucination => new StatusEffect[] { confusion },
                    FantasyTrapKind.WitheringHex => new StatusEffect[] { weaken, silence },
                    _ => Array.Empty<StatusEffect>()
                };
                trap.VisualObject = DungeonPropModels.Create("Trap" + kind, root.transform, 2.5f);
                trap.VisualObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + kind + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var bread = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset");
        bread.IsFood = true;
        bread.SpoiledFood = Food(bread, "Spoiled Bread", .25f);
        bread.CharredFood = Food(bread, "Charred Bread", .5f);
        EditorUtility.SetDirty(bread);
        AssetDatabase.SaveAssets();
        Debug.Log("Authored 18 modeled fantasy traps, four status prefabs, and recoverable food variants.");
    }
    static T Status<T>(string name) where T : StatusEffect
    {
        var root = new GameObject(name);
        try
        {
            var effect = root.AddComponent<T>(); effect.TurnsLeft = 3;
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Effects/" + name + ".prefab").GetComponent<T>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static ItemDefinition Food(ItemDefinition source, string name, float scale)
    {
        string path = "Assets/Resources/TrapFood/" + name.Replace(" ", "") + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        var copy = UnityEngine.Object.Instantiate(source);
        copy.ItemName = name; copy.Description = "Restores " + (int)(scale * 100) + "% of normal fullness.";
        copy.IsFood = true; copy.SpoiledFood = null; copy.CharredFood = null;
        var original = source.ItemEffectDefinition as ModifyStatsItemEffectDefinition;
        if (original == null) throw new InvalidOperationException("Food needs an explicit restoration effect.");
        var effectPath = path.Replace(".asset", "Effect.asset");
        var effect = AssetDatabase.LoadAssetAtPath<ModifyStatsItemEffectDefinition>(effectPath);
        if (effect == null) { effect = ScriptableObject.CreateInstance<ModifyStatsItemEffectDefinition>(); AssetDatabase.CreateAsset(effect, effectPath); }
        effect.VitalModification = new VitalModification { Hunger = Mathf.CeilToInt(original.VitalModification.Hunger * scale) };
        effect.DoDamageAnimation = false; EditorUtility.SetDirty(effect);
        copy.ItemEffectDefinition = effect;
        if (item == null) { item = copy; AssetDatabase.CreateAsset(item, path); }
        else { EditorUtility.CopySerialized(copy, item); UnityEngine.Object.DestroyImmediate(copy); EditorUtility.SetDirty(item); }
        return item;
    }
}
