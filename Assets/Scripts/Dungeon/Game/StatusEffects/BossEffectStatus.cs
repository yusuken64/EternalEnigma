using System.Collections.Generic;
using UnityEngine;

/// <summary>Short-lived boss ailments and stances participate in normal status ticking and cleansing.</summary>
public sealed class BossEffectStatus : StatusEffect
{
    public string Kind;
    internal override string StackKey => base.StackKey + ":" + Kind;
    internal override bool BlocksMovement => !IsExpired() && Kind == "Root";
    internal override bool PreventsMenu() => !IsExpired() && Kind is "Sleep" or "Confusion";
    internal override bool Interupts(GameAction action) => !IsExpired() && Kind == "Silence" && action is SkillAction or CastSpellAction;
    internal override string GetEffectName() => Kind;
    internal override StatModification GetStatModification() => Kind == "Empowered" ? new StatModification { Strength = 12 } : null;
    public override GameAction GetActionOverride(Character character)
    {
        if (IsExpired()) return null;
        if (Kind == "Sleep") return new WaitAction();
        if (Kind == "Confusion")
        {
            var directions = new[] { Vector3Int.right,Vector3Int.up,Vector3Int.left,Vector3Int.down };
            var move = new MovementAction(character,character.TilemapPosition,character.TilemapPosition+directions[Random.Range(0,4)]);
            return move.IsValid(character) ? move : new WaitAction();
        }
        return null;
    }
    internal override List<GameAction> GetTickEffects(Character character) => Kind is "Poison" or "Bleed"
        ? new() { new TakeDamageAction(DamageSource,character,Mathf.Max(1,character.FinalStats.HPMax/12)) } : new();
    internal override void ModifyIncomingDamage(Character owner,DamageContext context)
    {
        if (context.Target != owner || IsExpired()) return;
        if (Kind is "Guard" or "Armored") context.Damage = Mathf.Max(1,context.Damage/4);
        if (Kind == "Exposed") context.Damage *= 2;
    }
    internal override IEnumerable<GameAction> GetResponseTo(Character owner,GameAction action)
    {
        if (Kind == "Guard" && !IsExpired() && action is TakeDamageAction hit && hit.Target == owner &&
            hit.Attacker != null && hit.Attacker != owner && hit.Damage>0 && !hit.Missed && DamageResponses.CanRespond(hit))
            yield return new TakeDamageAction(owner,hit.Attacker,Mathf.Max(1,owner.FinalStats.Strength)) { ResponseDepth=hit.ResponseDepth+1 };
    }
}
