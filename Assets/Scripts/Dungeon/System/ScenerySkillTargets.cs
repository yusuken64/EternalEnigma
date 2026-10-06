using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class ScenerySkillTargets
{
    internal static bool Damaging(Skill skill) => skill.ActionEffects.Any(a => a is ScaledDamageAction || a is DashStrikeAction || a is TeleportBehindAction || a is SongCountStrikeAction || a is TakeDamageAction d && d.damage > 0);
    internal static List<DungeonProp> Candidates(Character caster, Skill skill)
    {
        if (skill.Targeting == SkillTargeting.LockedDoor)
        {
            var sight = Game.Instance.CurrentDungeon.GetVisibleTiles(caster, caster.TilemapPosition);
            return Game.Instance.CurrentDungeon.Interactables.OfType<DungeonProp>().Where(p => p.IsClosedDoor && sight.Contains(p.Position) &&
                TileWorldDungeon.ChevDistance(p.Position, caster.TilemapPosition) <= skill.MissileRange).ToList();
        }
        if (!Damaging(skill) || skill.Targeting == SkillTargeting.InventoryItem || skill.TargetSelector == null ||
            skill.TargetSelector.Team == TargetTeam.Allies || skill.TargetSelector.Team == TargetTeam.Self) return new();
        var dungeon = Game.Instance.CurrentDungeon;
        var visible = dungeon.GetVisibleTiles(caster, caster.TilemapPosition);
        return dungeon.Interactables.OfType<DungeonProp>().Where(p => p.Alive &&
            (skill.TargetSelector.Area == TargetArea.All ||
             skill.TargetSelector.Area == TargetArea.Visible && visible.Contains(p.Position) ||
             skill.TargetSelector.Area == TargetArea.Melee && Character.Contains2D(caster.GetAttackBounds(),p.Position) && visible.Contains(p.Position))).ToList();
    }
    internal static List<DungeonProp> Affected(Character caster, Skill skill, Character selected, DungeonProp prop, Vector3Int missileCell)
    {
        var candidates = Candidates(caster, skill);
        if (skill.Targeting == SkillTargeting.LockedDoor) return candidates.Where(p => p == prop).ToList();
        if (skill.Targeting == SkillTargeting.AllTargets) return candidates;
        if (skill.Targeting == SkillTargeting.Missile) return Game.Instance.CurrentDungeon.Interactables.OfType<DungeonProp>()
            .Where(p => p.Alive && Damaging(skill) && TileWorldDungeon.ChevDistance(p.Position, missileCell) <= skill.AreaRadius).ToList();
        var center = skill.Targeting == SkillTargeting.Self ? caster.TilemapPosition : prop != null ? prop.Position : selected != null ? selected.TilemapPosition : caster.TilemapPosition;
        return candidates.Where(p => TileWorldDungeon.ChevDistance(p.Position, center) <= skill.AreaRadius).ToList();
    }
    internal static IEnumerable<GameAction> Effects(Character caster, Skill skill, DungeonProp prop, float multiplier = 1f)
    {
        foreach (var effect in skill.ActionEffects)
        {
            if (effect is UnlockDoorAction unlock && prop.IsClosedDoor) yield return unlock.For(prop);
            if (effect is TakeDamageAction damage && damage.damage > 0)
                yield return prop.Damage(caster, Mathf.RoundToInt(skill.RankScaling.ScalePower(damage.damage, skill.Rank) * multiplier));
            float percent = effect is DashStrikeAction dash ? dash.DamagePercent : effect is TeleportBehindAction teleport ? teleport.DamagePercent : effect is SongCountStrikeAction song ? song.PercentPerHit : 0;
            if(percent > 0 && (!(effect is TeleportBehindAction behind) || TileWorldDungeon.ChevDistance(caster.TilemapPosition,prop.Position)<=behind.MaxRange))
            {
                int hits = effect is SongCountStrikeAction ? Mathf.Max(1,SongRules.ActiveSongs(caster).Count) : 1;
                var strike=new ScaledDamageAction {Percent=percent};
                for(int i=0;i<hits;i++) yield return prop.Damage(caster,strike.RawSceneryDamage(caster,skill.RankContext));
            }
            if (effect is ScaledDamageAction scaled)
                for (int i = 0; i < scaled.Hits; i++) yield return prop.Damage(caster, Mathf.RoundToInt(scaled.RawSceneryDamage(caster, skill.RankContext) * multiplier));
        }
    }
}
