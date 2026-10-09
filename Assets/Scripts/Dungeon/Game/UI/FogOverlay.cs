using System;
using UnityEngine;

public class FogOverlay : MonoBehaviour
{
    internal static FogOverlay Instance { get; private set; }
    private Minimap.MinimapTileData[,] visibilityMap;
    private float cellSize;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (fogTexture != null) Destroy(fogTexture);
        if (fogMaterial != null) Destroy(fogMaterial);
    }

    internal bool IsCurrentlyVisible(Vector3 worldPosition, FootPrint footprint = FootPrint.Size1x1)
    {
        if (visibilityMap == null || cellSize <= 0) return false;
        int x = Mathf.RoundToInt(worldPosition.x / cellSize);
        int y = Mathf.RoundToInt(worldPosition.y / cellSize);
        foreach (var cell in Character.ToBounds(footprint, new Vector3Int(x, y, 0)).allPositionsWithin)
            if (cell.x >= 0 && cell.y >= 0 && cell.x < visibilityMap.GetLength(0) && cell.y < visibilityMap.GetLength(1)
                && visibilityMap[cell.x, cell.y].visibility == Minimap.MinimapTileVisibility.Visible) return true;
        return false;
    }

    internal bool IsExplored(Vector3 position)
    {
        if(visibilityMap==null || cellSize<=0)return false;
        int x=Mathf.RoundToInt(position.x/cellSize),y=Mathf.RoundToInt(position.y/cellSize);
        return x>=0 && y>=0 && x<visibilityMap.GetLength(0) && y<visibilityMap.GetLength(1) && visibilityMap[x,y].visibility!=Minimap.MinimapTileVisibility.Unseen;
    }
    public GameObject fogOverlayQuad;
    public Vector2 worldSize; // e.g., (100, 100)
    public Vector2 worldOrigin; // e.g., (0, 0)

    private Material fogMaterial;
    private Texture2D fogTexture;
    private byte[] pixels;
    internal Texture2D VisibilityTexture => fogTexture;
    internal Vector4 ShadowBounds => new(worldOrigin.x,worldOrigin.y,worldSize.x*cellSize,worldSize.y*cellSize);

    internal void Initialize(TileWorldDungeon currentDungeon)
    {
        visibilityMap = null;
        cellSize = currentDungeon.CellToWorld(Vector3Int.right).x;
        if (fogTexture != null) Destroy(fogTexture);
        worldSize = new Vector2(currentDungeon.dungeonWidth, currentDungeon.dungeonHeight);

        // Cover tall dungeon boundaries. Compensate the added height along the view
        // ray so the existing fog-mask projection and gameplay visibility stay fixed.
        float coverHeight=Mathf.Max(3.35f,currentDungeon.PresentationWallHeight+.35f);
        float padding=coverHeight+cellSize*2;
        fogOverlayQuad.transform.localScale=new Vector3(worldSize.x*cellSize+padding*2,worldSize.y*cellSize+padding*2,1);
        var direction=Camera.main!=null?Camera.main.transform.forward:new Vector3(0,12,14).normalized;
        var shift=direction.z>.001f ? -(Vector2)direction*((coverHeight-3.35f)/direction.z) : Vector2.zero;
        fogOverlayQuad.transform.position = new Vector3(
            worldOrigin.x + worldSize.x * cellSize / 2+shift.x,
            worldOrigin.y + worldSize.y * cellSize / 2+shift.y,
            -coverHeight
        );

        // Cache material once
        if (fogMaterial == null) fogMaterial = fogOverlayQuad.GetComponent<Renderer>().material;

        // Pass shader uniforms
        fogMaterial.SetVector("_FogWorldSize", new Vector4(worldSize.x * cellSize, worldSize.y * cellSize, 0, 0));
        fogMaterial.SetVector("_FogWorldOrigin", new Vector4(worldOrigin.x+shift.x, worldOrigin.y+shift.y, 0, 0));

        // Create and setup texture once
        int width = currentDungeon.dungeonWidth;
        int height = currentDungeon.dungeonHeight;
        fogTexture = new Texture2D(width, height, TextureFormat.Alpha8, false);
        fogTexture.filterMode = FilterMode.Bilinear;
        //fogTexture.filterMode = FilterMode.Point;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        // Assign texture once
        fogMaterial.SetTexture("_FogTex", fogTexture);
    }

    internal void UpdateFog(Minimap.MinimapTileData[,] dungeonMap)
    {
        visibilityMap = dungeonMap;
        int width = dungeonMap.GetLength(0);
        int height = dungeonMap.GetLength(1);
        if (pixels == null || pixels.Length != width * height) pixels = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // The shader inverts this value to produce fog opacity.
                float alpha = dungeonMap[x, y].visibility switch
                {
                    Minimap.MinimapTileVisibility.Visible => 1f,
                    Minimap.MinimapTileVisibility.Explored => 0.5f,
                    _ => 0f
                };
                pixels[y * width + x] = (byte)Mathf.RoundToInt(alpha * 255);
            }
        }

        fogTexture.SetPixelData(pixels, 0);
        fogTexture.Apply(false);
    }
}
