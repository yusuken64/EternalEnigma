using System;
using EternalEnigma.Core.World;
using UnityEngine;

// Values are the texture/vertex channel contract. Water and bridges never enter the splat.
public enum GroundSurface { Grass, Sand, Mountain, Forest, Snow, Marsh, Ash, Dirt, Cobble, Water, Bridge }

[CreateAssetMenu(menuName = "Game/Art/Painted Ground Style")]
public sealed class PaintedGroundStyle : ScriptableObject
{
    public Material DryGround;
    public Material Water;
    public Material Bridge;
    public Mesh GrassLip;
    public Material GrassLipMaterial;
    public int Revision = 1;
    public static PaintedGroundStyle Load() => Resources.Load<PaintedGroundStyle>("EnvironmentKit/DioramaGround");
    public static GroundSurface Surface(OverworldBiome biome) => biome switch {
        OverworldBiome.Desert => GroundSurface.Sand, OverworldBiome.Water => GroundSurface.Water,
        OverworldBiome.Mountain => GroundSurface.Mountain, OverworldBiome.Forest => GroundSurface.Forest,
        OverworldBiome.Tundra => GroundSurface.Snow, OverworldBiome.Marsh => GroundSurface.Marsh,
        OverworldBiome.Volcanic => GroundSurface.Ash, _ => GroundSurface.Grass
    };
    public static readonly string[] TextureNames = { "Grass", "Sand", "Mountain", "Forest", "Snow", "Marsh", "Ash", "Dirt", "Cobble" };
}
