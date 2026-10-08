using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Terminal;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Progression;
using JuicyChickenGames.Menu;
using UnityEngine;

internal static class TerminalSceneSnapshots
{
    private static GridPoint Point(Vector3Int p) => new(p.x, p.y);
    private static TerminalCell Cell(char glyph, TerminalColor color) => new(glyph, color);
    private static readonly TerminalCell FloorCell = new('.', TerminalPalette.Dim);
    private static bool At(IReadOnlyDictionary<string, GridLayer> layers, string key, GridPoint p) =>
        layers.TryGetValue(key, out var layer) && layer.At(p);

    internal static DungeonTerminalSnapshot Dungeon(Game game, Minimap minimap = null, GameMessages messages = null, TargetDialog targetDialog = null)
    {
        var live = game.CurrentDungeon;
        var floor = live.Floor;
        int width = floor.Width, height = floor.Height;
        var visibility = new TerminalVisibility[width, height];
        var known = minimap != null ? minimap.dungeonMap : null;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            if (known != null && x < known.GetLength(0) && y < known.GetLength(1))
                visibility[x, y] = known[x, y].visibility == Minimap.MinimapTileVisibility.Visible ? TerminalVisibility.Visible :
                    known[x, y].visibility == Minimap.MinimapTileVisibility.Explored ? TerminalVisibility.Explored : TerminalVisibility.Unseen;
            if (game.PartyVisibleTiles.Contains(new Vector3Int(x, y, 0))) visibility[x, y] = TerminalVisibility.Visible;
        }
        var actors = new List<TerminalActor>();
        foreach (var character in game.AllCharacters)
        {
            if (character == null || character.Vitals == null || character.Vitals.HP <= 0) continue;
            var kind = character == game.PlayerController.ControlledAlly ? TerminalActorKind.Player :
                character is Ally allyKind && allyKind.IsSummon ? TerminalActorKind.Summon :
                character is Ally ? TerminalActorKind.Ally : TerminalActorKind.Enemy;
            var p = Point(character.TilemapPosition);
            if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height) continue;
            bool revealed = character is Enemy enemy && game.FloorReveal != null && game.FloorReveal.EnemiesRevealedTurns > 0;
            bool overlapsSight = character is Enemy && DungeonSight.OverlapsVisible(game.PartyVisibleTiles,
                Character.ToBounds(character.FootPrint, character.TilemapPosition));
            bool visible = character is Ally || overlapsSight || revealed;
            if (!visible) continue;
            char glyph = character is Enemy disguise && EnemyBehavior.IsDisguised(disguise) ? 'i' :
                character is Enemy ? TerminalGlyphs.Enemy(character.CharacterName) : '\0';
            actors.Add(new TerminalActor(character.GetInstanceID().ToString(), p, glyph == 'i' ? TerminalActorKind.Item : kind, glyph,
                revealed || overlapsSight));
        }
        foreach (var ally in game.DownedAllies)
            if (ally != null) actors.Add(new TerminalActor(ally.GetInstanceID().ToString(), Point(ally.TilemapPosition), TerminalActorKind.Downed));
        foreach (var interactable in live.Interactables)
        {
            if (interactable == null) continue;
            var p = Point(interactable.Position);
            if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height) continue;
            bool visible = visibility[p.X, p.Y] == TerminalVisibility.Visible;
            bool revealed = game.FloorReveal != null &&
                ((game.FloorReveal.LayoutRevealed && interactable is Stairs) ||
                 (game.FloorReveal.TreasureRevealed && interactable is Gold));
            if (interactable is Trap trap && (trap.VisualObject == null || !trap.VisualObject.activeSelf || !visible)) continue;
            if (!visible && !revealed) continue;
            char glyph = interactable is Stairs ? '>' : interactable is Gold ? '$' :
                interactable is DroppedItem ? 'i' : interactable is Trap ? '^' :
                interactable is DungeonProp prop && prop.IsDoor ? '+' : 'p';
            actors.Add(new TerminalActor(interactable.GetInstanceID().ToString(), p, TerminalActorKind.Item, glyph, revealed));
        }
        var target = targetDialog != null && targetDialog.isActiveAndEnabled && targetDialog.TerminalSelectedCell.HasValue
            ? new TerminalTargetSnapshot(Point(targetDialog.TerminalSelectedCell.Value), targetDialog.RangeLabel,
                targetDialog.TerminalValidCells.Select(Point)) : null;
        var party = game.Allies.Where(a => a != null && a.Vitals != null).Select(a =>
            new TerminalPartyStat(a.CharacterName, a.Vitals.Level, a.Vitals.HP, a.FinalStats.HPMax,
                a.Vitals.SP, a.FinalStats.SPMax)).ToArray();
        return new DungeonTerminalSnapshot(width, height, Point(game.PlayerController.ControlledAlly.TilemapPosition),
            "Dungeon floor " + game.PlayerController.Floor, p => DungeonTerrain(floor, p), visibility, actors,
            party, Tail(messages), target);
    }
    private static TerminalCell DungeonTerrain(DungeonFloor floor, GridPoint p)
    {
        if (p.Equals(floor.Stairs)) return Cell('>', TerminalPalette.Feature);
        if (p.Equals(floor.Start)) return Cell('<', TerminalPalette.Feature);
        if (At(floor.Layers, DungeonLayers.Columns, p)) return Cell('I', TerminalPalette.Wall);
        if (At(floor.Layers, DungeonLayers.Torchlights, p)) return Cell('!', TerminalPalette.Feature);
        if (At(floor.Layers, DungeonLayers.Carpet, p)) return Cell(',', TerminalPalette.Feature);
        return floor.IsWalkable(p) ? FloorCell : Cell('#', TerminalPalette.Wall);
    }

    internal static OverworldTerminalSnapshot Overworld(OverworldScene scene, GameMessages messages = null)
    {
        var grid = scene.Map.CurrentGrid;
        var locations = scene.Campaign.Locations.Where(l => l.ParentTownId == null && grid.Locations.ContainsKey(l.Id))
            .ToDictionary(l => grid.Locations[l.Id], l => l.Kind);
        var gateOpen = grid.Locks.SelectMany(l => l.Cells.Select(c => (c, l.RouteId)))
            .ToDictionary(e => e.c, e => scene.Context.Gates.IsWalkable(e.c, scene.Held));
        var keys = new Dictionary<GridPoint, bool>();
        foreach (var route in scene.Campaign.Routes.Where(r => r.KeyLocationId != null && grid.Locations.ContainsKey(r.KeyLocationId)))
            keys[grid.Locations[route.KeyLocationId]] = scene.CollectedKeys.Contains(route.KeyId);
        var followers = scene.Followers.Where(a => a != null)
            .Select(a => new TerminalActor(a.GetInstanceID().ToString(), Point(a.TilemapPosition), TerminalActorKind.Ally));
        var actors = followers.Concat(new[] { new TerminalActor("player", scene.Position, TerminalActorKind.Player) }).ToArray();
        return new OverworldTerminalSnapshot(grid.Width, grid.Height, scene.Position, "Overworld", p =>
        {
            if (grid.WarpsAt(p).Any()) return Cell('O', TerminalPalette.Feature);
            if (keys.TryGetValue(p, out bool collected)) return Cell(collected ? 'k' : 'K', TerminalPalette.Item);
            if (locations.TryGetValue(p, out var kind)) return Cell(kind switch
            {
                LocationKind.Town => 'T', LocationKind.StoryDungeon => 'D', LocationKind.RepeatableDungeon => 'R',
                LocationKind.FinalDungeon => 'F', LocationKind.Converter => 'C', LocationKind.Secret => '?',
                LocationKind.Landmark => '*', _ => 'o'
            }, TerminalPalette.Feature);
            if (gateOpen.TryGetValue(p, out bool open)) return Cell(open ? '/' : '+', TerminalPalette.Feature);
            if (grid.RequiresBoat(p) || At(grid.Layers, OverworldLayers.Water, p)) return Cell('~', TerminalPalette.Ally);
            if (At(grid.Layers, OverworldLayers.Roads, p)) return Cell(':', TerminalPalette.Dim);
            if (At(grid.Layers, OverworldLayers.Trees, p)) return Cell('"', TerminalPalette.Item);
            return grid.IsGround(p) ? FloorCell : Cell('#', TerminalPalette.Wall);
        }, actors, null, Tail(messages));
    }

    internal static TownTerminalSnapshot Town(Town town, GameMessages messages = null)
    {
        var plan = town.Plan;
        var focus = Point(town.TownPlayer.ControllingTownAlly.TilemapPosition);
        var actors = new List<TerminalActor>();
        foreach (var ally in town.TownAllies)
            if (ally != null) actors.Add(new TerminalActor(ally.GetInstanceID().ToString(), Point(ally.TilemapPosition),
                ally == town.TownPlayer.ControllingTownAlly ? TerminalActorKind.Player : TerminalActorKind.Ally));
        foreach (var vendor in town.ShopVendors)
            if (vendor != null) actors.Add(new TerminalActor(vendor.GetInstanceID().ToString(), Point(vendor.TilemapPosition), TerminalActorKind.Npc, 'v'));
        foreach (var npc in town.Townsfolk)
            if (npc != null) actors.Add(new TerminalActor(npc.GetInstanceID().ToString(), Point(npc.Cell), TerminalActorKind.Npc));
        actors.Add(new TerminalActor("player", focus, TerminalActorKind.Player));
        var doors = town.TownBuildings.Where(b => b != null && b.Definition != null)
            .GroupBy(b => Point(b.TilemapPosition)).ToDictionary(g => g.Key,
                g => TerminalGlyphs.TownService(g.First().Definition.Id ?? g.First().Definition.DialogId));
        return new TownTerminalSnapshot(plan.Width, plan.Height, focus, "Town", p => TownTerrain(plan, doors, p), actors, null, Tail(messages));
    }
    private static TerminalCell TownTerrain(TownPlan plan, IReadOnlyDictionary<GridPoint, char> doors, GridPoint p)
    {
        if (p.Equals(plan.Exit)) return Cell('X', TerminalPalette.Feature);
        if (p.Equals(plan.DungeonEntrance)) return Cell('D', TerminalPalette.Feature);
        if (doors.TryGetValue(p, out char service)) return Cell(service, TerminalPalette.Feature);
        if (plan.BuildingIndexAt(p) != null) return Cell('*', TerminalPalette.Feature);
        if (At(plan.Layers, TownLayers.Furniture, p)) return Cell('f', TerminalPalette.Feature);
        if (At(plan.Layers, TownLayers.ShopFloor, p)) return Cell('_', TerminalPalette.Dim);
        if (At(plan.Layers, TownLayers.ShopWalls, p)) return Cell('+', TerminalPalette.Wall);
        if (At(plan.Layers, TownLayers.Houses, p)) return Cell('H', TerminalPalette.Wall);
        if (At(plan.Layers, TownLayers.Trees, p)) return Cell('T', TerminalPalette.Item);
        if (At(plan.Layers, TownLayers.MainRoads, p)) return Cell('=', TerminalPalette.Dim);
        if (At(plan.Layers, TownLayers.Alleys, p)) return Cell('-', TerminalPalette.Dim);
        if (At(plan.Layers, TownLayers.Roads, p)) return Cell(':', TerminalPalette.Dim);
        return FloorCell;
    }
    private static IEnumerable<string> Tail(GameMessages messages) => messages == null ? Array.Empty<string>() : messages.History.Skip(Math.Max(0, messages.History.Count - 2)).Select(InputPrompts.Format).ToArray();
}
