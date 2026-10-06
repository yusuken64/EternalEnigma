using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class LevelUpAction : GameAction
{
	private readonly Character recipient;
	public LevelUpAction() { }
	public LevelUpAction(Character recipient) { this.recipient = recipient; }
	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		character = recipient != null ? recipient : character;
		TrackAnimationTarget(character);
		// Allies grow by class; enemies and classless heroes keep the flat +2 Strength / +5 HPMax.
		var growth = HeroClass.Growth(character is Ally ally ? ally.PrimaryClass : null);
		return new()
		{
			new ModifyStatAction(
				character,
				character,
				(stats, vitals) =>
				{
					HeroClass.ApplyLevelGrowth(stats, character is Ally hero ? hero.PrimaryClass : null, 1);
					vitals.HP += growth.HPMax;
					vitals.SP += growth.SPMax;
				},
				false)
		};
	}

    internal override void RecordOutcome(Character character) { character = recipient != null ? recipient : character; GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} leveled up."); }

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		character = recipient != null ? recipient : character;
		if (skipAnimation) yield break;
		(character as Ally)?.HeroAnimator?.PlayOneShot(AnimatedAction.LevelUp);
		AudioManager.Instance.SoundEffects.LevelUp.PlayAsSound();
        DungeonFloatingText.Show(Game.Instance, "Level Up", new Color(.84f,.76f,1f), character, true);

		yield return new WaitForSecondsRealtime(1.0f);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
