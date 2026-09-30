namespace EternalEnigma.Core.World;

public sealed class ShopRoom
{
    public GridPoint Door { get; }
    public IReadOnlyList<GridPoint> Floor { get; }
    public IReadOnlyList<GridPoint> Wall { get; }
    /// <summary>The floor cell where the vendor stands; three cells inside the door unless the room says otherwise.</summary>
    public GridPoint VendorAnchor { get; }

    public ShopRoom(GridPoint door, IEnumerable<GridPoint> floor, IEnumerable<GridPoint> wall, GridPoint? vendorAnchor = null)
    {
        if (floor == null)
            throw new ArgumentNullException(nameof(floor));
        if (wall == null)
            throw new ArgumentNullException(nameof(wall));

        Door = door;
        Floor = floor.ToList().AsReadOnly();
        Wall = wall.ToList().AsReadOnly();
        VendorAnchor = vendorAnchor ?? new GridPoint(door.X, door.Y + 3);
    }
}
