using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class LevelUpAction : GameAction
{
	public LevelUpAction() { }
	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		// Allies grow by class; enemies and classless heroes keep the flat +2 Strength / +5 HPMax.
		var growth = HeroClass.Growth(character is Ally ally ? ally.PrimaryClass : null);
		return new()
		{
			new ModifyStatAction(
				character,
				character,
				(stats, vitals) =>
				{
					stats.HPMax += growth.HPMax;
					stats.SPMax += growth.SPMax;
					stats.HungerMax += growth.HungerMax;
					stats.Strength += growth.Strength;
					stats.Defense += growth.Defense;
					vitals.HP += growth.HPMax;
					vitals.SP += growth.SPMax;
				},
				false)
		};
	}

    internal override void RecordOutcome(Character character) { GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} leveled up."); }

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		AudioManager.Instance.SoundEffects.LevelUp.PlayAsSound();
        DungeonFloatingText.Show(Game.Instance, "Level Up", new Color(.84f,.76f,1f), character.transform.position, true);

		yield return new WaitForSecondsRealtime(1.0f);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}