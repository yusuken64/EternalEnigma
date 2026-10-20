using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Owned, stateful props for the one opening home. Gameplay remains in OpeningHomeLesson.</summary>
public sealed class OpeningHomeProps : MonoBehaviour
{
    private EnvironmentMeshOwner owner;
    private Material furniture;
    private Transform chestLid, doorHinge;
    private GameObject bookmark, lockPlate;

    public static OpeningHomeProps Create(Town town, Vector3Int bookCell, Vector3Int chestCell, Vector3Int doorCell)
    {
        var root = new GameObject("Opening home props");
        root.transform.SetParent(town.transform, false);
        var props = root.AddComponent<OpeningHomeProps>();
        props.owner = root.AddComponent<EnvironmentMeshOwner>();
        var catalog = TownInteriorCatalog.Load();
        var biome = town.WalkableMap.TileWorldCreator.GetComponent<TownBiomeStyle>()?.Current ?? OverworldBiome.Grassland;
        props.furniture = catalog.HomeFurniture != null ? catalog.HomeFurniture : catalog.Material(biome);
        float size = town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        Transform Cell(string name, Vector3Int cell)
        {
            var node = props.Node(root.transform, name);
            node.position = town.WalkableMap.CellToWorld(cell) + new Vector3(.5f, .5f, 0) * size;
            node.localScale = Vector3.one * size;
            return node;
        }
        props.BuildBook(Cell("Book beside the bed", bookCell), catalog);
        props.BuildChest(Cell("Home key chest", chestCell));
        var door = Cell("Front door", doorCell);
        door.position = town.WalkableMap.CellToWorld(doorCell) + new Vector3(.5f, .04f, 0) * size;
        // Match the authored facade's half-cell horizontal scale and full height.
        door.localScale = new Vector3(size * .5f, size * .5f, 1);
        props.BuildDoor(door, biome);
        props.Refresh(false, false, false);
        return props;
    }

    public void Refresh(bool hasKey, bool unlocked, bool read)
    {
        if (chestLid != null) chestLid.localRotation = Quaternion.Euler(hasKey ? 105 : 0, 0, 0);
        if (doorHinge != null) doorHinge.localRotation = Quaternion.Euler(0, 0, unlocked ? 95 : 0);
        if (bookmark != null) bookmark.SetActive(!read);
        if (lockPlate != null) lockPlate.SetActive(!unlocked);
    }

    private Transform Node(Transform parent, string name)
    {
        var result = new GameObject(name).transform;
        result.SetParent(parent, false);
        return result;
    }

    private void Render(Transform parent, string name, Mesh mesh, Material material)
    {
        var node = Node(parent, name);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        var silhouette = node.gameObject.AddComponent<SilhouetteParticipant>();
        silhouette.Dynamic = true;
    }

    private void BuildBook(Transform root, TownInteriorCatalog catalog)
    {
        Render(root, "Lectern", catalog.Get("Lectern").Mesh, furniture);
        var book = Node(root, "Open book");
        book.localPosition = new Vector3(0, .04f, -.91f);
        Box(book, "Blue cloth cover", new Vector3(0, 0, .005f), new Vector3(.70f, .48f, .035f), 18);
        Box(book, "Spine", new Vector3(0, 0, -.032f), new Vector3(.035f, .45f, .07f), 4);
        foreach (int side in new[] { -1, 1 })
        {
            var page = Node(book, side < 0 ? "Left page" : "Right page");
            page.localPosition = new Vector3(side * .165f, 0, -.036f);
            page.localRotation = Quaternion.Euler(0, -side * 7, 0);
            Box(page, "Cream paper", Vector3.zero, new Vector3(.31f, .425f, .025f), 5);
            // Broad short strokes read as print at the gameplay camera's scale.
            for (int row = 0; row < 4; row++)
                Box(page, "Printed line", new Vector3(0, .13f - row * .075f, -.014f),
                    new Vector3(row == 3 ? .13f : .21f, .012f, .003f), 22);
        }
        bookmark = Node(book, "Unread ribbon").gameObject;
        Box(bookmark.transform, "Ribbon", new Vector3(.20f, -.22f, -.028f), new Vector3(.055f, .15f, .012f), 15);
    }

    private void BuildChest(Transform root)
    {
        // Hollow native chest, with an actual moving lid and an empty interior after claiming the key.
        Box(root, "Base", new Vector3(0, 0, -.055f), new Vector3(.76f, .63f, .11f), 1);
        Box(root, "Dark interior", new Vector3(0, 0, -.116f), new Vector3(.63f, .51f, .018f), 28);
        foreach (float x in new[] { -.34f, .34f })
            Box(root, "Side", new Vector3(x, 0, -.295f), new Vector3(.08f, .63f, .41f), 2);
        foreach (float y in new[] { -.275f, .275f })
            Box(root, "Front and back", new Vector3(0, y, -.295f), new Vector3(.62f, .08f, .41f), 2);
        foreach (float x in new[] { -.27f, .27f })
        {
            Box(root, "Front strap", new Vector3(x, -.324f, -.295f), new Vector3(.07f, .035f, .42f), 3);
            Box(root, "Back strap", new Vector3(x, .324f, -.295f), new Vector3(.07f, .035f, .42f), 3);
        }
        chestLid = Node(root, "Hinged lid");
        chestLid.localPosition = new Vector3(0, .30f, -.49f);
        Box(chestLid, "Lid", new Vector3(0, -.30f, -.06f), new Vector3(.79f, .68f, .12f), 2);
        foreach (float x in new[] { -.27f, .27f })
            Box(chestLid, "Lid strap", new Vector3(x, -.30f, -.127f), new Vector3(.07f, .68f, .025f), 3);
        Box(chestLid, "Brass latch", new Vector3(0, -.65f, -.025f), new Vector3(.12f, .035f, .16f), 27);
    }

    private void BuildDoor(Transform root, OverworldBiome biome)
    {
        var catalog = DioramaCatalog.Load();
        doorHinge = Node(root, "Door hinge");
        var hinge = new Vector3(-.49f, .012f, 0);
        doorHinge.localPosition = hinge;
        if (catalog?.Get("FacadeDoor")?.Prefab is GameObject prefab)
        {
            var source = prefab.GetComponent<MeshFilter>().sharedMesh;
            var materials = prefab.GetComponent<MeshRenderer>().sharedMaterials;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                string role = materials[sub].name.Split('_').Last();
                if (role is not ("Door" or "Metal")) continue;
                var vertices = source.vertices;
                var indices = source.GetTriangles(sub);
                if (role == "Metal") indices = Enumerable.Range(0, indices.Length / 3)
                    .Where(t => Enumerable.Range(t * 3, 3).All(i => vertices[indices[i]].x < .4f))
                    .SelectMany(t => indices.Skip(t * 3).Take(3)).ToArray();
                if (indices.Length == 0) continue;
                var mesh = Instantiate(source); mesh.name = "Opening home door " + role;
                mesh.vertices = vertices.Select(v => v - hinge).ToArray();
                mesh.subMeshCount = 1; mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
                owner.Meshes.Add(mesh);
                Render(doorHinge, role, mesh, catalog.Material(role, biome));
            }
        }
        else Box(doorHinge, "Wooden door", new Vector3(.49f, 0, -.47f), new Vector3(.98f, .09f, .90f), 2);
        // The brass plate faces into the room, so the locked state is legible from the player's approach.
        lockPlate = Node(doorHinge, "Locked brass plate").gameObject;
        Box(lockPlate.transform, "Plate", new Vector3(.73f, .07f, -.75f), new Vector3(.16f, .04f, .20f), 27);
        Box(lockPlate.transform, "Keyhole", new Vector3(.73f, .095f, -.75f), new Vector3(.035f, .01f, .075f), 28);
    }

    // Same bevel and palette convention as the production town furniture authoring source.
    private void Box(Transform parent, string name, Vector3 center, Vector3 dimensions, int swatch)
    {
        var half = dimensions * .5f;
        float bevel = Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .28f;
        var outline = new[] { new Vector2(-half.x + bevel, -half.y), new Vector2(half.x - bevel, -half.y),
            new Vector2(half.x, -half.y + bevel), new Vector2(half.x, half.y - bevel),
            new Vector2(half.x - bevel, half.y), new Vector2(-half.x + bevel, half.y),
            new Vector2(-half.x, half.y - bevel), new Vector2(-half.x, -half.y + bevel) };
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
        void Face(params Vector3[] face)
        {
            int offset = vertices.Count;
            vertices.AddRange(face);
            uv.AddRange(Enumerable.Repeat(new Vector2((swatch + .5f) / 32f, .625f), face.Length));
            for (int i = 1; i < face.Length - 1; i++) triangles.AddRange(new[] { offset, offset + i, offset + i + 1 });
        }
        Vector3 At(int i, float z) => center + new Vector3(outline[i].x, outline[i].y, z);
        Face(Enumerable.Range(0, 8).Reverse().Select(i => At(i, -half.z)).ToArray());
        Face(Enumerable.Range(0, 8).Select(i => At(i, half.z)).ToArray());
        for (int i = 0; i < 8; i++) Face(At(i, -half.z), At((i + 1) % 8, -half.z), At((i + 1) % 8, half.z), At(i, half.z));
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); owner.Meshes.Add(mesh);
        Render(parent, name, mesh, furniture);
    }
}
