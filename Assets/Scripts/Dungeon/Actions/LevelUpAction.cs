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
				(stats, vitals) =>
				{
					stats.Strength += 2;
					stats.HPMax += 5;
					//vitals.Level = levelUp.Level;
					vitals.HP += 5;
				},
				false)
		};
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		AudioManager.Instance.SoundEffects.LevelUp.PlayAsSound();
		Game.Instance.DoFloatingText("Level Up", Color.yellow, character.transform.position);
		yield return new WaitForSecondsRealtime(1.0f);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}