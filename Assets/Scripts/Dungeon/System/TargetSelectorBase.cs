using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class TargetSelector
{
    public TargetTeam Team;
    public TargetArea Area;

    public List<Vector3Int> GetTargets(Character caster)
    {
        IEnumerable<Character> candidates = Game.Instance.AllCharacters;

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
            TargetArea.Visible => Game.Instance.CurrentDungeon.GetVisionBounds(caster, caster.TilemapPosition),
            TargetArea.Melee => caster.GetAttackBounds(),
            TargetArea.Self => new BoundsInt(caster.TilemapPosition, Vector3Int.one),
            _ => null // Custom can be handled separately
        };

        if (bounds.HasValue)
            candidates = candidates.Where(x => bounds.Value.Contains(x.TilemapPosition));

        return candidates
            .Select(x => x.TilemapPosition)
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