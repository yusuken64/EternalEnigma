using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
	public OverworldSaveData OverworldSaveData = new();
	public DungeonSaveData DungeonSaveData = new();
}

[Serializable]
public class DungeonSaveData
{
	public int StartFloor;
	public int EndFloor;
}

[Serializable]
public class OverworldSaveData
{
	public int OverworldSeed = 0; //0 means uninitialzed;
	public int Gold = 100;
	public int DonationTotal;
	public List<string> Inventory = new();
	public List<OverworldAllyData> RecruitedAlliesData = new();
}

[Serializable]
public class OverworldAllyData
{
	public string AllyName;
	public List<string> Skills;
}