using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A Scout can follow a route to undiscovered stairs, one normal movement action per turn.</summary>
[Serializable]
public sealed class FindExitAction : GameAction, ISkillCastCondition
{
    [NonSerialized] private Ally scout;
    public bool CanCast(Character caster, out string reason)
    {
        bool valid = ScoutExitNavigation.CanBegin(caster as Ally);
        reason = valid ? null : "No safe route to the exit";
        return valid;
    }
    internal override GameAction AsTargetedSkill(Character caster, Character target) => new FindExitAction { scout = caster as Ally };
    internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => AsTargetedSkill(caster, target);
    internal override bool IsValid(Character character) => ScoutExitNavigation.CanBegin(scout ?? character as Ally);
    internal override List<GameAction> ExecuteImmediate(Character character)
    {
        var ally = scout ?? character as Ally;
        if (!ScoutExitNavigation.CanBegin(ally)) return new();
        var navigation = ally.GetComponent<ScoutExitNavigation>() ?? ally.gameObject.AddComponent<ScoutExitNavigation>();
        navigation.Begin(ally);
        return new();
    }
    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
}
