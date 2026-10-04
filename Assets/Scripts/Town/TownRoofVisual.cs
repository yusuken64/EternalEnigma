using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>One building's TWC roof tiles, toggled when its room is occupied.</summary>
public sealed class TownRoofVisual : MonoBehaviour
{
    public GridPoint Door { get; private set; }
    public ShopRoom Room { get; private set; }

    public void Initialize(GridPoint door, ShopRoom room)
    {
        Door = door;
        Room = room;
    }
}
