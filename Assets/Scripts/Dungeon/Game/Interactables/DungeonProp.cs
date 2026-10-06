using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

// Damageable scenery never becomes a Character or enters turn/victory/experience lists.
internal interface IDungeonDamageTarget
{
    Vector3Int Cell { get; }
    bool Alive { get; }
    GameAction Damage(Character source, int amount);
}

internal sealed class CharacterDamageTarget : IDungeonDamageTarget
{
    readonly Character character;
    internal CharacterDamageTarget(Character character) { this.character = character; }
    public Vector3Int Cell => character.TilemapPosition;
    public bool Alive => character != null && character.Vitals.HP > 0;
    public GameAction Damage(Character source, int amount) => new TakeDamageAction(source, character, amount);
}

public sealed class DungeonProp : Interactable, IDungeonDamageTarget
{
    public DungeonScenery Definition { get; private set; }
    public int HitPoints { get; private set; }
    public Vector3Int Cell => Position;
    public bool Alive => !Opened && HitPoints > 0;
    public bool BlocksMovement => !Opened && Definition.Kind != DungeonSceneryKind.Hazard;
    public bool IsDoor => Definition.Kind == DungeonSceneryKind.Door;
    public bool IsClosedDoor => IsDoor && !Opened;
    public GameAction Damage(Character source, int amount) => new PropDamageAction(this, amount);
    internal override string GetInteractionText() => Definition.Kind == DungeonSceneryKind.Container ? "Open container" :
        IsDoor ? "Locked door" : Definition.Kind.ToString();
    internal override List<GameAction> GetInteractionSideEffects(Character character)
    {
        if ((Definition.Kind != DungeonSceneryKind.Container && !IsDoor) || Opened ||
            !GridMovement.CanStep(character.TilemapPosition, Position, cell => cell == Position || Game.Instance.CurrentDungeon.IsWalkable(cell))) return new();
        if (IsDoor)
        {
            // Without a key the door can still be bashed open; it is never required.
            if (!SmallKeys.TrySpend()) { GameMessages.Post("The door is locked."); return new(); }
            GameMessages.Post("Unlocked the door.");
        }
        ReleaseReward();
        return new();
    }
    /// <summary>Opens a door without a key (lockpicking and similar skills).</summary>
    internal void Unlock() { if (IsClosedDoor) ReleaseReward(); }
    internal void ApplyDamage(int damage)
    {
        if (!Alive) return;
        HitPoints = Mathf.Max(0, HitPoints - Mathf.Max(0, damage));
        if (HitPoints == 0)
        {
            if (IsDoor) GameMessages.Post("The door gave way!");
            ReleaseReward();
        }
    }
    void ReleaseReward()
    {
        if (Opened) return;
        Opened = true; // Commit before any drop callback; duplicate actions cannot award twice.
        var dungeon = Game.Instance.CurrentDungeon;
        dungeon.Interactables.Remove(this);
        if (Definition.Reward == SceneryReward.Item)
            dungeon.SetDroppedItem(Position, Common.Instance.ItemManager.GetRandomDrop(Definition.Roll, Game.Instance.PlayerController.Floor));
        else if (Definition.Reward == SceneryReward.Gold)
            dungeon.SetTreasure(Position,Definition.Roll);
        if (Definition.HoldsKey)
        {
            // Beside any other reward, so both stay collectable.
            dungeon.SetSmallKey(Definition.Reward == SceneryReward.None ? Position : dungeon.GetDropPosition(Position));
            GameMessages.Post("A small key fell out!");
        }
        if (IsDoor) { dungeon.DoorsChanged(); Game.Instance.UpdateMiniMap(); }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    internal static DungeonProp Create(TileWorldDungeon dungeon, DungeonScenery definition, DungeonTheme theme, GameObject visualPrefab = null)
    {
        var go = new GameObject(definition.Kind.ToString());
        go.transform.SetParent(dungeon.transform, false);
        go.transform.position = dungeon.CellToWorld(definition.Cell.ToCell());
        var prop = go.AddComponent<DungeonProp>();
        prop.Definition = definition; prop.Position = definition.Cell.ToCell(); prop.HitPoints = definition.HitPoints;
        if (definition.Kind == DungeonSceneryKind.Door)
        {
            CreateDoorVisual(dungeon, go.transform, prop.Position);
            dungeon.Interactables.Add(prop);
            return prop;
        }
        if (visualPrefab != null)
        {
            var model=Instantiate(visualPrefab,go.transform,false);
            float cellSize=dungeon.CellToWorld(Vector3Int.right).x-dungeon.CellToWorld(Vector3Int.zero).x;
            model.transform.localPosition=new Vector3(cellSize*.5f,cellSize*.5f,0);
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0) {
                float bottom=renderers.Max(r=>r.bounds.max.z);
                model.transform.position+=Vector3.forward*(DungeonPresentation.GroundPlaneZ-bottom);
            }
            dungeon.Interactables.Add(prop);
            return prop;
        }
        float size = dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x;
        DungeonPropModels.Create(DungeonPropModels.ModelFor(definition.Kind, theme != null ? theme.Biome : OverworldBiome.Grassland), go.transform, size);
        dungeon.Interactables.Add(prop);
        return prop;
    }

    // Doorways are one tile between two walls; the door spans the gap between them.
    static void CreateDoorVisual(TileWorldDungeon dungeon, Transform parent, Vector3Int cell)
    {
        float size = dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x;
        if (dungeon.LockedDoorPrefab == null) { DungeonPropModels.Create("Crate", parent, size); return; }
        bool spansX = !dungeon.IsFloorCell(cell + Vector3Int.left) && !dungeon.IsFloorCell(cell + Vector3Int.right);
        var model = Instantiate(dungeon.LockedDoorPrefab, parent, false);
        // The model is authored Y-up and X-wide; this world is -Z up.
        model.transform.localRotation = Quaternion.Euler(0f, 0f, spansX ? 0f : 90f) * Quaternion.Euler(-90f, 0f, 0f);
        model.transform.localPosition = Vector3.zero;
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds Bounds() { var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b; }
        var bounds = Bounds();
        model.transform.localScale *= size / Mathf.Max(.01f, spansX ? bounds.size.x : bounds.size.y);
        bounds = Bounds();
        var center = parent.position + new Vector3(size * .5f, size * .5f, 0f);
        model.transform.position += new Vector3(center.x - bounds.center.x, center.y - bounds.center.y, DungeonPresentation.GroundPlaneZ - bounds.max.z);
    }
}

internal sealed class PropDamageAction : GameAction
{
    internal Vector3Int Cell => target != null ? target.Position : default;
    readonly DungeonProp target;
    readonly int damage;
    internal PropDamageAction(DungeonProp target, int damage) { this.target = target; this.damage = damage; }
    internal override bool IsValid(Character character) => target != null && target.Alive;
    internal override List<GameAction> ExecuteImmediate(Character character) { if (IsValid(character)) target.ApplyDamage(damage); return new(); }
    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
}
