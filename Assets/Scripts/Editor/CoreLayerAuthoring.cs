using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TWC;
using TWC.Actions;

/// <summary>Validates the committed Core-to-TWC layer bindings.</summary>
public static class CoreLayerAuthoring
{
    const string DungeonPath = "Assets/Prefabs/Dungeon/DungeonAsset.asset";
    const string ThronePath = "Assets/Prefabs/Dungeon/DungeonThroneAsset.asset";
    const string VillagePath = "Assets/TileWorldCreator/VillageLSystemAsset.asset";


    static TileWorldCreatorAsset Load(string path) => AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(path);

    [MenuItem("Tools/Eternal Enigma/Core Layers/Verify Assets")]
    public static void VerifyAssets()
    {
        var issues = new List<string>();
        issues.AddRange(Verify(Load(DungeonPath), CoreLayerNames.Dungeon));
        issues.AddRange(Verify(Load(ThronePath), CoreLayerNames.Dungeon));
        issues.AddRange(Verify(Load(VillagePath), CoreLayerNames.Town));

        if (issues.Count == 0)
        {
            Debug.Log("OK");
        }
        else
        {
            foreach (var issue in issues) Debug.Log(issue);
        }
    }

    public static List<string> Verify(TileWorldCreatorAsset asset, string[] coreLayerNames)
    {
        var issues = new List<string>();
        if (asset == null)
        {
            issues.Add("Asset is null.");
            return issues;
        }

        bool isDungeon = ReferenceEquals(coreLayerNames, CoreLayerNames.Dungeon);
        bool isTown = ReferenceEquals(coreLayerNames, CoreLayerNames.Town);

        // Reference dungeon action used to detect option-field drift between layers.
        CoreDungeonLayerGenerator dungeonReference = null;
        CoreTownLayerGenerator townReference = null;

        foreach (var layer in asset.mapBlueprintLayers)
        {
            if (!coreLayerNames.Contains(layer.layerName)) continue;

            if (layer.stack == null || layer.stack.Count != 1)
            {
                issues.Add($"{asset.name}: layer '{layer.layerName}' does not have exactly one action.");
                continue;
            }

            var action = layer.stack[0].action;

            if (isDungeon)
            {
                if (action is CoreDungeonLayerGenerator dungeonAction)
                {
                    if (dungeonAction.LayerName != layer.layerName)
                        issues.Add($"{asset.name}: layer '{layer.layerName}' action LayerName is '{dungeonAction.LayerName}'.");

                    if (dungeonReference == null)
                    {
                        dungeonReference = dungeonAction;
                    }
                    else if (dungeonAction.Throne != dungeonReference.Throne ||
                             dungeonAction.EnemyCount != dungeonReference.EnemyCount ||
                             dungeonAction.GoldCount != dungeonReference.GoldCount ||
                             dungeonAction.ItemCount != dungeonReference.ItemCount ||
                             dungeonAction.TrapCount != dungeonReference.TrapCount)
                    {
                        issues.Add($"{asset.name}: layer '{layer.layerName}' core action options differ from other core actions in the asset.");
                    }

                    bool expectedThrone = asset.mapWidth == 12;
                    if (dungeonAction.Throne != expectedThrone)
                        issues.Add($"{asset.name}: layer '{layer.layerName}' Throne={dungeonAction.Throne} but mapWidth={asset.mapWidth} (expected Throne={expectedThrone}).");
                }
                else
                {
                    issues.Add($"{asset.name}: layer '{layer.layerName}' action is not a CoreDungeonLayerGenerator.");
                }
            }
            else if (isTown)
            {
                if (action is CoreTownLayerGenerator townAction)
                {
                    if (townAction.LayerName != layer.layerName)
                        issues.Add($"{asset.name}: layer '{layer.layerName}' action LayerName is '{townAction.LayerName}'.");

                    if (townReference == null)
                    {
                        townReference = townAction;
                    }
                    else if (townAction.BuildingCount != townReference.BuildingCount ||
                             townAction.ShopFlags != townReference.ShopFlags ||
                             townAction.AllyCount != townReference.AllyCount ||
                             townAction.PartySpawnX != townReference.PartySpawnX ||
                             townAction.PartySpawnY != townReference.PartySpawnY ||
                             townAction.ExitX != townReference.ExitX ||
                             townAction.ExitY != townReference.ExitY)
                    {
                        issues.Add($"{asset.name}: layer '{layer.layerName}' core action options differ from other core actions in the asset.");
                    }
                }
                else
                {
                    issues.Add($"{asset.name}: layer '{layer.layerName}' action is not a CoreTownLayerGenerator.");
                }
            }
        }

        var blueprintGuids = new HashSet<Guid>(asset.mapBlueprintLayers.Select(l => l.guid));
        foreach (var buildLayer in asset.mapBuildLayers)
        {
            if (buildLayer.assignedGenerationLayerGuid != Guid.Empty && !blueprintGuids.Contains(buildLayer.assignedGenerationLayerGuid))
                issues.Add($"{asset.name}: build layer '{buildLayer.layerName}' assignedGenerationLayerGuid does not match any blueprint layer guid.");
        }

        return issues;
    }
}
