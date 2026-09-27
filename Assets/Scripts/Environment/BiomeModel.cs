using EternalEnigma.Core.World;
using UnityEngine;

public sealed class BiomeModel : MonoBehaviour
{
    public EnvironmentKit Kit;
    public string ModelId;
    public void Apply(OverworldBiome biome) => GetComponent<MeshRenderer>().sharedMaterial = Kit.Surface(ModelId,biome);
    public static void ApplyAll(GameObject root, OverworldBiome biome)
    {
        foreach (var model in root.GetComponentsInChildren<BiomeModel>(true)) model.Apply(biome);
    }
}
