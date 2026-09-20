using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class TargetSelector
{
    public TargetTeam Team;
    public TargetArea Area;

    public List<Vector3Int> GetTargets(Character caster)
        => GetCharacters(caster).Select(x => x.TilemapPosition).Distinct().ToList();

    public List<Character> GetCharacters(Character caster)
    {
        IEnumerable<Character> candidates = Game.Instance.AllCharacters.Where(x => x != null && x.Vitals.HP > 0);

        // --- Team filtering ---
        candidates = Team switch
        {
            TargetTeam.All => candidates,
            TargetTeam.Enemies => candidates.Where(x => x.Team != caster.Team),
            TargetTeam.Allies => candidates.Where(x => x.Team == caster.Team),
            TargetTeam.Self => candidates.Where(x => x == caster),
            _ => candidates
        };

        // --- Area filtering ---
        BoundsInt? bounds = Area switch
        {
            TargetArea.All => null,
            TargetArea.Visible => null,
            TargetArea.Melee => caster.GetAttackBounds(),
            TargetArea.Self => new BoundsInt(caster.TilemapPosition, Vector3Int.one),
            _ => null // Custom can be handled separately
        };

        if (Area == TargetArea.Visible)
            candidates = candidates.Where(x => Game.Instance.CurrentDungeon.CanSee(caster, x));

        if (bounds.HasValue)
            candidates = candidates.Where(x => x.OverlapsWith(bounds.Value));

        if (Area == TargetArea.Melee)
            candidates = candidates.Where(x => Game.Instance.CurrentDungeon.CanSee(caster, x));

        // Custom has no configured geometry yet; do not silently target the entire floor.
        if (Area == TargetArea.Custom) return new();

        return candidates
            .Distinct()
            .ToList();
    }
}

public enum TargetTeam
{
    All,
    Enemies,
    Allies,
    Self
}

public enum TargetArea
{
    All,
    Visible,
    Melee,
    Custom, // room to plug in something special
    Self
}
