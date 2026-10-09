using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class AllyCombat
{
    internal static bool IsVisibleHostile(Game game, Ally ally, Character target) =>
        target != null && target != ally && target.Vitals.HP > 0 && target.Team != ally.Team &&
        target.Team != Team.Neutral && !EnemyBehavior.IsDisguised(target) &&
        game.AllCharacters.Contains(target) && game.CurrentDungeon.CanSee(ally, target);

    internal static List<Character> VisibleHostiles(Game game, Ally ally) => game.AllCharacters
        .Where(c => IsVisibleHostile(game, ally, c)).OrderBy(c => c.TilemapPosition.y)
        .ThenBy(c => c.TilemapPosition.x).ThenBy(c => c.GetInstanceID()).ToList();

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
        var sight = game.CurrentDungeon.GetVisibleTiles(ally, origin);
        if (ally.IsRangedAttack(out var projectile))
        {
			if (ally.GetActionInterupt(new RangedAttackAction())) return result;
            bool bow = ArrowSupply.HasBow(ally);
            if (bow && ArrowSupply.Count(ally) < 1) return result;
            foreach (var direction in SkillCastOptions.Directions)
            {
                var line = MissileTargeting.TraceLineFrom(ally, origin, direction,
                    10 + ClassPassives.MissileRangeBonus(ally), bow ? ArrowSupply.Penetration(ally) : 1);
                // Penetrating shots must not knowingly hit neutral, disguised or unseen units.
                if (line.Encounters.Count == 0 || line.Encounters.Any(h => !enemies.Contains(h.Character) ||
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
            foreach (var enemy in enemies)
            {
                var cells = new List<Vector3Int>();
                foreach (var cell in enemy.ToBounds().allPositionsWithin)
                    if (Character.Contains2D(bounds, cell) && sight.Contains(cell)) cells.Add(cell);
                if (cells.Count > 0) result.Add(new Attack { Target = enemy, Cell = cells[0],
                    Value = SkillEstimates.NormalAttackExpected(ally, enemy) });
            }
        }
        return result;
    }

    internal static Attack ChooseAttack(Game game, Ally ally) => AttacksFrom(game, ally, ally.TilemapPosition)
        .OrderByDescending(a => a.Target == ally.PursuitTarget).ThenBy(a => a.Target.TilemapPosition.y)
        .ThenBy(a => a.Target.TilemapPosition.x).ThenBy(a => a.Target.GetInstanceID()).FirstOrDefault();
}
