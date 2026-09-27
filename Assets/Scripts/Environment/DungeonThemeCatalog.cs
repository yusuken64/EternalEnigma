using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable]
public sealed class DungeonTheme
{
    public string Name;
    public OverworldBiome Biome;
    public DungeonEnvironmentKind Environment;
    public TileWorldCreator4TilesPreset RegularBoundary, ThroneBoundary, Floor, Accent;
    public Material DecorationMaterial, PoolMaterial;
    public string[] Decorations = Array.Empty<string>();
    public bool UseTrees;
    public Color Ambient = Color.gray;
    public Color LightColor = Color.white;
    public float LightIntensity = 1;
}

[CreateAssetMenu(menuName="Game/Environment/Dungeon Theme Catalog")]
public sealed class DungeonThemeCatalog : ScriptableObject
{
    public DungeonTheme[] Themes = Array.Empty<DungeonTheme>();
    public DungeonTheme Get(DungeonVisualSelection selection) => Themes.Single(t=>t.Biome==selection.Biome && t.Environment==selection.Environment);

    // Only build presentation changes. Blueprint stacks, masks, dimensions and seed remain untouched.
    public void Apply(TileWorldCreatorAsset clone, DungeonVisualSelection selection, bool throne)
    {
        if (selection.IsLegacy) return;
        var theme = Get(selection);
        for (int i=0;i<clone.mapBuildLayers.Count;i++)
        {
            var layer=clone.mapBuildLayers[i];
            if (layer is InstantiateTiles tiles)
            {
                string role = layer.layerName.ToLowerInvariant();
                if(role.Contains("torch")) { layer.active=false; continue; }
                // Floor excludes the carpet mask, so both must supply paving on regular floors.
                // Reserve the flat accent material for the throne's actual carpet.
                var preset = role.Contains("carpet") ? (throne ? theme.Accent : theme.Floor) : role.Contains("floor") || role.Contains("ground") ? theme.Floor : throne ? theme.ThroneBoundary : theme.RegularBoundary;
                clone.mapBuildLayers[i] = new DungeonThemeTileLayer { guid=layer.guid, assignedGenerationLayerGuid=layer.assignedGenerationLayerGuid,
                    layerName=layer.layerName, active=layer.active, Preset=preset, Offset=tiles.globalPositionOffset + (role.Contains("carpet") && throne ? new Vector3(0,0,-.015f) : Vector3.zero),
                    IgnoreLayers=tiles.ignoreLayers.ToArray() };
            }
            else layer.active = false; // Legacy torches/gates/columns are replaced by bounded cosmetics.
        }
    }
}
