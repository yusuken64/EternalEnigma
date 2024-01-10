using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Skill : MonoBehaviour
{
	public abstract int SPCost { get; }
	public abstract string SkillName { get; }

	internal abstract List<GameAction> GetEffects(Character caster);
	internal abstract IEnumerator ExecuteRoutine(Character caster);

	internal virtual bool IsValid(Character caster)
	{
		return caster.Vitals.SP >= SPCost;
	}
}
