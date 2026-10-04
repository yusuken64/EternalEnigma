using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class CastSpellAction : GameAction
{
	public Func<List<GameAction>> GetActionsFunc { get; internal set; }
	public AudioClip CastSound { get; internal set; }

	public CastSpellAction() {}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return GetActionsFunc?.Invoke();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation && CastSound != null) AudioManager.Instance?.PlaySoundEffect(CastSound);
		yield break;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
