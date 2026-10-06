using System.Collections.Generic;
using System.Linq;

// Shared targeting rules for learned skills and usable item definitions.
internal readonly struct ActionTargeting
{
    internal readonly SkillTargeting Mode;
    internal readonly TargetSelector Characters;
    internal readonly InventoryTargetSelector Items;
    internal readonly int Radius;
    internal readonly int MissileRange;

    internal ActionTargeting(SkillTargeting mode, TargetSelector characters, InventoryTargetSelector items, int radius, int missileRange)
    {
        Mode = mode; Characters = characters; Items = items; Radius = radius; MissileRange = missileRange;
    }

    internal bool IsConfigured => Mode == SkillTargeting.InventoryItem ? Items != null :
        Characters != null && Radius >= 0 && (Mode != SkillTargeting.Missile || MissileRange > 0);
    internal bool RequiresSelection => Mode == SkillTargeting.Tile || Mode == SkillTargeting.SelectedTarget &&
        Characters.Team != TargetTeam.Self && Characters.Area != TargetArea.Self;
    internal List<Character> GetCharacters(Character caster) =>
        Mode == SkillTargeting.InventoryItem ? new() : Characters.GetCharacters(caster);
    internal List<Character> GetAffected(Character caster, Character selected)
    {
        var candidates = GetCharacters(caster);
        if (Mode == SkillTargeting.AllTargets) return candidates;
        var center = RequiresSelection ? selected : caster;
        if (center == null || (RequiresSelection && !candidates.Contains(center))) return new();
        int radius = Radius;
        return candidates.Where(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, center.TilemapPosition) <= radius).ToList();
    }
    internal List<Character> GetMissileAffected(Character caster, MissileTargeting.Hit hit)
    {
        int radius = Radius;
        var selector = Characters;
        if (radius > 0) return Game.Instance.AllCharacters.Where(c => selector.Eligible(caster, c) && TileWorldDungeon.ChevDistance(c.TilemapPosition, hit.Cell) <= radius).ToList();
        return hit.Character != null && Characters.Eligible(caster, hit.Character) ? new List<Character> { hit.Character } : new List<Character>();
    }
}
