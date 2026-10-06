using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
    [NonSerialized] public bool IsSandbox;
    public EternalEnigma.Core.Progression.CampaignSnapshot Campaign;
    // JsonUtility can materialize an empty snapshot for standalone test runs.
    public bool HasCampaign => !string.IsNullOrEmpty(Campaign?.Fingerprint);
    public List<TownAllyData> Roster = new();
    public string ProtagonistId;
    public double PlaytimeSeconds;
    public string SavedUtc;
    public string SavePointId;
    public string ArrivalTownId;
    public string ArrivalHeroId;
    public bool HasArrival;
    public int ArrivalX, ArrivalY;
    public Facing ArrivalFacing;
    public List<string> VisitedRegions = new();
    public CampaignSaveSummary Summary;
    [NonSerialized] public bool NeedsInitialSave;
	public TownSaveData TownSaveData = new();
	public DungeonSaveData DungeonSaveData = new();
}

[Serializable]
public class DungeonSaveData
{
    public bool UseBiomeLayout;
    public int LayoutTier;
    public EternalEnigma.Core.World.OverworldBiome LayoutBiome;
    public DungeonVisualSelection VisualSelection;
	public bool ReturnCommitted;
	public int StartFloor;
	public int EndFloor;
}

[Serializable]
public class TownSaveData
{
	public string ConfigurationId;
	public int RestockCycle;
	public List<TownShopSaveData> Shops = new();
	public List<string> CompletedTiers = new();
	public List<ItemSaveData> InventoryItems = new();
	public int TownSeed = 0; //0 means uninitialzed;
	public int Gold = 100;
	public int DonationTotal;
	public List<TownAllyData> RecruitedAlliesData = new();
}

[Serializable]
public class TownAllyData
{
	public string AllyId;
	public string AllyName;
	// ClassDefinition.Id values; empty means "no class". For recruits the prefab is
	// authoritative; for the protagonist these saved values are authoritative.
	public string PrimaryClassId = "";
	public string SecondaryClassId = "";
	public List<string> Skills;
	public List<ItemSaveData> Equipment = new();
	// Parallel to Skills: a learned skill with no entry here is rank 1.
	public List<SkillRankSaveData> SkillRanks = new();
	// Highest dungeon level this hero has reached; gates trainer ranks and tiers.
	public int HighestLevel = 1;
    public int Level = 1;
    public int Experience;
	// Persisted vitals carried between dungeon runs; -1 means full (never set, or restored by the inn).
	public int Hp = -1;
	public int Sp = -1;
    // Explicit presence flag distinguishes old saves from genuine zero hunger.
    public bool HasHunger;
    public int Hunger;
    public int HungerAccumulate;
}

[Serializable]
public class SkillRankSaveData
{
	public string SkillName;
	public int Rank = 1;
}
