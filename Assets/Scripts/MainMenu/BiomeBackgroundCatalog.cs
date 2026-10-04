using System;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Campaign biome backgrounds")]
public sealed class BiomeBackgroundCatalog : ScriptableObject
{
    [Serializable] public class Entry { public OverworldBiome Biome; public Texture2D Texture; }
    public Texture2D Neutral;
    public Entry[] Backgrounds = Array.Empty<Entry>();
    public Texture2D For(CampaignSaveSummary summary) => summary == null ? Neutral : Backgrounds.FirstOrDefault(b=>b.Biome==summary.Biome)?.Texture ?? Neutral;
    [ContextMenu("Validate background assignments")]
    public void Validate()
    {
        if (Neutral == null) throw new InvalidOperationException("Neutral campaign artwork is missing.");
        foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
            if(Backgrounds.Count(b=>b.Biome==biome && b.Texture!=null)!=1) throw new InvalidOperationException("Missing or duplicate background: "+biome);
    }
}
