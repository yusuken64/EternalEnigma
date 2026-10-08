using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Terminal;

public readonly struct TerminalViewport
{
    public int Left { get; }
    public int Bottom { get; }
    public int Width { get; }
    public int Height { get; }
    public TerminalViewport(int mapWidth, int mapHeight, int width, int height, GridPoint focus)
    {
        Width = Math.Max(1, width); Height = Math.Max(1, height);
        Left = Math.Clamp(focus.X - Width / 2, 0, Math.Max(0, mapWidth - Width));
        Bottom = Math.Clamp(focus.Y - Height / 2, 0, Math.Max(0, mapHeight - Height));
    }
    public GridPoint At(int column, int row) => new(Left + column, Bottom + Height - 1 - row);
}
