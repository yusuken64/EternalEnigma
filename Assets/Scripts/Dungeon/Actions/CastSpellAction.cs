using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class CastSpellAction : GameAction
{
	public Func<List<GameAction>> GetActionsFunc { get; internal set; }
	public AudioClip CastSound { get; internal set; }

	public string SpellName = "Spell";
    public Character Target;
    private bool releasing;
    private bool started;
    private bool resolved;
    private bool releaseSoundPlayed;
    private Team originalTeam;
    private UnityEngine.Object floor;
    public CastSpellAction() {}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!releasing)
        {
            if (!IsValid(character)) return new();
            started = true;
            QueueSound(SoundCue.AbilityStart);
            var release = new CastSpellAction { releasing = true, SpellName = SpellName, Target = Target,
                originalTeam = Target.Team, floor = Game.Instance.CurrentDungeon, GetActionsFunc = GetActionsFunc, CastSound = CastSound };
            character.PendingCast = new PendingCast(release, SpellName, 1);
            GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} starts charging {GameplayText.Name(SpellName)}. It will be released on the next action.");
            return new();
        }
        if (floor != Game.Instance.CurrentDungeon || Target == null || Target.Vitals.HP <= 0 || Target.Team != originalTeam || !Game.Instance.AllCharacters.Contains(Target))
        { GameMessages.ForCharacter(character, $"{GameMessages.Name(character)}'s {GameplayText.Name(SpellName)} fizzled because the target is no longer available."); return new(); }
        GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} releases {GameplayText.Name(SpellName)}.");
        resolved = true;
        return GetActionsFunc?.Invoke() ?? new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation && started) CombatEffectPlayer.Get()?.ShowCasting(character, CombatVisualCatalog.Instance?.Utility, true);
        if (releasing) CombatEffectPlayer.Get()?.ClearCasting(character);
		PlayReleaseSound(character);
		yield break;
	}

	internal override bool IsValid(Character character)
	{
		return character != null && Target != null && Target.Vitals.HP > 0 && GetActionsFunc != null;
	}

    internal void PlayReleaseSound(Character actor)
    {
        if (releaseSoundPlayed || !resolved) return;
        releaseSoundPlayed = true;
        if (ShouldPlayAudio(actor)) AudioManager.Instance?.PlaySoundEffect(CastSound);
    }
}
