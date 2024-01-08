using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Skill : MonoBehaviour
{
	public abstract int SPCost { get; }
	public abstract string SkillName { get; }

	internal abstract List<GameAction> GetEffects(Player player);
	internal abstract IEnumerator ExecuteRoutine(Player player);

	internal virtual bool IsValid(Player player)
	{
		return player.Vitals.SP >= SPCost;
	}
}
