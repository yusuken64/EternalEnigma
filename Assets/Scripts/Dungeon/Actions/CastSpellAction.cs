using System;
using System.Collections;
using System.Collections.Generic;

internal class CastSpellAction : GameAction
{
	public Func<List<GameAction>> GetActionsFunc { get; internal set; }

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return GetActionsFunc?.Invoke();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return true;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}