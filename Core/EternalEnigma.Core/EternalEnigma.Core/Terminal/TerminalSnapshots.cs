using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Terminal;

public enum TerminalVisibility : byte { Unseen, Explored, Visible }
public enum TerminalActorKind : byte { Player, Ally, Summon, Downed, Enemy, Item, Prop, Npc }

public readonly struct TerminalActor
{
    public string Id { get; }
    public GridPoint Cell { get; }
    public TerminalActorKind Kind { get; }
    public char Glyph { get; }
    public bool VisibleInFog { get; }
    public TerminalActor(string id, GridPoint cell, TerminalActorKind kind, char glyph = '\0', bool visibleInFog = false)
    { Id = id ?? string.Empty; Cell = cell; Kind = kind; Glyph = glyph == '\0' ? DefaultGlyph(kind) : TerminalCell.Normalize(glyph); VisibleInFog = visibleInFog; }
    private static char DefaultGlyph(TerminalActorKind kind) => kind switch
    {
        TerminalActorKind.Player => '@', TerminalActorKind.Ally => 'a', TerminalActorKind.Summon => 's',
        TerminalActorKind.Downed => 'x', TerminalActorKind.Enemy => 'e', TerminalActorKind.Item => 'i',
        TerminalActorKind.Prop => 'p', _ => 'n'
    };
}

public readonly struct TerminalPartyStat
{
    public string Name { get; }
    public int Level { get; }
    public int Hp { get; }
    public int MaxHp { get; }
    public int Sp { get; }
    public int MaxSp { get; }
    public TerminalPartyStat(string name, int level, int hp, int maxHp, int sp, int maxSp)
    { Name = name ?? string.Empty; Level = level; Hp = hp; MaxHp = maxHp; Sp = sp; MaxSp = maxSp; }
}

public sealed class TerminalTargetSnapshot
{
    public GridPoint? Cell { get; }
    public string Label { get; }
    public IReadOnlyList<GridPoint> Area { get; }
    public TerminalTargetSnapshot(GridPoint? cell, string? label, IEnumerable<GridPoint>? area = null)
    { Cell = cell; Label = label ?? string.Empty; Area = Array.AsReadOnly((area ?? Array.Empty<GridPoint>()).ToArray()); }
}

/// <summary>Immutable frame facts. The static terrain callback may close over an immutable Core world description.</summary>
public abstract class TerminalMapSnapshot
{
    private readonly Func<GridPoint, TerminalCell> terrain;
    private readonly TerminalVisibility[,] visibility;
    public int Width { get; }
    public int Height { get; }
    public GridPoint Focus { get; }
    public string Title { get; }
    public IReadOnlyList<TerminalActor> Actors { get; }
    public IReadOnlyList<TerminalPartyStat> Party { get; }
    public IReadOnlyList<string> Messages { get; }
    public TerminalTargetSnapshot? Target { get; }

    protected TerminalMapSnapshot(int width, int height, GridPoint focus, string? title,
        Func<GridPoint, TerminalCell> terrain, TerminalVisibility[,]? visibility,
        IEnumerable<TerminalActor>? actors, IEnumerable<TerminalPartyStat>? party,
        IEnumerable<string>? messages, TerminalTargetSnapshot? target)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height; Focus = focus; Title = title ?? string.Empty;
        this.terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        if (visibility != null && (visibility.GetLength(0) != width || visibility.GetLength(1) != height))
            throw new ArgumentException("Visibility dimensions must match the map.", nameof(visibility));
        this.visibility = visibility == null ? AllVisible(width, height) : (TerminalVisibility[,])visibility.Clone();
        Actors = Array.AsReadOnly((actors ?? Array.Empty<TerminalActor>()).ToArray());
        Party = Array.AsReadOnly((party ?? Array.Empty<TerminalPartyStat>()).ToArray());
        Messages = Array.AsReadOnly((messages ?? Array.Empty<string>()).Select(s => s ?? string.Empty).ToArray());
        Target = target;
    }
    private static TerminalVisibility[,] AllVisible(int width, int height)
    {
        var cells = new TerminalVisibility[width, height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) cells[x, y] = TerminalVisibility.Visible;
        return cells;
    }
    public TerminalCell TerrainAt(GridPoint p) => terrain(p);
    public TerminalVisibility VisibilityAt(GridPoint p) => visibility[p.X, p.Y];
}

public sealed class DungeonTerminalSnapshot : TerminalMapSnapshot
{
    public DungeonTerminalSnapshot(int width, int height, GridPoint focus, string? title, Func<GridPoint, TerminalCell> terrain,
        TerminalVisibility[,] visibility, IEnumerable<TerminalActor>? actors = null, IEnumerable<TerminalPartyStat>? party = null,
        IEnumerable<string>? messages = null, TerminalTargetSnapshot? target = null)
        : base(width, height, focus, title, terrain, visibility, actors, party, messages, target) { }
}

public sealed class OverworldTerminalSnapshot : TerminalMapSnapshot
{
    public OverworldTerminalSnapshot(int width, int height, GridPoint focus, string? title, Func<GridPoint, TerminalCell> terrain,
        IEnumerable<TerminalActor>? actors = null, IEnumerable<TerminalPartyStat>? party = null, IEnumerable<string>? messages = null,
        TerminalTargetSnapshot? target = null)
        : base(width, height, focus, title, terrain, null, actors, party, messages, target) { }
}

public sealed class TownTerminalSnapshot : TerminalMapSnapshot
{
    public TownTerminalSnapshot(int width, int height, GridPoint focus, string? title, Func<GridPoint, TerminalCell> terrain,
        IEnumerable<TerminalActor>? actors = null, IEnumerable<TerminalPartyStat>? party = null, IEnumerable<string>? messages = null,
        TerminalTargetSnapshot? target = null)
        : base(width, height, focus, title, terrain, null, actors, party, messages, target) { }
}
