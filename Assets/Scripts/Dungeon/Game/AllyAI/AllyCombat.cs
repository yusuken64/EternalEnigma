using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class AllyCombat
{
    internal static bool IsVisibleHostile(Game game, Ally ally, Character target) =>
        target != null && target != ally && target.Vitals.HP > 0 && target.Team != ally.Team &&
        target.Team != Team.Neutral && !EnemyBehavior.IsDisguised(target) && !EncounterActor.Protected(target) &&
        game.ContainsCharacter(target) && game.CurrentDungeon.CanSee(ally, target);

    // Keys (y, x, instance id) are unique per object, so an unstable Sort yields the same order as OrderBy/ThenBy.
    static readonly System.Comparison<Character> HostileOrder = (a, b) =>
    {
        int c = a.TilemapPosition.y.CompareTo(b.TilemapPosition.y);
        if (c != 0) return c;
        c = a.TilemapPosition.x.CompareTo(b.TilemapPosition.x);
        if (c != 0) return c;
        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    };

    internal static List<Character> VisibleHostiles(Game game, Ally ally)
    {
        var result = new List<Character>();
        foreach (var c in game.Allies) if (IsVisibleHostile(game, ally, c)) result.Add(c);
        foreach (var c in game.Enemies) if (IsVisibleHostile(game, ally, c)) result.Add(c);
        if (result.Count > 1) result.Sort(HostileOrder);
        return result;
    }

    internal sealed class Attack
    {
        internal Character Target;
        internal Vector3Int Cell;
        internal Vector3Int? Direction;
        internal float Value;
        internal GameObject Projectile;
        internal GameAction ToAction(Ally ally)
        {
            ally.SetFacingByTargetPosition(Direction.HasValue ? ally.TilemapPosition + Direction.Value : Cell);
            return Direction.HasValue ? new RangedAttackAction(ally, null, ally.FinalStats.Strength, Projectile) :
                new AttackAction(ally, ally.TilemapPosition, Cell);
        }
    }

    // Both damage scoring and the attack policy use this exact availability check.
    internal static List<Attack> AttacksFrom(Game game, Ally ally, Vector3Int origin, IReadOnlyList<Character> visibleEnemies = null)
    {
        var result = new List<Attack>();
        var enemies = visibleEnemies ?? VisibleHostiles(game, ally);
        if (enemies.Count == 0) return result;
        if (ally.IsRangedAttack(out var projectile))
        {
			if (ally.GetActionInterupt(new RangedAttackAction())) return result;
            bool bow = ArrowSupply.HasBow(ally);
            if (bow && ArrowSupply.Count(ally) < 1) return result;
            int range = 10 + ClassPassives.MissileRangeBonus(ally);
            int penetration = bow ? ArrowSupply.Penetration(ally) : 1;
            HashSet<Vector3Int> sight = null;
            foreach (var direction in SkillCastOptions.Directions)
            {
                if (!HasTargetOnRay(origin, direction, range, enemies)) continue;
                var line = MissileTargeting.TraceLineFrom(ally, origin, direction, range, penetration);
                if (line.Encounters.Count == 0) continue;
                sight ??= game.CurrentDungeon.GetVisibleTiles(ally, origin);
                // Penetrating shots must not knowingly hit neutral, disguised or unseen units.
                if (line.Encounters.Any(h => !enemies.Contains(h.Character) ||
                    !DungeonSight.OverlapsVisible(sight, h.Character.ToBounds()))) continue;
                result.Add(new Attack { Target = line.Encounters[0].Character, Cell = line.Encounters[0].Cell,
                    Direction = direction, Projectile = projectile,
                    Value = line.Encounters.Sum(h => SkillEstimates.NormalAttackExpected(ally, h.Character)) });
            }
        }
        else
        {
			if (ally.GetActionInterupt(new AttackAction())) return result;
            var bounds = Character.ToBounds(origin, ally.FootPrint == FootPrint.Size3x3 ? new Vector3Int(5,5,1) : new Vector3Int(3,3,1));
            HashSet<Vector3Int> sight = null;
            foreach (var enemy in enemies)
            {
                var targetBounds = enemy.ToBounds();
                if (!bounds.Overlaps2D(targetBounds)) continue;
                sight ??= game.CurrentDungeon.GetVisibleTiles(ally, origin);
                foreach (var cell in targetBounds.allPositionsWithin)
                {
                    if (!Character.Contains2D(bounds, cell) || !sight.Contains(cell)) continue;
                    result.Add(new Attack { Target = enemy, Cell = cell, Value = SkillEstimates.NormalAttackExpected(ally, enemy) });
                    break;
                }
            }
        }
        return result;
    }

    private static bool HasTargetOnRay(Vector3Int origin, Vector3Int direction, int range, IReadOnlyList<Character> targets)
    {
        for (int i = 0; i < targets.Count; i++)
            foreach (var cell in targets[i].ToBounds().allPositionsWithin)
            {
                var delta = cell - origin;
                int steps = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                if (steps > 0 && steps <= range && delta.x == direction.x * steps && delta.y == direction.y * steps) return true;
            }
        return false;
    }

    internal static Attack ChooseAttack(Game game, Ally ally) => AttacksFrom(game, ally, ally.TilemapPosition)
        .OrderByDescending(a => a.Target == ally.PursuitTarget).ThenBy(a => a.Target.TilemapPosition.y)
        .ThenBy(a => a.Target.TilemapPosition.x).ThenBy(a => a.Target.GetInstanceID()).FirstOrDefault();
}
