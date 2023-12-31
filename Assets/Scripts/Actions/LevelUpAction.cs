using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class LevelUpAction : GameAction
{
	private readonly Character character;
	private LevelInfo levelUp;

	public LevelUpAction(Character character, LevelInfo levelUp)
	{
		this.character = character;
		this.levelUp = levelUp;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return new()
		{
			new ModifyStatAction(
				character,
				character,
				(x) =>
				{
					x.BaseStats.Level = levelUp.Level;
					x.BaseStats.Strength += 2;
					x.BaseStats.HPMax += 5;
					x.BaseStats.HP += 5;
				},
				false)
		};
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		Game.Instance.DoFloatingText("Level Up", Color.yellow, character.transform.position);
		yield return new WaitForSecondsRealtime(1.0f);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}