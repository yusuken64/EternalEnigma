using System.Collections.Generic;
using UnityEngine;

/// <summary>Small keys belong to the dungeon run, not the bag: any key opens any door on any later floor of the run.</summary>
public static class SmallKeys
{
    static DungeonSaveData Run => Common.Instance?.GameSaveData?.DungeonSaveData;
    public static int Count => Run?.SmallKeys ?? 0;
    internal static void Add() { if (Run != null) { Run.SmallKeys++; HudRefresh.Request(HudChange.Resources); } }
    internal static bool TrySpend()
    {
        if (Count <= 0) return false;
        Run.SmallKeys--;
        HudRefresh.Request(HudChange.Resources);
        return true;
    }
}

/// <summary>A small key dropped by a prop. Picked up like gold: it never takes a bag slot.</summary>
public sealed class SmallKey : Interactable
{
    internal override string GetInteractionText() => "";

    internal override List<GameAction> GetInteractionSideEffects(Character character)
    {
        if (Opened || !Game.Instance.CurrentDungeon.Interactables.Contains(this)) return new();
        using var receipt = AutoplayRunner.LootPickup(this, character, 1);
        Opened = true;
        Game.Instance.CurrentDungeon.RemoveInteractable(this);
        AudioManager.Instance?.PlayCue(SoundCue.Pickup);
        SmallKeys.Add();
        GameMessages.Post("Picked up a small key.");
        return new();
    }

    internal static SmallKey Create(TileWorldDungeon dungeon, Vector3Int cell)
    {
        var go = new GameObject("Small key");
        go.transform.SetParent(dungeon.transform, false);
        go.transform.position = dungeon.CellToWorld(cell);
        var key = go.AddComponent<SmallKey>();
        key.Position = cell;
        float size = dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x;
        var prefab = dungeon.SmallKeyPrefab != null ? dungeon.SmallKeyPrefab : DioramaItemCatalog.Load().GetProp("key");
        var model = Instantiate(prefab, go.transform, false);
        var center = go.transform.position + new Vector3(size * .5f, size * .5f, 0);
        center.z = DungeonPresentation.GroundPlaneZ;
        DungeonPickupFootprint.Place(model.transform, center, size * .82f);
        return key;
    }
}
