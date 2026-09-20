using System;
using System.Collections.Generic;

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
    public int? HP;
    public int? SP;

    public GameSaveData CreateSave()
    {
        if (StartFloor < 1 || EndFloor < StartFloor)
            throw new ArgumentException("Expected 1 <= StartFloor <= EndFloor.");
        return new GameSaveData {
            DungeonSaveData = new DungeonSaveData { StartFloor = StartFloor, EndFloor = EndFloor },
            OverworldSaveData = new OverworldSaveData {
                Gold = Gold, OverworldSeed = Seed,
                Inventory = new List<string>(Items),
                RecruitedAlliesData = new List<OverworldAllyData> {
                    new OverworldAllyData { AllyName = AllyName, Skills = new List<string>(Skills) }
                }
            }
        };
    }
}
