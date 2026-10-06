using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Runtime only: one primary action advances one charging step, never a generated effect.
internal sealed class PendingCast
{
    internal readonly GameAction Release;
    internal readonly string Name;
    internal readonly UnityEngine.Object Floor;
    internal readonly Skill Skill;
    internal int Remaining;
    internal PendingCast(GameAction release, string name, int charge, Skill skill = null)
    { Release = release; Name = name; Remaining = Math.Max(0, charge - 1); Skill = skill; Floor = Game.Instance?.CurrentDungeon; }
    internal bool Reduce() { if (Remaining <= 0) return false; Remaining--; return true; }
    internal static bool Mobile(Character c) => c is Ally && c.Skills != null && c.Skills.Any(s => s != null && s.ActivationType == ActivationType.Passive && s.SkillName == "Mobile Casting");
    internal static void Cancel(Character c, string reason)
    {
        if (c == null || c.PendingCast == null) return;
        var name = c.PendingCast.Name;
        c.PendingCast = null;
        CombatEffectPlayer.Get()?.ClearCasting(c);
        GameMessages.ForCharacter(c, $"[Cast] {GameMessages.Name(c)}'s {name} was interrupted: {reason}.");
    }
    internal static void Validate(Character c)
    {
        if (c == null) return;
        var pending = c.PendingCast;
        if (pending == null) return;
        if (c.Vitals == null || c.Vitals.HP <= 0) { Cancel(c, "downed or dead"); return; }
        if (pending.Floor != Game.Instance?.CurrentDungeon) { Cancel(c, "floor changed"); return; }
        var blocking = c.StatusEffects.FirstOrDefault(s => s != null && !s.IsExpired() &&
            (s.PreventsMenu() || s.Interupts(pending.Release) ||
             s is FearStatusEffect || s is ConfusionStatusEffect));
        if (blocking != null) Cancel(c, blocking.GetEffectName());
    }
}

internal sealed class AdvanceCastAction : GameAction
{
    internal override bool IsValid(Character c) => c.PendingCast != null;
    internal override List<GameAction> ExecuteImmediate(Character c)
    {
        PendingCast.Validate(c);
        var cast = c.PendingCast;
        if (cast == null) return new();
        if (cast.Remaining > 0) { cast.Remaining--; return new(); }
        if (cast.Skill?.UsesArrows == true && (!ArrowSupply.HasBow(c) || ArrowSupply.Count(c) < ArrowSupply.RequiredToCast(cast.Skill)))
        {
            PendingCast.Cancel(c, "no arrows");
            if (c == Game.Instance.PlayerController.ControlledAlly) GameMessages.Post("no arrows", true);
            return new();
        }
        c.PendingCast = null; // Effects can start or interrupt another cast safely.
        CombatEffectPlayer.Get()?.ClearCasting(c);
        return new() { cast.Release };
    }
    internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false) { yield break; }
}

[Serializable]
public sealed class ReduceCastAction : GameAction
{
    private Character target;
    internal override GameAction AsTargetedSkill(Character caster, Character recipient) => new ReduceCastAction { target = recipient };
    internal override GameAction AsTargetedSkill(Character caster, Character recipient, SkillRankContext rank) => AsTargetedSkill(caster, recipient);
    internal override bool IsValid(Character c) => target?.PendingCast?.Remaining > 0;
    internal override List<GameAction> ExecuteImmediate(Character c) { target?.PendingCast?.Reduce(); return new(); }
    internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false) { yield break; }
}
