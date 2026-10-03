using System;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

public enum BiomeDecorationKind { Fixture, Ornament, Accent, LampPost, SignPost, SignPanel }
[Serializable] public sealed class BiomeFacadeSocket
{
    public Vector3 Center,Normal;
    public float Width,Height;
    public BiomeDecorationKind Kind;
}
[Serializable] public sealed class BiomeFacadeDefinition
{
    public string Model;
    public Bounds[] Exclusions=Array.Empty<Bounds>();
    public BiomeFacadeSocket[] Sockets=Array.Empty<BiomeFacadeSocket>();
}
[Flags] public enum DecorationSurface { BuiltWall = 1, NaturalWall = 2, Facade = 4, Verge = 8 }
[Serializable]
public sealed class BiomeDecorationAsset
{
    public OverworldBiome Biome;
    public BiomeDecorationKind Kind;
    public Mesh Mesh;
    public GameObject Prefab;
    public GameObject Effect;
    public Vector3 MountOffset;
    public Vector3 EffectOffset;
    public DecorationSurface Surfaces;
    public float TownScale = .8f, OverworldScale = .45f, DungeonScale = .7f;
    public Bounds Bounds => Mesh.bounds;
}
[CreateAssetMenu(menuName = "Game/Environment/Biome Decorations")]
public sealed class BiomeDecorationCatalog : ScriptableObject
{
    public Material Material;
    public BiomeDecorationAsset[] Assets = Array.Empty<BiomeDecorationAsset>();
    public BiomeFacadeDefinition[] Facades = Array.Empty<BiomeFacadeDefinition>();
    public BiomeDecorationAsset Get(OverworldBiome biome, BiomeDecorationKind kind) => Assets.Single(a => a.Biome == biome && a.Kind == kind);
    public static BiomeDecorationCatalog Load() => Resources.Load<BiomeDecorationCatalog>("BiomeDecorations/Catalog");
}
