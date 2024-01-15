using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
	public OverworldSaveData OverworldSaveData = new();
}

[Serializable]
public class OverworldSaveData
{
	public int Gold = 1000;
	public int DonationTotal;
	public List<string> Inventory = new();
	public List<OverworldAlly> RecruitedAllies = new();
}