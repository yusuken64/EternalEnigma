using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>An optional adventure on existing furniture; never changes the town's generated occupancy.</summary>
public sealed class TownDungeonBook : MonoBehaviour
{
    public Vector3Int Tile { get; private set; }
    public Transform PageAnchor { get; private set; }
    public bool IsOpen { get; private set; }
    public BookDungeonDefinition Definition => Common.InstanceOrNull?.CampaignContext?.BookDungeon;
    private Town town;
    private Transform cover;
    private GameObject bookmark;
    private EnvironmentMeshOwner owner;
    private Material furniture;
    private bool promptOpen;

    public static bool IsHostTown(Town town) => town?.Configuration != null &&
        Common.InstanceOrNull?.CampaignContext?.BookDungeon?.ParentTownId == town.Configuration.Id;

    public static bool TryFindTable(TownPlan plan, out TownPropPlacement table, TownConfiguration configuration = null)
    {
        table = null;
        if (plan == null) return false;
        // Version-zero layouts label the configured home Residential. Match the
        // authored slot mapping rather than changing that legacy town's geometry.
        GridPoint? homeDoor = null;
        if (configuration?.SlotBuildings != null)
            for (int slot = 0; slot < configuration.SlotBuildings.Count && slot < plan.BuildingSlots.Count; slot++)
                if (configuration.SlotBuildings[slot]?.Id == "home") { homeDoor = plan.BuildingSlots[slot]; break; }
        var reachable = new HashSet<GridPoint>(GridSearch.VisitOrder(plan.PartySpawn, plan.CardinalNeighbors));
        table = plan.Interiors.Where(i => i.Spec.Kind == TownInteriorKind.Residential &&
                (!homeDoor.HasValue || !i.Door.Equals(homeDoor.Value)))
            .OrderBy(i => i.Door.Y).ThenBy(i => i.Door.X)
            .SelectMany(i => i.Props.Where(p => p.Asset == "Table").OrderBy(p => p.Cell.Y).ThenBy(p => p.Cell.X))
            .FirstOrDefault(p => plan.CardinalNeighbors(p.Cell).Any(reachable.Contains));
        return table != null;
    }

    public static TownDungeonBook Create(Town town)
    {
        foreach (var old in town.GetComponentsInChildren<TownDungeonBook>(true))
        { old.gameObject.SetActive(false); DungeonPresentation.Release(old.gameObject); }
        if (!IsHostTown(town)) return null;
        if (!TryFindTable(town.Plan, out var table, town.Configuration))
            throw new InvalidOperationException("The book dungeon requires a reachable residential table.");
        var root = new GameObject("The Other Country - adventure book");
        root.transform.SetParent(town.transform, false);
        var book = root.AddComponent<TownDungeonBook>();
        book.town = town;
        book.Tile = table.Cell.ToCell();
        float size = town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        root.transform.position = town.WalkableMap.CellToWorld(book.Tile) + new Vector3(.5f, .5f, -.67f - table.Elevation) * size;
        root.transform.localScale = Vector3.one * size;
        root.transform.localRotation = Quaternion.Euler(0, 0, table.QuarterTurns * 90);
        book.owner = root.AddComponent<EnvironmentMeshOwner>();
        var catalog = TownInteriorCatalog.Load();
        book.furniture = catalog.HomeFurniture != null ? catalog.HomeFurniture : catalog.Material(book.Definition.Biome);
        book.Build();
        book.SetOpen(false);
        book.RefreshBookmark();
        return book;
    }

    public bool TryGetStandCell(out Vector3Int cell)
    {
        foreach (var direction in new[] { Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.up })
            if (town.CanEnter(Tile + direction)) { cell = Tile + direction; return true; }
        cell = default;
        return false;
    }

    public bool TryInteract(Vector3Int target)
    {
        if (target != Tile) return false;
        var common = Common.InstanceOrNull;
        var player = town?.TownPlayer;
        var hero = player?.ControllingTownAlly;
        if (promptOpen || Definition == null || !town.IsReady || hero == null || player.IsBusy ||
            player.CutsceneLocked || common.Travel.IsTransitioning || common.GlobalSettings.IsOpen ||
            MenuUIInputModule.Active?.HasDialog == true ||
            hero.TilemapPosition + Dungeon.GetFacingOffset(hero.CurrentFacing) != Tile) return false;
        player.StopHeldWalk();
        promptOpen = true;
        player.CutsceneLocked = true;
        void Release() { promptOpen = false; if (player != null) player.CutsceneLocked = false; }
        try
        {
            BookDungeonPrompt.Show(town, Definition, () =>
            {
                Release();
                if (!common.Travel.EnterBookDungeon(town, this))
                    GameMessages.Reject("The book cannot be entered right now.");
            }, Release);
        }
        catch { Release(); throw; }
        return true;
    }

    public IEnumerator Open()
    {
        if (IsOpen) yield break;
        if (DungeonPreferences.AnimationMode != DungeonAnimationMode.NoAnimations && !TerminalMode.Effective)
            for (float elapsed = 0; elapsed < .25f; elapsed += Time.unscaledDeltaTime)
            {
                float amount = Mathf.Clamp01(elapsed / .25f);
                cover.localRotation = Quaternion.Euler(0, 178 * (1 - Mathf.Pow(1 - amount, 3)), 0);
                yield return null;
            }
        SetOpen(true);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (cover != null) cover.localRotation = Quaternion.Euler(0, open ? 178 : 0, 0);
    }

    private void Update() => RefreshBookmark();
    private void RefreshBookmark()
    {
        if (bookmark != null) bookmark.SetActive(Definition != null &&
            Common.InstanceOrNull.CampaignContext.Completed.Contains(Definition.Id));
    }

    private void Build()
    {
        // The spread stays within the existing table. Up is -Z in the game's world.
        Box(transform, "Cloth back", new Vector3(.165f, 0, -.013f), new Vector3(.35f, .48f, .026f), 18);
        Box(transform, "Paper edges", new Vector3(.17f, 0, -.052f), new Vector3(.31f, .44f, .055f), 5);
        Box(transform, "Spine", new Vector3(0, 0, -.047f), new Vector3(.035f, .48f, .07f), 4);
        cover = Node(transform, "Opening cover");
        cover.localPosition = new Vector3(0, 0, -.09f);
        Box(cover, "Blue cloth cover", new Vector3(.165f, 0, 0), new Vector3(.35f, .48f, .022f), 18);
        Box(cover, "Gold title plate", new Vector3(.17f, .08f, -.013f), new Vector3(.23f, .06f, .005f), 27);
        Box(cover, "Cream inside cover", new Vector3(.17f, 0, .013f), new Vector3(.31f, .44f, .005f), 5);
        var art = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
        var texture = art?.Backgrounds.FirstOrDefault(b => b.Biome == Definition.Biome)?.Texture ?? art?.Neutral;
        var material = new Material(TownInteriorCatalog.Load().Material(Definition.Biome))
            { name = "Book landscape illustration", mainTexture = texture };
        owner.Materials.Add(material);
        var pageMesh = new Mesh { name = "Book landscape page" };
        pageMesh.vertices = new[] { new Vector3(.025f,-.19f,-.082f),new Vector3(.315f,-.19f,-.082f),
            new Vector3(.315f,.19f,-.082f),new Vector3(.025f,.19f,-.082f) };
        pageMesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        pageMesh.triangles = new[] { 0,2,1,0,3,2 };
        pageMesh.RecalculateNormals(); pageMesh.RecalculateBounds(); owner.Meshes.Add(pageMesh);
        Render(transform, "Illustrated country", pageMesh, material);
        PageAnchor = Node(transform, "Illustration camera anchor");
        PageAnchor.localPosition = new Vector3(.17f, 0, -.085f);
        bookmark = Node(transform, "Cleared bookmark").gameObject;
        Box(bookmark.transform, "Crimson ribbon", new Vector3(.24f, -.235f, -.09f), new Vector3(.05f,.16f,.012f),15);
    }

    private static Transform Node(Transform parent, string name)
    {
        var node = new GameObject(name).transform; node.SetParent(parent, false); return node;
    }

    private static void Render(Transform parent, string name, Mesh mesh, Material material)
    {
        var node = Node(parent, name);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        node.gameObject.AddComponent<SilhouetteParticipant>().Dynamic = true;
    }

    private void Box(Transform parent, string name, Vector3 center, Vector3 dimensions, int swatch)
    {
        var half = dimensions * .5f;
        var vertices = new List<Vector3>(); var indices = new List<int>();
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = vertices.Count; vertices.AddRange(new[] { center+a,center+b,center+c,center+d });
            indices.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 });
        }
        float x=half.x,y=half.y,z=half.z;
        Face(new(-x,-y,-z),new(-x,y,-z),new(x,y,-z),new(x,-y,-z));
        Face(new(-x,-y,z),new(x,-y,z),new(x,y,z),new(-x,y,z));
        Face(new(-x,-y,-z),new(x,-y,-z),new(x,-y,z),new(-x,-y,z));
        Face(new(-x,y,-z),new(-x,y,z),new(x,y,z),new(x,y,-z));
        Face(new(-x,-y,-z),new(-x,-y,z),new(-x,y,z),new(-x,y,-z));
        Face(new(x,-y,-z),new(x,y,-z),new(x,y,z),new(x,-y,z));
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetTriangles(indices,0);
        mesh.SetUVs(0, Enumerable.Repeat(new Vector2((swatch+.5f)/32f,.625f),vertices.Count).ToList());
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); owner.Meshes.Add(mesh);
        Render(parent,name,mesh,furniture);
    }
}
