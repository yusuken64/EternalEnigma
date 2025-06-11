using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class SleepTurnAction : GameAction
{
	private Character effectedCharacter;

	public SleepTurnAction(Character character)
	{
		this.effectedCharacter = character;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }
		if (effectedCharacter == Game.Instance.PlayerController)
		{
			Game.Instance.DoFloatingText("Sleeping...", Color.white, effectedCharacter.VisualParent.transform.position);
			yield return new WaitForSecondsRealtime(1f);
		}
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}