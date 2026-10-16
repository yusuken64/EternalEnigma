using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class TestScenario
{
    public string AllyName = "Rowan";
    public string[] AdditionalAllies = Array.Empty<string>();
    public string[] Items = Array.Empty<string>();
    public string[] Skills = Array.Empty<string>();
    public int Gold = 100;
    public int Seed = 12345;
    public int StartFloor = 1;
    public int EndFloor = 5;
    public bool IncludeStartingItems;
    public EternalEnigma.Core.World.DungeonVariant Variant;
    public EternalEnigma.Core.World.OverworldBiome Biome;
    public int Tier;
    public int? HP;
    public int? Mana;

    public GameSaveData CreateSave()
    {
        if (StartFloor < 1 || EndFloor < StartFloor)
            throw new ArgumentException("Expected 1 <= StartFloor <= EndFloor.");
        return new GameSaveData {
            DungeonSaveData = new DungeonSaveData { StartFloor = StartFloor, EndFloor = EndFloor, Variant = Variant,
                UseBiomeLayout = Variant == EternalEnigma.Core.World.DungeonVariant.Town, LayoutBiome = Biome, LayoutTier = Tier },
            TownSaveData = new TownSaveData {
                Gold = Gold, TownSeed = Seed,
                InventoryItems = Items.Select(name => new ItemSaveData { ItemName = name }).ToList(),
                RecruitedAlliesData = new List<TownAllyData> {
                    new TownAllyData { AllyName = AllyName, Skills = new List<string>(Skills) }
                }
            }
        };
    }
}
