using EternalEnigma.Core.Terminal;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Terminal;

public class TerminalTests
{
    [Fact]
    public void FrameClipsColorsEscapesAndCaches()
    {
        var frame = new TerminalFrame();
        frame.Begin(5, 2);
        frame.Box(0, 0, 5, 2, TerminalPalette.White);
        Assert.True(frame.Commit());
        Assert.Equal("+---+\n+---+", frame.PlainText);
        frame.Begin(5, 2); frame.Box(0, 0, 5, 2, TerminalPalette.White);
        Assert.False(frame.Commit());
        frame.Begin(5, 2); frame.Write(0, 0, "</no", TerminalPalette.Enemy); frame.Write(0, 1, "<&>\u0001", TerminalPalette.White);
        Assert.True(frame.Commit());
        Assert.Equal("</no \n<&>? ", frame.PlainText);
        Assert.Contains("<noparse><</noparse>", frame.RichText);
        Assert.Contains("<noparse><</noparse>&>", frame.RichText);
        frame.Begin(5, 2); Assert.True(frame.Commit());
        Assert.Equal("     \n     ", frame.PlainText);
    }

    [Fact]
    public void ViewportCentersClampsAndPads()
    {
        var view = new TerminalViewport(3, 2, 5, 4, new GridPoint(2, 1));
        Assert.Equal(new GridPoint(0, 3), view.At(0, 0));
        Assert.Equal(new GridPoint(2, 0), view.At(2, 3));
        var clamped = new TerminalViewport(20, 20, 5, 5, new GridPoint(19, 19));
        Assert.Equal(new GridPoint(15, 19), clamped.At(0, 0));
    }

    [Fact]
    public void DungeonFogHidesActorsAndTargetFocuses()
    {
        var fog = new TerminalVisibility[4, 3];
        fog[0, 0] = TerminalVisibility.Visible;
        fog[1, 0] = TerminalVisibility.Explored;
        var snapshot = new DungeonTerminalSnapshot(4, 3, new GridPoint(0, 0), "Floor", _ => new TerminalCell('.', TerminalPalette.White), fog,
            new[] { new TerminalActor("hero", new GridPoint(0, 0), TerminalActorKind.Player), new TerminalActor("hidden", new GridPoint(2, 0), TerminalActorKind.Enemy) });
        var renderer = new DungeonTerminalRenderer();
        Assert.True(renderer.Render(snapshot, 8, 6));
        Assert.Contains('@', renderer.Frame.PlainText);
        Assert.DoesNotContain('e', renderer.Frame.PlainText.Replace("Floor", string.Empty));
        Assert.Equal(TerminalPalette.Dim, renderer.Frame[3, 3].Color);
        Assert.False(renderer.Render(snapshot, 8, 6));
    }

    [Fact]
    public void MenuRetainsIdsAndNestedFocus()
    {
        var menu = new TerminalMenuModel("root", Enumerable.Range(0, 5).Select(i => new TerminalMenuOption($"id{i}", $"Item {i}")), 2);
        menu.Move(1); menu.Move(1);
        Assert.Equal("id2", menu.SelectedId);
        Assert.Equal(1, menu.Page);
        menu.Push("child", new[] { new TerminalMenuOption("off", "Off", false, "Locked") });
        Assert.Null(menu.Confirm());
        Assert.True(menu.Cancel());
        Assert.Equal("id2", menu.SelectedId);
        menu.Replace("root", new[] { new TerminalMenuOption("id2", "Retained"), new TerminalMenuOption("id5", "New") });
        Assert.Equal("id2", menu.Confirm());
        Assert.Equal("id2", menu.Confirm(101));
        Assert.Null(menu.Confirm(101));
        Assert.Equal("id2", menu.Confirm(102));
        menu.Replace("empty", Array.Empty<TerminalMenuOption>());
        Assert.Null(menu.Confirm());
    }

    [Fact]
    public void TownAndOverworldUseLiveActorsAndChangingFeatures()
    {
        var gateOpen = false;
        TerminalCell Terrain(GridPoint p) => new(p.X == 1 ? gateOpen ? '/' : '+' : '.', TerminalPalette.White);
        var town = new TownTerminalRenderer();
        var withVendor = new TownTerminalSnapshot(3, 2, new GridPoint(0, 0), "Town", Terrain,
            new[] { new TerminalActor("vendor", new GridPoint(1, 0), TerminalActorKind.Npc, 'v'),
                new TerminalActor("hero", new GridPoint(0, 0), TerminalActorKind.Player) });
        Assert.True(town.Render(withVendor, 9, 6));
        Assert.Contains('v', town.Frame.PlainText);
        var withoutVendor = new TownTerminalSnapshot(3, 2, new GridPoint(0, 0), "Town", Terrain,
            new[] { new TerminalActor("hero", new GridPoint(0, 0), TerminalActorKind.Player) });
        Assert.True(town.Render(withoutVendor, 9, 6));
        Assert.DoesNotContain('v', town.Frame.PlainText);

        var overworld = new OverworldTerminalRenderer();
        var world = new OverworldTerminalSnapshot(3, 2, new GridPoint(0, 0), "World", Terrain);
        Assert.True(overworld.Render(world, 9, 6));
        Assert.Contains('+', overworld.Frame.PlainText);
        gateOpen = true;
        Assert.True(overworld.Render(world, 9, 6));
        Assert.Contains('/', overworld.Frame.PlainText);
        Assert.False(overworld.Render(world, 9, 6));
    }
    [Fact]
    public void GlyphRulesHaveStableFallbacks()
    {
        Assert.Equal('g', TerminalGlyphs.Enemy("Goblin"));
        Assert.Equal('e', TerminalGlyphs.Enemy("123"));
        Assert.Equal('b', TerminalGlyphs.TownService("Bakery"));
        Assert.Equal('R', TerminalGlyphs.TownService("Trainer"));
        Assert.Equal('*', TerminalGlyphs.TownService("Unknown"));
    }
}
