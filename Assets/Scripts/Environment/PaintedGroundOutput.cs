using UnityEngine;

// The single TWC owner of the broad surfaces; also retained when terrain is cached.
public sealed class PaintedGroundOutput : MonoBehaviour
{
    public int Seed, Width, Height, OriginX, OriginY;
    public int[] SurfaceCells = new int[11];
    public int CellCount;
}
