using System;
using UnityEngine;

public class FogOverlay : MonoBehaviour
{
    public GameObject fogOverlayQuad;
    public Vector2 worldSize; // e.g., (100, 100)
    public Vector2 worldOrigin; // e.g., (0, 0)

    private Material fogMaterial;
    private Texture2D fogTexture;

    internal void Initialize(TileWorldDungeon currentDungeon)
    {
        worldSize = new Vector2(currentDungeon.dungeonWidth, currentDungeon.dungeonHeight);

        // Set quad size
        fogOverlayQuad.transform.localScale = new Vector3(worldSize.x * 2, worldSize.y * 2, 1);

        // Center it on world
        fogOverlayQuad.transform.position = new Vector3(
            worldOrigin.x + worldSize.x,
            worldOrigin.y + worldSize.y,
            -3.35f
        );

        // Cache material once
        fogMaterial = fogOverlayQuad.GetComponent<Renderer>().material;

        // Pass shader uniforms
        fogMaterial.SetVector("_FogWorldSize", new Vector4(worldSize.x * 2, worldSize.y * 2, 0, 0));
        fogMaterial.SetVector("_FogWorldOrigin", new Vector4(worldOrigin.x, 0, worldOrigin.y, 0));

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

    internal void UpdateFog(Minimap.MinimapTileData[,] dungeonMap, Vector3 playerPos)
    {
        int width = dungeonMap.GetLength(0);
        int height = dungeonMap.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool visible = dungeonMap[x, y].visibility == Minimap.MinimapTileVisibility.Visible;

                // Fully visible = white (alpha = 1), hidden = black (alpha = 0)
                float alpha = visible ? 1f : 0f;
                fogTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        fogTexture.Apply();
    }
}
