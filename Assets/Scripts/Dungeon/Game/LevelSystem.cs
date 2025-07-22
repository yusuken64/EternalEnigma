using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelSystem : MonoBehaviour
{
	private static readonly int[] expTable = new int[]
	{
		0, 10, 30, 60, 100, 150, 230, 350, 500, 700,
		950, 1200, 1500, 1800, 2300, 3000, 4000, 6000, 9000, 15000,
		23000, 33000, 45000, 60000, 80000, 100000, 130000, 180000, 240000, 300000,
		400000, 500000, 600000, 700000, 800000, 900000, 999999
	};

	// Returns progress [0..1] toward next level based on current level and EXP
	public float GetPercentageToNextLevel(Vitals displayedVitals)
	{
		int level = displayedVitals.Level;
		int exp = displayedVitals.Exp;

		// Clamp level to valid range of the table
		if (level < 1) level = 1;
		if (level >= expTable.Length) return 1.0f; // Max level reached

		int currentLevelExp = expTable[level - 1];
		int nextLevelExp = expTable[level];

		int expIntoLevel = exp - currentLevelExp;
		int expNeeded = nextLevelExp - currentLevelExp;

		if (expNeeded <= 0) return 1.0f; // Prevent div by zero
		if (expIntoLevel < 0) expIntoLevel = 0;
		if (expIntoLevel > expNeeded) expIntoLevel = expNeeded;

		return (float)expIntoLevel / expNeeded;
	}

	public List<LevelInfo> GetLevelUps(int currentLevel, int currentExp)
	{
		var levelUps = new List<LevelInfo>();

		// Start checking from the next level after currentLevel
		for (int level = currentLevel + 1; level <= expTable.Length; level++)
		{
			if (currentExp >= expTable[level - 1])
			{
				levelUps.Add(new LevelInfo
				{
					Level = level,
					Experience = expTable[level - 1]
				});
			}
			else
			{
				// Since expTable is sorted ascending, no need to check further
				break;
			}
		}

		return levelUps;
	}
}

[System.Serializable]
public class LevelInfo
{
	public int Level;
	public int Experience;
}