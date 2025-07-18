using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
	public OverworldSaveData OverworldSaveData = new();
	public int StartingFloor;
}

[Serializable]
public class OverworldSaveData
{
	public int OverworldSeed = 0; //0 means uninitialzed;
	public int Gold = 100;
	public int DonationTotal;
	public List<string> Inventory = new();
	public List<OverworldAlly> RecruitedAllies = new();
	public List<string> ActiveSkillNames = new();
	public int ActiveSkillMax = 1;
}