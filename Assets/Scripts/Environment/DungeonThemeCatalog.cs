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
    public DungeonBoundaryPreset RegularSmartBoundary, ThroneSmartBoundary;
    public Material DecorationMaterial, PoolMaterial;
    public AudioClip Music, BossMusic;
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
    public AudioClip FallbackMusic;
    public DungeonTheme Get(DungeonVisualSelection selection) => Themes.Single(t=>t.Biome==selection.Biome && t.Environment==selection.Environment);

    // Only build presentation changes. Blueprint stacks, masks, dimensions and seed remain untouched.
    public void Apply(TileWorldCreatorAsset clone, DungeonVisualSelection selection, bool throne)
    {
        var theme = Get(selection);
        foreach (var layer in clone.mapBuildLayers.OfType<DungeonThemeTileLayer>())
        {
            if (!layer.UseThemePreset || layer.Role == DungeonThemeRole.Custom) continue;
            // Participation is explicit. Active flags, bindings, offsets and the build stack remain authored.
            layer.Preset = layer.Role switch {
                DungeonThemeRole.Floor => theme.Floor,
                DungeonThemeRole.Accent => throne ? theme.Accent : theme.Floor,
                _ => throne ? theme.ThroneBoundary : theme.RegularBoundary
            };
            if (layer is DungeonBoundaryLayer boundary)
            {
                boundary.SmartPreset = throne ? theme.ThroneSmartBoundary : theme.RegularSmartBoundary;
                boundary.GroundPreset = theme.Floor;
            }
        }
    }
}
