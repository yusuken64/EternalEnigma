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
    public GameAction Damage(Character source, int amount) => new PropDamageAction(this, amount);
    internal override string GetInteractionText() => Definition.Kind == DungeonSceneryKind.Container ? "Open container" : Definition.Kind.ToString();
    internal override List<GameAction> GetInteractionSideEffects(Character character)
    {
        if (Definition.Kind != DungeonSceneryKind.Container || Opened ||
            !GridMovement.CanStep(character.TilemapPosition, Position, cell => cell == Position || Game.Instance.CurrentDungeon.IsWalkable(cell))) return new();
        ReleaseReward();
        return new();
    }
    internal void ApplyDamage(int damage)
    {
        if (!Alive) return;
        HitPoints = Mathf.Max(0, HitPoints - Mathf.Max(0, damage));
        if (HitPoints == 0) ReleaseReward();
    }
    void ReleaseReward()
    {
        if (Opened) return;
        Opened = true; // Commit before any drop callback; duplicate actions cannot award twice.
        var dungeon = Game.Instance.CurrentDungeon;
        dungeon.Interactables.Remove(this);
        if (Definition.Reward == SceneryReward.Item)
            dungeon.SetDroppedItem(Position, Common.Instance.ItemManager.GetRandomDrop(Definition.Roll));
        else if (Definition.Reward == SceneryReward.Gold)
            dungeon.SetTreasure(Position,Definition.Roll);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    internal static DungeonProp Create(TileWorldDungeon dungeon, DungeonScenery definition, DungeonTheme theme)
    {
        var go = new GameObject(definition.Kind.ToString());
        go.transform.SetParent(dungeon.transform, false);
        go.transform.position = dungeon.CellToWorld(definition.Cell.ToCell());
        var prop = go.AddComponent<DungeonProp>();
        prop.Definition = definition; prop.Position = definition.Cell.ToCell(); prop.HitPoints = definition.HitPoints;
        var visual = new GameObject("Scenery visual"); visual.transform.SetParent(go.transform,false);
        float size=dungeon.CellToWorld(Vector3Int.right).x-dungeon.CellToWorld(Vector3Int.zero).x;
        visual.transform.localPosition=new Vector3(size*.5f,size*.5f,-.5f);
        var biome=theme != null ? theme.Biome : OverworldBiome.Grassland;
        Mesh mesh=null;
        if(definition.Kind==DungeonSceneryKind.Destructible && (biome==OverworldBiome.Forest || biome==OverworldBiome.Marsh))
            mesh=Resources.Load<Mesh>("DungeonThemes/CryptRoots");
        if(mesh==null)
        {
            mesh=DungeonSceneryGeometry.Build(definition.Kind,biome);
            go.AddComponent<EnvironmentMeshOwner>().Meshes.Add(mesh);
        }
        else
        {
            float scale=Mathf.Min(size*.6f/Mathf.Max(.01f,Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y)),.9f/Mathf.Max(.01f,mesh.bounds.size.z));
            visual.transform.localScale=Vector3.one*scale;
            visual.transform.localPosition=new Vector3(size*.5f,size*.5f,-.9f)-mesh.bounds.center*scale;
        }
        visual.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=visual.AddComponent<MeshRenderer>();
        if(theme!=null) renderer.sharedMaterial=definition.Kind==DungeonSceneryKind.Hazard && theme.PoolMaterial!=null ? theme.PoolMaterial : theme.DecorationMaterial;
        dungeon.Interactables.Add(prop);
        return prop;
    }
}

internal sealed class PropDamageAction : GameAction
{
    readonly DungeonProp target;
    readonly int damage;
    internal PropDamageAction(DungeonProp target, int damage) { this.target = target; this.damage = damage; }
    internal override bool IsValid(Character character) => target != null && target.Alive;
    internal override List<GameAction> ExecuteImmediate(Character character) { if (IsValid(character)) target.ApplyDamage(damage); return new(); }
    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
}
