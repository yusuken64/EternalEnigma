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

    public GameObject fogOverlayQuad;
    public Vector2 worldSize; // e.g., (100, 100)
    public Vector2 worldOrigin; // e.g., (0, 0)

    private Material fogMaterial;
    private Texture2D fogTexture;

    internal void Initialize(TileWorldDungeon currentDungeon)
    {
        visibilityMap = null;
        cellSize = currentDungeon.CellToWorld(Vector3Int.right).x;
        if (fogTexture != null) Destroy(fogTexture);
        worldSize = new Vector2(currentDungeon.dungeonWidth, currentDungeon.dungeonHeight);

        // Set quad size
        fogOverlayQuad.transform.localScale = new Vector3(worldSize.x * cellSize, worldSize.y * cellSize, 1);

        // Center it on world
        fogOverlayQuad.transform.position = new Vector3(
            worldOrigin.x + worldSize.x * cellSize / 2,
            worldOrigin.y + worldSize.y * cellSize / 2,
            -3.35f
        );

        // Cache material once
        if (fogMaterial == null) fogMaterial = fogOverlayQuad.GetComponent<Renderer>().material;

        // Pass shader uniforms
        fogMaterial.SetVector("_FogWorldSize", new Vector4(worldSize.x * cellSize, worldSize.y * cellSize, 0, 0));
        fogMaterial.SetVector("_FogWorldOrigin", new Vector4(worldOrigin.x, worldOrigin.y, 0, 0));

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
                fogTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        fogTexture.Apply();
    }
}
