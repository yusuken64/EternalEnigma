namespace EternalEnigma.Core.World;

public sealed class ShopRoom
{
    public GridPoint Door { get; }
    public IReadOnlyList<GridPoint> Floor { get; }
    public IReadOnlyList<GridPoint> Wall { get; }
    public GridPoint VendorAnchor => new GridPoint(Door.X, Door.Y + 3);

    public ShopRoom(GridPoint door, IEnumerable<GridPoint> floor, IEnumerable<GridPoint> wall)
    {
        if (floor == null)
            throw new ArgumentNullException(nameof(floor));
        if (wall == null)
            throw new ArgumentNullException(nameof(wall));

        Door = door;
        Floor = floor.ToList().AsReadOnly();
        Wall = wall.ToList().AsReadOnly();
    }
}
