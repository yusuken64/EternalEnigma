using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Each affected turn chooses a direction, bumping, walking, or attacking indiscriminately.</summary>
public sealed class ConfusionStatusEffect : StatusEffect
{
    internal override string GetEffectName() => "Confusion";
    internal override bool PreventsMenu() => !IsExpired();
    internal override StatModification GetStatModification() => null;
    public override GameAction GetActionOverride(Character character) => IsExpired() ? null : new ConfusedTurnAction();

    private sealed class ConfusedTurnAction : GameAction
    {
        internal override bool IsValid(Character c) => c != null && c.Vitals.HP > 0;
        internal override List<GameAction> ExecuteImmediate(Character c)
        {
            var directions = (Facing[])System.Enum.GetValues(typeof(Facing));
            var direction = directions[Random.Range(0, directions.Length)];
            c.SetFacing(direction);
            var next = c.TilemapPosition + GridMovement.GetFacingOffset(direction);
            var dungeon = Game.Instance.CurrentDungeon;
            var occupant = dungeon.OverlapsAnyOtherCharacter(c, Character.ToBounds(next));
            if (occupant != null && AttackPolicy.CanAttack(Game.Instance, occupant, c))
                return new() { new AttackAction(c, c.TilemapPosition, next) };
            var move = new MovementAction(c, c.TilemapPosition, next);
            return occupant == null && move.IsValid(c) ? new() { move } : new() { new WaitAction() };
        }
        internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false) { yield break; }
    }
}
