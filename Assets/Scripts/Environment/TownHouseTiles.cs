using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Reusable cell sized house pieces for the TWC Houses and Roofs build layers.</summary>
public static class TownHouseTiles
{
    private static Mesh cube;
    public static Mesh Cube
    {
        get
        {
            if (cube != null) return cube;
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = UnityEngine.Object.Instantiate(temporary.GetComponent<MeshFilter>().sharedMesh);
            cube.name = "House tile box";
            if (Application.isPlaying) UnityEngine.Object.Destroy(temporary); else UnityEngine.Object.DestroyImmediate(temporary);
            return cube;
        }
    }

    public static Material ColorMaterial(Material source, Color tint, EnvironmentMeshOwner owner, string name, bool preserveTexture = false)
    {
        var material = new Material(source) { name = name, hideFlags = HideFlags.DontSave };
        if (!preserveTexture) material.mainTexture = null;
        if (material.HasProperty("_Color")) material.color = tint;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        owner.Materials.Add(material);
        return material;
    }

    public static (Color plaster, Color timber, Color roof, Color door, Color glass) Palette(OverworldBiome biome) => biome switch
    {
        OverworldBiome.Forest => (Hex("D7CEAA"), Hex("4E382A"), Hex("346E66"), Hex("56716B"), Hex("94AEB0")),
        OverworldBiome.Desert => (Hex("E6C99B"), Hex("855B3B"), Hex("B26A48"), Hex("5E777B"), Hex("A8C7C8")),
        OverworldBiome.Mountain => (Hex("CFCEC5"), Hex("504A46"), Hex("4D6688"), Hex("607287"), Hex("A7BCC6")),
        OverworldBiome.Marsh => (Hex("D0C6AA"), Hex("51493A"), Hex("765076"), Hex("5C7065"), Hex("9CB5A8")),
        OverworldBiome.Water => (Hex("D9DDD2"), Hex("4A5A60"), Hex("397C8B"), Hex("527B89"), Hex("B4D1D6")),
        OverworldBiome.Volcanic => (Hex("BDB2A5"), Hex("3D3435"), Hex("B15E39"), Hex("654C52"), Hex("C29A81")),
        OverworldBiome.Tundra => (Hex("E2DCD1"), Hex("5A514F"), Hex("A84759"), Hex("66818A"), Hex("B9D4DA")),
        _ => (Hex("E8D7AD"), Hex("4D3427"), Hex("BA6247"), Hex("657886"), Hex("A6C4CC"))
    };

    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }

    public static Texture2D ShingleTexture(EnvironmentMeshOwner owner)
    {
        const int width = 64, height = 64;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int course = y / 8;
            int shifted = (x + (course % 2) * 8) % width;
            int shingle = shifted / 16;
            int grain = (x * 37 + y * 17 + course * 41) % 23;
            float value = .89f + (grain - 11) * .003f + ((shingle + course * 3) % 4) * .018f;
            if (y % 8 == 0 || shifted % 16 == 0) value *= .67f;
            pixels[y * width + x] = new Color(value, value, value, 1);
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "House roof shingles", wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
        };
        texture.SetPixels(pixels); texture.Apply();
        owner.Textures.Add(texture);
        return texture;
    }

    public static void Box(EnvironmentBatch batch, Material material, float size, float x, float y, float z,
        float width, float height, float depth)
    {
        batch.Add(Cube, material, new Vector3(x, y, z) * size,
            new Vector3(width, height, depth) * size, role: SilhouetteRole.Caster);
    }

    // An exposed side is a complete facade panel. The entrance is carved into its
    // front panel, so the door's approach cell stays unobstructed and walkable.
    public static void Facade(EnvironmentBatch batch, Material plaster, Material timber, Material door,
        Material glass, float size, GridPoint cell, int dx, int dy, bool entrance, bool window,
        OverworldBiome biome=OverworldBiome.Grassland,BiomeDecorationSurfaceSet faces=null,string service="")
    {
        var catalog=DioramaCatalog.Load();
        if(catalog!=null)
        {
            string id=entrance?"FacadeDoor":window?"FacadeWindow":"FacadeWall";
            float angle=dx==1?90:dy==1?180:dx==-1?270:0;
            var position=new Vector3(cell.X+.5f+dx*.46f,cell.Y+.5f+dy*.46f,0)*size;
            var scale=new Vector3(size*.5f,size*.5f,1);
            catalog.Add(batch,id,biome,position,angle,scale:scale);
            var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,0,angle),scale);
            foreach(var socket in catalog.Get(id).Sockets)
            {
                if(socket.kind=="Service" && !string.IsNullOrEmpty(service))
                    catalog.Add(batch,"Service"+service,biome,matrix.MultiplyPoint3x4(socket.center),angle,scale:Vector3.one*1.15f);
                else if(socket.kind=="Ornament" && faces!=null)
                    faces.Faces.Add(new BiomeDecorationFace {Center=matrix.MultiplyPoint3x4(socket.center),
                        Normal=new Vector3(dx,dy,0),Width=socket.width*size*.5f,Height=socket.height,
                        Biome=biome,Surface=DecorationSurface.Facade,HouseId=cell.ToString(),PreferredKind=(int)BiomeDecorationKind.Ornament});
            }
            return;
        }
        float x = cell.X + .5f, y = cell.Y + .5f;
        bool horizontal = dy != 0;
        float cx = x + dx * .46f, cy = y + dy * .46f;
        void Part(Material m, float across, float z, float span, float depth)
        {
            float offset = m == timber ? .018f : m == door || m == glass ? -.012f : 0;
            Box(batch, m, size, cx + dx * offset + (horizontal ? across : 0),
                cy + dy * offset + (horizontal ? 0 : across), z,
                horizontal ? span : .10f, horizontal ? .10f : span, depth);
        }
        if (entrance)
        {
            Part(plaster, -.40f, -.48f, .20f, .96f);
            Part(plaster, .40f, -.48f, .20f, .96f);
            Part(plaster, 0, -.96f, 1, .12f);
            Part(timber, -.30f, -.52f, .055f, 1.05f);
            Part(timber, .30f, -.52f, .055f, 1.05f);
            Part(timber, 0, -1.04f, .66f, .075f);
            Part(door, 0, -.47f, .54f, .90f);
            Part(timber, 0, -.03f, .60f, .07f);
            Part(timber, .17f, -.48f, .045f, .045f);
        }
        else
        {
            if (window)
            {
                Part(plaster, -.345f, -.53f, .31f, 1.08f);
                Part(plaster, .345f, -.53f, .31f, 1.08f);
                Part(plaster, 0, -.22f, .38f, .46f);
                Part(plaster, 0, -.91f, .38f, .32f);
                Part(glass, 0, -.60f, .38f, .35f);
                Part(timber, 0, -.42f, .46f, .04f);
                Part(timber, 0, -.78f, .46f, .06f);
                Part(timber, -.22f, -.60f, .045f, .43f);
                Part(timber, .22f, -.60f, .045f, .43f);
                Part(timber, 0, -.60f, .035f, .35f);
            }
            else Part(plaster, 0, -.53f, 1, 1.08f);
            Part(timber, 0, -.27f, 1, .055f);
        }
        Part(timber, -.47f, -.54f, .06f, 1.10f);
        Part(timber, .47f, -.54f, .06f, 1.10f);
        Part(timber, 0, -1.09f, 1, .07f);
        Part(timber, 0, -.02f, 1, .06f);
    }

    public static Mesh RoofTile(float left, float right, float center, float halfWidth, bool ridge,bool frontEdge=false,bool backEdge=false)
    {
        float Height(float x) => RoofHeight(x, center, halfWidth);
        var vertices = new List<Vector3>(); var indices = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // Continuous underlayment closes the tiny depth gaps between shingle courses.
        for (int col = 0; col < 4; col++)
        {
            float x0 = Mathf.Lerp(left, right, col / 4f);
            float x1 = Mathf.Lerp(left, right, (col + 1) / 4f);
            float y0=frontEdge?-.10f:0,y1=backEdge?1.10f:1;
            Quad(new Vector3(x0, y0, Height(x0) + .04f), new Vector3(x0, y1, Height(x0) + .04f),
                new Vector3(x1, y1, Height(x1) + .04f), new Vector3(x1, y0, Height(x1) + .04f));
            if(frontEdge)Quad(new Vector3(x0,y0,Height(x0)),new Vector3(x1,y0,Height(x1)),new Vector3(x1,y0,Height(x1)+.09f),new Vector3(x0,y0,Height(x0)+.09f));
            if(backEdge)Quad(new Vector3(x1,y1,Height(x1)),new Vector3(x0,y1,Height(x0)),new Vector3(x0,y1,Height(x0)+.09f),new Vector3(x1,y1,Height(x1)+.09f));
        }
        // Split at the ridge and at every course, creating a visible shingle rhythm.
        for (int row = 0; row < 5; row++)
        {
            float y0 = row / 5f, y1 = (row + 1) / 5f;
            if(row==0&&frontEdge)y0=-.10f;if(row==4&&backEdge)y1=1.10f;
            for (int col = 0; col < 4; col++)
            {
                float x0 = Mathf.Lerp(left, right, col / 4f);
                float x1 = Mathf.Lerp(left, right, (col + 1) / 4f);
                float offset = row % 2 == 0 ? 0 : .012f;
                Quad(new Vector3(x0, y0, Height(x0) - offset), new Vector3(x0, y1, Height(x0) - offset),
                    new Vector3(x1, y1, Height(x1) - offset), new Vector3(x1, y0, Height(x1) - offset));
            }
        }
        var mesh = new Mesh { name = ridge ? "Roof ridge tile" : "Pitched roof tile" };
        mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
        mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x - left, v.y)).ToList());
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    public static float RoofHeight(float x, float center, float halfWidth) =>
        -(DioramaScale.Eaves/2+.07f) - .63f * (1f - Mathf.Abs(x - center) / halfWidth);

    public static Mesh GableTile(float left, float center, float halfWidth)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int half = 0; half < 2; half++)
        {
            float x0 = left + half * .5f, x1 = x0 + .5f;
            int i = vertices.Count;
            vertices.Add(new Vector3(x0, .04f, -DioramaScale.Eaves/2));
            vertices.Add(new Vector3(x0, .04f, RoofHeight(x0, center, halfWidth)));
            vertices.Add(new Vector3(x1, .04f, RoofHeight(x1, center, halfWidth)));
            vertices.Add(new Vector3(x1, .04f, -DioramaScale.Eaves/2));
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var mesh = new Mesh { name = "House gable tile" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}
