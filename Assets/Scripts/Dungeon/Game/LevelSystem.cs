using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelSystem : MonoBehaviour
{
	public List<LevelInfo> LevelData;

	internal float GetPercentageToNextLevel(Vitals displayedVitals)
	{
		var current = LevelData.First(x => x.Level == displayedVitals.Level);
		var next = LevelData.First(x => x.Level == displayedVitals.Level + 1);

		if (current == null ||
			next == null)
		{
			return 1.0f;
		}

		var levelExperience = current.Experience - displayedVitals.Exp;
		var nextLevelExperience = current.Experience - next.Experience;

		return (float)levelExperience / nextLevelExperience;
	}
}

[System.Serializable]
public class LevelInfo
{
	public int Level;
	public int Experience;
	public int BaseAttack;
}