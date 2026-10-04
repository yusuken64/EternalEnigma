using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Town/Interior catalog")]
public sealed class TownInteriorCatalog : ScriptableObject
{
    public TownInteriorAsset[] Assets = Array.Empty<TownInteriorAsset>();
    public Material[] BiomeMaterials = Array.Empty<Material>();
    public Material CharacterMaterial;
    public TileWorldCreator4TilesPreset Carpet, Counter;
    public TownNpcDefinition[] Characters = Array.Empty<TownNpcDefinition>();
    public static TownInteriorCatalog Load() => Resources.Load<TownInteriorCatalog>("TownInteriors/Catalog");
    public TownInteriorAsset Get(string id) => Assets.FirstOrDefault(a => a.Id == id);
    public Material Material(OverworldBiome biome) => BiomeMaterials[(int)biome % BiomeMaterials.Length];
}
[Serializable] public sealed class TownInteriorAsset
{
    public string Id;
    public Mesh Mesh;
    public GameObject Prefab;
}
