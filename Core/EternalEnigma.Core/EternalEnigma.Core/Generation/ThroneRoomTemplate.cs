using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

internal static class ThroneRoomTemplate
{
    public const int Size = 12;
    public static readonly GridPoint Start = new(6, 4);
    public static readonly GridPoint Stairs = new(6, 9);
    public static readonly GridRect Room = new(1, 1, 10, 10);

    private static readonly string[] Art = new[]
    {
        "############",   // y=11
        "#....CC....#",   // y=10
        "t....C>C...t",   // y=9   '>' = stairs at (6,9)
        "#.I.CCCC.I.#",   // y=8
        "#....CC....#",   // y=7
        "#....CC....#",   // y=6
        "#.I..CC..I.#",   // y=5
        "t....CS....t",   // y=4   'S' = start at (6,4)
        "#....CC....#",   // y=3
        "#..........#",   // y=2
        "#..........#",   // y=1
        "############",   // y=0
    };

    static ThroneRoomTemplate()
    {
        // Validate art: 12 rows of 12 chars each
        if (Art.Length != Size)
            throw new InvalidOperationException($"Art must have {Size} rows, got {Art.Length}");

        foreach (var row in Art)
        {
            if (row.Length != Size)
                throw new InvalidOperationException($"Each art row must have {Size} characters, got {row.Length}");
        }

        // Validate Start marker is at Start position
        int startX = Start.X;
        int startY = Start.Y;
        // Art[0] is y=11, Art[11] is y=0, so Art[11-startY] is y=startY
        int artRow = Size - 1 - startY;
        if (Art[artRow][startX] != 'S')
            throw new InvalidOperationException($"Start marker 'S' must be at ({startX},{startY})");

        // Validate Stairs marker is at Stairs position
        int stairsX = Stairs.X;
        int stairsY = Stairs.Y;
        int stairsArtRow = Size - 1 - stairsY;
        if (Art[stairsArtRow][stairsX] != '>')
            throw new InvalidOperationException($"Stairs marker '>' must be at ({stairsX},{stairsY})");
    }

    /// Builds Floor, Dungeon, Carpet, Columns, Torchlights, Start, Stairs layers (12x12) from the fixed art.
    public static Dictionary<string, GridLayer> Layers()
    {
        var floor = new bool[Size, Size];
        var carpet = new bool[Size, Size];
        var columns = new bool[Size, Size];
        var torchlights = new bool[Size, Size];
        var start = new bool[Size, Size];
        var stairs = new bool[Size, Size];

        // Parse art from top to bottom
        for (int artRow = 0; artRow < Art.Length; artRow++)
        {
            int y = Size - 1 - artRow; // Convert art row index to y coordinate
            string row = Art[artRow];

            for (int x = 0; x < row.Length; x++)
            {
                char ch = row[x];

                switch (ch)
                {
                    case '#':
                        // Wall: Dungeon only
                        floor[x, y] = false;
                        break;

                    case 't':
                        // Wall + Torchlights: Dungeon + Torchlights
                        floor[x, y] = false;
                        torchlights[x, y] = true;
                        break;

                    case 'I':
                        // Wall + Columns: Dungeon + Columns
                        floor[x, y] = false;
                        columns[x, y] = true;
                        break;

                    case '.':
                        // Floor only
                        floor[x, y] = true;
                        break;

                    case 'C':
                        // Floor + Carpet
                        floor[x, y] = true;
                        carpet[x, y] = true;
                        break;

                    case 'S':
                        // Floor + Carpet + Start marker
                        floor[x, y] = true;
                        carpet[x, y] = true;
                        start[x, y] = true;
                        break;

                    case '>':
                        // Floor + Carpet + Stairs marker
                        floor[x, y] = true;
                        carpet[x, y] = true;
                        stairs[x, y] = true;
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown character '{ch}' in art at ({x},{y})");
                }
            }
        }

        // Build Dungeon layer as inverse of Floor
        var dungeon = new bool[Size, Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                dungeon[x, y] = !floor[x, y];
            }
        }

        return new Dictionary<string, GridLayer>(StringComparer.Ordinal)
        {
            { DungeonLayers.Floor, new GridLayer(floor) },
            { DungeonLayers.Dungeon, new GridLayer(dungeon) },
            { DungeonLayers.Carpet, new GridLayer(carpet) },
            { DungeonLayers.Columns, new GridLayer(columns) },
            { DungeonLayers.Torchlights, new GridLayer(torchlights) },
            { DungeonLayers.Start, new GridLayer(start) },
            { DungeonLayers.Stairs, new GridLayer(stairs) },
        };
    }
}
