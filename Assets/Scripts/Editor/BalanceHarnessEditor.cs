using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BalanceHarnessEditor
{
    [MenuItem("Tools/Eternal Enigma/Balance/Build Windows Player")]
    public static void Build()
    {
        Export();
        try { AutoplayBatchBuild.Build(); }
        finally
        {
            Directory.CreateDirectory("Builds/DebugAutoplay");
            if (File.Exists("Temp/DebugAutoplay/Build.txt"))
                File.Copy("Temp/DebugAutoplay/Build.txt","Builds/DebugAutoplay/balance-build-result.txt",true);
        }
    }

    [MenuItem("Tools/Eternal Enigma/Balance/Run From File")]
    public static void RunFromFile()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Finish the existing session first.");
        var options = JsonUtility.FromJson<AutoplayOptions>(File.ReadAllText("Temp/Balance/options.json"));
        if (options?.Balance == null || !options.Validate(out _)) throw new ArgumentException("Expected validated normal balance options.");
        SessionState.SetString(AutoplayRunner.PendingKey, JsonUtility.ToJson(options));
        EditorApplication.isPlaying = true;
    }

    [Serializable] public class Catalog
    {
        public string Policy = BalanceExperimentOptions.PolicyVersion;
        public string[] Heroes;
        public Hero[] HeroDetails;
        public Offer[] Items;
        public Ability[] Abilities;
    }
    [Serializable] public class Offer { public string Name, Building, Asset; public int Price, Stack, Tier, HP, Mana, Food; }
    [Serializable] public class Ability { public string Name, Asset, Intent; public int Mana, CastTime, MaxRank; }
    [Serializable] public class Hero { public string Id, Name, Primary, Secondary; }

    [MenuItem("Tools/Eternal Enigma/Balance/Export Catalog")]
    public static void Export()
    {
        var catalog = new Catalog {
            Heroes = TownSceneLoader.Default.AllyCatalog.Select(h => h.Id).OrderBy(x => x).ToArray(),
            HeroDetails = TownSceneLoader.Default.AllyCatalog.Select(h => new Hero { Id=h.Id,Name=h.Name,
                Primary=h.PrimaryClass?.DisplayName,Secondary=h.SecondaryClass?.DisplayName }).OrderBy(h=>h.Name).ToArray(),
            Items = Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings").SelectMany(b => b.ShopCatalog.Where(o => o.Item != null).Select(o => {
                var effect = o.Item.ItemEffectDefinition as ModifyStatsItemEffectDefinition;
                return new Offer { Name = o.Item.ItemName, Building = b.Id, Asset = AssetDatabase.GetAssetPath(b), Price = o.Price,
                    Stack = o.StackCount, Tier = o.MinimumTier, HP = effect?.VitalModification.Hp ?? 0,
                    Mana = effect?.VitalModification.Mana ?? 0, Food = effect?.VitalModification.Hunger ?? 0 };
            })).OrderBy(o => o.Building).ThenBy(o => o.Name).ToArray(),
            Abilities = Resources.LoadAll<Skill>("Classes/Skills").Select(s => new Ability {
                Name = s.SkillName, Asset = AssetDatabase.GetAssetPath(s), Mana = s.ManaCost, CastTime = s.CastTime,
                MaxRank = s.MaxRank, Intent = SkillIntents.Classify(s).ToString() }).OrderBy(s => s.Name).ToArray()
        };
        Directory.CreateDirectory("Logs/Balance");
        File.WriteAllText("Logs/Balance/catalog.json", JsonUtility.ToJson(catalog,true));
    }
}
