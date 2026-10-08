using System.Text;

namespace EternalEnigma.Core.Terminal;

public readonly struct TerminalColor : IEquatable<TerminalColor>
{
    public readonly byte R, G, B;
    public TerminalColor(byte r, byte g, byte b) { R = r; G = g; B = b; }
    public bool Equals(TerminalColor other) => R == other.R && G == other.G && B == other.B;
    public override bool Equals(object? obj) => obj is TerminalColor other && Equals(other);
    public override int GetHashCode() => (R << 16) | (G << 8) | B;
}

public readonly struct TerminalCell : IEquatable<TerminalCell>
{
    public readonly char Glyph;
    public readonly TerminalColor Color;
    public TerminalCell(char glyph, TerminalColor color) { Glyph = Normalize(glyph); Color = color; }
    public bool Equals(TerminalCell other) => Glyph == other.Glyph && Color.Equals(other.Color);
    public override bool Equals(object? obj) => obj is TerminalCell other && Equals(other);
    public override int GetHashCode() => (Glyph << 24) ^ Color.GetHashCode();
    public static char Normalize(char glyph) => glyph >= 32 && glyph <= 126 ? glyph : '?';
}

public static class TerminalPalette
{
    public static readonly TerminalColor White = new(232, 232, 232);
    public static readonly TerminalColor Dim = new(95, 100, 108);
    public static readonly TerminalColor Wall = new(140, 150, 165);
    public static readonly TerminalColor Player = new(255, 224, 112);
    public static readonly TerminalColor Ally = new(105, 210, 230);
    public static readonly TerminalColor Enemy = new(255, 100, 105);
    public static readonly TerminalColor Item = new(125, 220, 135);
    public static readonly TerminalColor Feature = new(220, 165, 245);
    public static readonly TerminalColor Selected = new(255, 255, 70);
}

/// <summary>Reusable fixed-cell surface. Commit compares the completed frame, including colors.</summary>
public sealed class TerminalFrame
{
    private TerminalCell[] front = Array.Empty<TerminalCell>();
    private TerminalCell[] back = Array.Empty<TerminalCell>();
    private readonly StringBuilder builder = new();
    private string richText = string.Empty;
    private string plainText = string.Empty;
    private bool hasFrame;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string RichText => richText;
    public string PlainText => plainText;
    public TerminalCell this[int x, int y] => front[y * Width + x];

    public void Begin(int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 1_000_000) throw new ArgumentOutOfRangeException(nameof(width));
        if (Width != width || Height != height)
        {
            Width = width; Height = height;
            front = new TerminalCell[width * height];
            back = new TerminalCell[width * height];
            hasFrame = false;
        }
        Array.Fill(back, new TerminalCell(' ', TerminalPalette.White));
    }

    public void Put(int x, int y, char glyph, TerminalColor color)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height) back[y * Width + x] = new TerminalCell(glyph, color);
    }

    public void Write(int x, int y, string? value, TerminalColor color)
    {
        if (value == null || y < 0 || y >= Height) return;
        for (int i = 0; i < value.Length; i++) Put(x + i, y, value[i], color);
    }

    public void Fill(int x, int y, int width, int height, char glyph, TerminalColor color)
    {
        for (int row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
            for (int col = Math.Max(0, x); col < Math.Min(Width, x + width); col++) Put(col, row, glyph, color);
    }

    public void Box(int x, int y, int width, int height, TerminalColor color)
    {
        if (width < 2 || height < 2) return;
        for (int col = x + 1; col < x + width - 1; col++) { Put(col, y, '-', color); Put(col, y + height - 1, '-', color); }
        for (int row = y + 1; row < y + height - 1; row++) { Put(x, row, '|', color); Put(x + width - 1, row, '|', color); }
        Put(x, y, '+', color); Put(x + width - 1, y, '+', color);
        Put(x, y + height - 1, '+', color); Put(x + width - 1, y + height - 1, '+', color);
    }

    public bool Commit()
    {
        if (hasFrame && front.AsSpan().SequenceEqual(back)) return false;
        (front, back) = (back, front);
        hasFrame = true;
        builder.Clear();
        for (int y = 0; y < Height; y++)
        {
            if (y != 0) builder.Append('\n');
            for (int x = 0; x < Width; x++) builder.Append(front[y * Width + x].Glyph);
        }
        plainText = builder.ToString();
        builder.Clear();
        for (int y = 0; y < Height; y++)
        {
            if (y != 0) builder.Append('\n');
            bool opened = false;
            TerminalColor previous = default;
            for (int x = 0; x < Width; x++)
            {
                var cell = front[y * Width + x];
                if (!opened || !previous.Equals(cell.Color))
                {
                    if (opened) builder.Append("</color>");
                    builder.Append("<color=#").Append(cell.Color.R.ToString("x2"))
                        .Append(cell.Color.G.ToString("x2")).Append(cell.Color.B.ToString("x2")).Append('>');
                    previous = cell.Color; opened = true;
                }
                // Split every literal '<' into its own noparse region. Even a user-supplied
                // </noparse> can never terminate a region containing later user text.
                if (cell.Glyph == '<') builder.Append("<noparse><</noparse>");
                else builder.Append(cell.Glyph);
            }
            if (opened) builder.Append("</color>");
        }
        richText = builder.ToString();
        return true;
    }

    public string ToRichText() => RichText;
}
