using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Small keys belong to the dungeon run, not the bag: any key opens any door on any later floor of the run.</summary>
public static class SmallKeys
{
    static DungeonSaveData Run => Common.Instance?.GameSaveData?.DungeonSaveData;
    public static int Count => Run?.SmallKeys ?? 0;
    internal static void Add() { if (Run != null) Run.SmallKeys++; }
    internal static bool TrySpend()
    {
        if (Count <= 0) return false;
        Run.SmallKeys--;
        return true;
    }
}

/// <summary>A small key dropped by a prop. Picked up like gold: it never takes a bag slot.</summary>
public sealed class SmallKey : Interactable
{
    internal override string GetInteractionText() => "";

    internal override List<GameAction> GetInteractionSideEffects(Character character)
    {
        Game.Instance.CurrentDungeon.RemoveInteractable(this);
        AudioManager.Instance.SoundEffects.BuySell.PlayAsSound();
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
        GameObject model;
        if (dungeon.SmallKeyPrefab != null)
        {
            model = Instantiate(dungeon.SmallKeyPrefab, go.transform, false);
            // Same forward tilt as the Adorable gold bag pickup.
            model.transform.localRotation = Quaternion.Euler(-33f, 0f, 0f);
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(go.transform, false);
            model.GetComponent<Renderer>().material.color = new Color(1f, .8f, .25f);
        }
        model.transform.localPosition = Vector3.zero;
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds Bounds() { var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b; }
            var bounds = Bounds();
            model.transform.localScale *= size * .5f / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.y));
            bounds = Bounds();
            var center = go.transform.position + new Vector3(size * .5f, size * .5f, 0f);
            model.transform.position += new Vector3(center.x - bounds.center.x, center.y - bounds.center.y, 0f);
        }
        DungeonPresentation.GroundFloorObject(go.transform);
        return key;
    }
}
