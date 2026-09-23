namespace EternalEnigma.Core.World;

public enum DiagonalRule { RequireOpenSides, AllowCornerCutting }

public static class GridSteps
{
    /// <summary>Fixed order matching Unity GridMovement.Directions: DownLeft, Down, DownRight, Left, Right, UpLeft, Up, UpRight</summary>
    public static readonly GridPoint[] EightWay = { new(-1,-1), new(0,-1), new(1,-1), new(-1,0), new(1,0), new(-1,1), new(0,1), new(1,1) };

    /// <summary>Order matching Unity CampaignTownCorridor.CompleteBuildingPositions: Up, Left, Right, Down</summary>
    public static readonly GridPoint[] Cardinal = { new(0,1), new(-1,0), new(1,0), new(0,-1) };

    /// <summary>
    /// Determines if a step from one grid point to another is valid.
    /// For RequireOpenSides: delegates to OverworldMovement.CanStep.
    /// For AllowCornerCutting: checks that both from and to are passable (note: Unity's checks only to).
    /// </summary>
    public static bool CanStep(GridPoint from, GridPoint to, Func<GridPoint, bool> passable, DiagonalRule rule)
    {
        if (rule == DiagonalRule.RequireOpenSides)
            return OverworldMovement.CanStep(from, to, passable);

        int dx = to.X - from.X, dy = to.Y - from.Y;
        return Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && !(dx == 0 && dy == 0) && passable(from) && passable(to);
    }

    /// <summary>Returns EightWay offsets as neighbors, filtered by CanStep, in EightWay order</summary>
    public static IEnumerable<GridPoint> Neighbors(GridPoint from, Func<GridPoint, bool> passable, DiagonalRule rule)
    {
        foreach (var offset in EightWay)
        {
            var to = new GridPoint(from.X + offset.X, from.Y + offset.Y);
            if (CanStep(from, to, passable, rule))
                yield return to;
        }
    }

    /// <summary>Returns Cardinal offsets as neighbors where passable(next), in Cardinal order</summary>
    public static IEnumerable<GridPoint> CardinalNeighbors(GridPoint from, Func<GridPoint, bool> passable)
    {
        foreach (var offset in Cardinal)
        {
            var to = new GridPoint(from.X + offset.X, from.Y + offset.Y);
            if (passable(to))
                yield return to;
        }
    }
}
