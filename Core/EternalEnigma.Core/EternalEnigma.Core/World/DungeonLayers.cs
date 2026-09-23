namespace EternalEnigma.Core.World;

public static class DungeonLayers
{
    public const string Floor = "Floor";             // walkable cells (the only layer Unity gameplay reads)
    public const string Dungeon = "Dungeon";         // wall mass == !Floor
    public const string Carpet = "Carpet";           // subset of Floor
    public const string Columns = "Columns";         // subset of Dungeon
    public const string Torchlights = "Torchlights"; // subset of Dungeon
    public const string Start = "Start";             // single-cell marker
    public const string Stairs = "Stairs";           // single-cell marker
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { Floor, Dungeon, Carpet, Columns, Torchlights, Start, Stairs });
}
