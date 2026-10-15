using EternalEnigma.Core.Terminal;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Terminal;

public class TownOverworldTerminalTests
{
    private static TerminalCell Ground(GridPoint _) => new('.', TerminalPalette.Dim);

    [Fact]
    public void ContextAndLegendAreImmutableAndServiceIdsHaveSemanticFallbacks()
    {
        string[] context = { "Facing north" }, legend = { "@ you" };
        var snapshot = new TownTerminalSnapshot(20, 20, new GridPoint(10, 10), "Town", Ground,
            context: context, legend: legend);
        context[0] = "Changed"; legend[0] = "Changed";
        Assert.Equal("Facing north", Assert.Single(snapshot.Context));
        Assert.Equal("@ you", Assert.Single(snapshot.Legend));
        Assert.Equal('h', TerminalGlyphs.TownService("town-0-residence", "home"));
        Assert.Equal('I', TerminalGlyphs.TownService("resthouse", "inn"));
        Assert.Equal('R', TerminalGlyphs.TownService("ballista", "trainer"));
        Assert.Equal('b', TerminalGlyphs.TownService("bakery", "shop"));
        Assert.Equal('*', TerminalGlyphs.TownService("unknown", "unknown"));
    }

    [Theory]
    [InlineData(36, 20)]
    [InlineData(72, 24)]
    [InlineData(128, 38)]
    public void ContextLegendAndLongLogStayInsideFrame(int columns, int rows)
    {
        var snapshot = new TownTerminalSnapshot(100, 100, new GridPoint(50, 50), new string('T', 160), Ground,
            messages: new[] { new string('L', 400) + "\nLatest event completed." },
            context: new[] { "Hero level 3", "Facing north", "Talk/use: Enter" },
            legend: new[] { "@ you", "a party", "v vendor", "I inn", "R trainer", "h home", "X exit" });
        var renderer = new TownTerminalRenderer();
        renderer.Render(snapshot, columns, rows);
        string[] lines = renderer.Frame.PlainText.Split('\n');
        Assert.Equal(rows, lines.Length);
        Assert.All(lines, line => Assert.Equal(columns, line.Length));
        foreach (string line in lines.Skip(1).Take(rows - 2))
        {
            Assert.Equal('|', line[0]);
            Assert.Equal('|', line[^1]);
        }
        Assert.Contains("Latest event completed.", renderer.Frame.PlainText);
        Assert.Contains("...", renderer.Frame.PlainText);
        Assert.Contains("Talk/use: Enter", renderer.Frame.PlainText);
        Assert.Contains("X exit", renderer.Frame.PlainText);
        Assert.True(renderer.TryFrameCell(snapshot.Focus, out int x, out int y));
        Assert.Equal('.', renderer.Frame[x, y].Glyph);
    }

    [Fact]
    public void NarrativePunctuationRemainsReadableInAscii()
    {
        var renderer = new TownTerminalRenderer();
        renderer.Render(new TownTerminalSnapshot(20, 20, new GridPoint(10, 10), "Town \u2014 home", Ground,
            messages: new[] { "\u201cWelcome\u201d \u2014 traveller\u2019s rest\u2026" }), 50, 15);
        Assert.Contains("Town - home", renderer.Frame.PlainText);
        Assert.Contains("\"Welcome\" - traveller's rest...", renderer.Frame.PlainText);
        Assert.DoesNotContain('?', renderer.Frame.PlainText);
    }

    [Fact]
    public void MapUsesFullWidthWhenThereIsNoSidebarContent()
    {
        var renderer = new OverworldTerminalRenderer();
        renderer.Render(new OverworldTerminalSnapshot(100, 100, new GridPoint(50, 50), "World", Ground), 90, 24);
        Assert.True(renderer.TryFrameCell(new GridPoint(90, 50), out _, out _));
        renderer.Render(new OverworldTerminalSnapshot(100, 100, new GridPoint(50, 50), "World", Ground,
            legend: new[] { "@ you" }), 90, 24);
        Assert.False(renderer.TryFrameCell(new GridPoint(90, 50), out _, out _));
    }

    [Fact]
    public void MappingRejectsBorderPaddingSidebarAndStatusAndRefreshesAfterResize()
    {
        var renderer = new TownTerminalRenderer();
        Assert.False(renderer.TryMapCell(0, 0, out _));
        renderer.Render(new TownTerminalSnapshot(5, 4, new GridPoint(2, 2), "Town", Ground,
            context: new[] { "Facing north" }, legend: new[] { "@ you" }), 80, 24);
        Assert.False(renderer.TryMapCell(0, 0, out _));
        Assert.False(renderer.TryMapCell(1, 1, out _)); // unused space around a small map
        Assert.False(renderer.TryMapCell(70, 3, out _)); // sidebar
        Assert.False(renderer.TryFrameCell(new GridPoint(5, 0), out _, out _));
        for (int y = 0; y < 4; y++) for (int x = 0; x < 5; x++)
        {
            var cell = new GridPoint(x, y);
            Assert.True(renderer.TryFrameCell(cell, out int column, out int row));
            Assert.True(renderer.TryMapCell(column, row, out var mapped));
            Assert.Equal(cell, mapped);
        }
        renderer.Render(new TownTerminalSnapshot(100, 100, new GridPoint(90, 90), "Town", Ground,
            context: new[] { "Facing north" }, legend: new[] { "@ you" }), 36, 20);
        Assert.False(renderer.TryMapCell(1, 17, out _)); // status lines
        Assert.False(renderer.TryFrameCell(new GridPoint(2, 2), out _, out _));
        Assert.True(renderer.TryFrameCell(new GridPoint(90, 90), out int resizedX, out int resizedY));
        Assert.True(renderer.TryMapCell(resizedX, resizedY, out var resized));
        Assert.Equal(new GridPoint(90, 90), resized);
    }

    [Theory]
    [InlineData(36, 20)]
    [InlineData(80, 24)]
    public void TargetInstructionsRemainVisibleAndPointerFocusCanStayFixed(int columns, int rows)
    {
        var fog = TerminalKnowledge.Capture(100, 100, _ => TerminalVisibility.Visible);
        var focus = new GridPoint(30, 30);
        var target = new TerminalTargetSnapshot(new GridPoint(80, 80),
            "Extremely long ability name for selecting a target\nGoblin captain\nAiming northeast, up to 12 tiles away\nAffects an area within 3 tiles\nA / Cross: confirm\nB / Circle: cancel");
        var snapshot = new DungeonTerminalSnapshot(100, 100, focus, "Dungeon", Ground, fog, target: target);
        var renderer = new DungeonTerminalRenderer();
        renderer.Render(snapshot, columns, rows, focus);
        Assert.Contains("A / Cross: confirm", renderer.Frame.PlainText);
        Assert.Contains("B / Circle: cancel", renderer.Frame.PlainText);
        Assert.True(renderer.TryFrameCell(focus, out _, out _));
        Assert.False(renderer.TryFrameCell(target.Cell!.Value, out _, out _));
        renderer.Render(snapshot, columns, rows);
        Assert.True(renderer.TryFrameCell(target.Cell.Value, out _, out _));
        Assert.False(renderer.TryFrameCell(focus, out _, out _));
    }
}
