using System;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

[Serializable] public sealed class EnvironmentModel { public string Id; public Mesh Mesh; public int Triangles; }
[Serializable] public sealed class EnvironmentPalette { public OverworldBiome Biome; public Material Props; public Material Buildings; public Material Ground; }

[CreateAssetMenu(menuName = "Game/Art/Environment Kit")]
public sealed class EnvironmentKit : ScriptableObject
{
    public EnvironmentModel[] Models = Array.Empty<EnvironmentModel>();
    public EnvironmentPalette[] Palettes = Array.Empty<EnvironmentPalette>();
    public Material Paving;
    public Material Road;
    public Material Shore;
    public TreeModelPicker TreeModels;
    public Mesh Mesh(string id) => Models.First(m => m.Id == id).Mesh;
    public int Triangles(string id) => DioramaCatalog.Load()?.Get(id)?.Triangles ?? Models.First(m => m.Id == id).Triangles;
    public Material Material(OverworldBiome biome) => Palettes.First(p => p.Biome == biome).Props;
    public Material BuildingMaterial(OverworldBiome biome) => Palettes.First(p => p.Biome == biome).Buildings;
    public Material Surface(string id, OverworldBiome biome) => id.StartsWith("SmartShore") ? Shore : id.StartsWith("SmartRoad") ? Road : id == "Paving" ? Paving :
        id.StartsWith("SmartHouse") || id == "House" || id == "Inn" || id == "Shop" || id == "Trainer" ? BuildingMaterial(biome) : Material(biome);
    public Material Ground(OverworldBiome biome) => Palettes.First(p => p.Biome == biome).Ground;
    public static EnvironmentKit Load() => Resources.Load<EnvironmentKit>("EnvironmentKit/Kit");

    public GameObject Create(string id, OverworldBiome biome, Transform parent, Vector3 position, float size = 1)
    {
        var obj = new GameObject(id); obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position; obj.transform.localScale = Vector3.one * size;
        obj.AddComponent<MeshFilter>().sharedMesh = Mesh(id);
        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Surface(id,biome);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        obj.AddComponent<SilhouetteParticipant>().Role = id.Contains("Water") || id.Contains("Shore") ? SilhouetteRole.None :
            id.Contains("Bridge") || id.Contains("Paving") || id.Contains("Road") ? SilhouetteRole.Receiver : SilhouetteRole.Caster;
        return obj;
    }
}
