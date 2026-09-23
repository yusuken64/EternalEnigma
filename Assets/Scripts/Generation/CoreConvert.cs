using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

public static class CoreConvert
{
    public static Vector3Int ToCell(this GridPoint p) => new Vector3Int(p.X, p.Y, 0);
    public static GridPoint ToGridPoint(this Vector3Int c) => new GridPoint(c.x, c.y);
    public static List<Vector3Int> ToCells(this IEnumerable<GridPoint> points) => points.Select(ToCell).ToList();
    public static HashSet<Vector3Int> ToCellSet(this IEnumerable<GridPoint> points) => new HashSet<Vector3Int>(points.Select(ToCell));
}
