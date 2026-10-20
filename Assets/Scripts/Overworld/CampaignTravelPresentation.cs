using System.Linq;
using UnityEngine;

/// <summary>Resolves scene-local framing only after the destination has finished building.</summary>
public static class CampaignTravelPresentation
{
    public static SceneTravelPresentation Create(SceneTravelKind kind, Vector3 sourceAnchor, Camera camera, string destinationId)
    {
        return new SceneTravelPresentation(kind, sourceAnchor, destinationId, () => ResolveArrival(kind, destinationId))
        { SourceCamera = camera };
    }

    public static Vector3 TownCellAnchor(Town town, Vector3Int cell)
    {
        float size = town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        return town.WalkableMap.CellToWorld(cell) + new Vector3(.5f, .5f, -.4f) * size;
    }

    private static SceneTravelArrival ResolveArrival(SceneTravelKind kind, string destinationId)
    {
        if (kind == SceneTravelKind.TownToDungeon || kind == SceneTravelKind.HouseToBookDungeon)
        {
            var player = Game.Instance?.PlayerController;
            return player?.ControlledAlly == null ? default : new SceneTravelArrival(player.CameraController.Camera, player.ControlledAlly.transform.position);
        }
        if (kind == SceneTravelKind.TownToOverworld)
        {
            var world = Object.FindFirstObjectByType<OverworldScene>();
            if (world == null || !world.IsReady || !world.Context.Grid.Locations.TryGetValue(destinationId, out var tile)) return default;
            return new SceneTravelArrival(world.ViewCamera, world.CellCenterToWorld(tile));
        }
        var town = Object.FindFirstObjectByType<Town>();
        if (town == null || !town.IsReady || town.Configuration.Id != destinationId) return default;
        var camera = town.TownPlayer.CameraController.Camera;
        if (kind == SceneTravelKind.BookDungeonToHouse)
        {
            var book = town.DungeonBook;
            if (book == null) return default;
            book.SetOpen(true);
            return new SceneTravelArrival(camera, book.PageAnchor.position);
        }
        if (kind == SceneTravelKind.DungeonToTown)
        {
            var entrance = town.TownBuildings.FirstOrDefault(b => b.Definition.DialogId == "entrance");
            if (entrance != null) return new SceneTravelArrival(camera, TownCellAnchor(town, entrance.TilemapPosition));
        }
        var leader = town.TownPlayer.ControllingTownAlly;
        return leader == null ? default : new SceneTravelArrival(camera, leader.transform.position);
    }
}
