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
    private Team originalTeam;
    private UnityEngine.Object floor;
    public CastSpellAction() {}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!releasing)
        {
            started = true;
            var release = new CastSpellAction { releasing = true, SpellName = SpellName, Target = Target,
                originalTeam = Target.Team, floor = Game.Instance.CurrentDungeon, GetActionsFunc = GetActionsFunc };
            character.PendingCast = new PendingCast(release, SpellName, 1);
            GameMessages.ForCharacter(character, $"[Cast] {GameMessages.Name(character)} starts {SpellName} (1 charging action).");
            return new();
        }
        if (floor != Game.Instance.CurrentDungeon || Target == null || Target.Vitals.HP <= 0 || Target.Team != originalTeam || !Game.Instance.AllCharacters.Contains(Target))
        { GameMessages.ForCharacter(character, $"[Cast] {GameMessages.Name(character)}'s {SpellName} fizzled: invalid target."); return new(); }
        GameMessages.ForCharacter(character, $"[Cast] {GameMessages.Name(character)} releases {SpellName}.");
        return GetActionsFunc?.Invoke();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation && started) CombatEffectPlayer.Get()?.ShowCasting(character, CombatVisualCatalog.Instance?.Utility, true);
        if (releasing) CombatEffectPlayer.Get()?.ClearCasting(character);
        if (!skipAnimation && started && CastSound != null) AudioManager.Instance?.PlaySoundEffect(CastSound);
		yield break;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
