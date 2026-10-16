using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ResetFloorAction : GameAction, ISkillEffectPrecondition
{
    internal static Skill Skill()
    {
        var skill = ScriptableObject.CreateInstance<Skill>();
        skill.SkillName = skill.name = "Reset Floor"; skill.ActivationType = ActivationType.Active;
        skill.ManaCost = 0; skill.MaxRank = 1; skill.Targeting = SkillTargeting.Self;
        skill.SoundFamily = AbilitySoundFamily.Utility;
        skill.CastSound = AudioManager.Instance?.SoundEffects?.UseItem;
        skill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.Self };
        skill.Description = "Restore a puzzle floor and the party to their floor-entry state. No Mana cost.";
        skill.ActionEffects = new List<GameAction> { new ResetFloorAction() }; return skill;
    }
    internal override GameAction AsTargetedSkill(Character caster, Character target) => new ResetFloorAction();
    internal override bool IsValid(Character character) => Game.Instance?.CanResetFloor == true;
    internal override List<GameAction> ExecuteImmediate(Character character) { Game.Instance.ResetFloor(); return new(); }
    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
    public bool CanUse(Character caster, out string reason)
    { bool allowed = IsValid(caster); reason = allowed ? null : "Reset Floor is available only on puzzle floors after clearing the first repeatable dungeon."; return allowed; }
}
