using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
	public TownSaveData TownSaveData = new();
	public DungeonSaveData DungeonSaveData = new();
}

[Serializable]
public class DungeonSaveData
{
	public bool ReturnCommitted;
	public int StartFloor;
	public int EndFloor;
}

[Serializable]
public class TownSaveData
{
	public string ConfigurationId;
	public int RestockVersion;
	public List<TownShopSaveData> Shops = new();
	public List<string> CompletedTiers = new();
	// JsonUtility turns missing lists into empty lists, so migration requires an explicit format flag.
	public int InventoryFormatVersion;
	public List<ItemSaveData> InventoryItems;
	public int TownSeed = 0; //0 means uninitialzed;
	public int Gold = 100;
	public int DonationTotal;
	public List<string> Inventory = new();
	public List<TownAllyData> RecruitedAlliesData = new();
}

[Serializable]
public class TownAllyData
{
	public string AllyId;
	public string AllyName;
	public List<string> Skills;
	public List<ItemSaveData> Equipment = new();
}
