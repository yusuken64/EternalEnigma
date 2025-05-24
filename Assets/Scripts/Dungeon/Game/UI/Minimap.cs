using System;
using UnityEngine;
using UnityEngine.UI;

public class Minimap : MonoBehaviour
{
    public MinimapTileData[,] dungeonMap;

    public Texture2D minimapTexture;
    public RawImage minimapImage;
    public Color OverlapMapColor;
    public Color FullMapColor;
    public GameObject background;

    public Color VisibleGroundColor;
    public Color ExploredGroundColor;
    public Color PlayerColor;
    public Color AllyColor;
    public Color EnemyColor;
    public Color ItemColor;


    private TileWorldDungeon _currentDungeon;
    private MinimapMode currentMode;

    public FogOverlay FogOverlay;

    internal void Initialize(TileWorldDungeon currentDungeon)
    {
        if (minimapTexture != null)
            UnityEngine.Object.Destroy(minimapTexture);

        _currentDungeon = currentDungeon;
        minimapTexture = new Texture2D(_currentDungeon.dungeonWidth, _currentDungeon.dungeonHeight);
        minimapTexture.filterMode = FilterMode.Point;
        minimapImage.texture = minimapTexture;

        dungeonMap = new MinimapTileData[_currentDungeon.dungeonWidth, _currentDungeon.dungeonHeight];

        for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
        {
            for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
            {
                dungeonMap[x, y] = new MinimapTileData();
                dungeonMap[x, y].isWall = !_currentDungeon.CanWalk(new Vector3Int(x, y, 0));
            }
        }

        currentMode = MinimapMode.Overlay;
        UpdateMinimapMode();
    }

    public void UpdateVision(BoundsInt visibleTiles)
    {
        for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
        {
            for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
            {
                if (dungeonMap[x, y].visibility == MinimapTileVisibility.Visible)
                    dungeonMap[x, y].visibility = MinimapTileVisibility.Explored;
            }
        }

        for (int x = visibleTiles.xMin; x < visibleTiles.xMax; x++)
        {
            for (int y = visibleTiles.yMin; y < visibleTiles.yMax; y++)
            {
                // Bounds check to prevent errors
                if (x >= 0 && x < _currentDungeon.dungeonWidth &&
                    y >= 0 && y < _currentDungeon.dungeonHeight)
                {
                    dungeonMap[x, y].visibility = MinimapTileVisibility.Visible;
                }
            }
        }
    }

    public void UpdateMinimap(BoundsInt visionBounds)
    {
        for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
        {
            for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
            {
                Color pixelColor = Color.clear;

                switch (dungeonMap[x, y].visibility)
                {
                    case MinimapTileVisibility.Unseen: pixelColor = Color.clear; break;
                    case MinimapTileVisibility.Explored:
                        pixelColor = dungeonMap[x, y].isWall ? Color.clear : ExploredGroundColor;
                        break;
                    case MinimapTileVisibility.Visible:
                        pixelColor = dungeonMap[x, y].isWall ? Color.clear : VisibleGroundColor;
                        break;
                }

                minimapTexture.SetPixel(x, y, pixelColor);
            }
        }

        // Draw player
        var playerPos = Game.Instance.PlayerCharacter.TilemapPosition;
        minimapTexture.SetPixel(playerPos.x, playerPos.y, PlayerColor);

        foreach(var character in Game.Instance.AllCharacters)
        {
            switch (character)
            {
                case Player player:
                    minimapTexture.SetPixel(player.TilemapPosition.x, player.TilemapPosition.y, PlayerColor);
                    break;
                case Ally ally:
                    minimapTexture.SetPixel(ally.TilemapPosition.x, ally.TilemapPosition.y, AllyColor);
                    break;
                case Enemy enemy:
                    if (visionBounds.Contains(new Vector3Int(enemy.TilemapPosition.x, enemy.TilemapPosition.y, 0)))
                    {
                        minimapTexture.SetPixel(enemy.TilemapPosition.x, enemy.TilemapPosition.y, EnemyColor);
                    }
                    break;
                default:
                    break;
            }
        }

        foreach (var interactable in Game.Instance.CurrentDungeon.Interactables)
        {
            if (interactable is Trap trap)
            {
                if (trap.VisualObject.activeSelf)
                {
                    if (visionBounds.Contains(new Vector3Int(interactable.Position.x, interactable.Position.y, 0)))
                    {
                        minimapTexture.SetPixel(interactable.Position.x, interactable.Position.y, ItemColor);
                    }
                }
            }
            else if (visionBounds.Contains(new Vector3Int(interactable.Position.x, interactable.Position.y, 0)))
            {
                minimapTexture.SetPixel(interactable.Position.x, interactable.Position.y, ItemColor);
            }
        }

        minimapTexture.Apply();

        FogOverlay.UpdateFog(dungeonMap, Game.Instance.PlayerCharacter.transform.position);
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            currentMode = (MinimapMode)(((int)currentMode + 1) % System.Enum.GetValues(typeof(MinimapMode)).Length);
            UpdateMinimapMode();
        }
    }

    private void UpdateMinimapMode()
    {
        switch (currentMode)
        {
            case MinimapMode.Hidden:
                minimapImage.gameObject.SetActive(false);
                background.gameObject.SetActive(false);
                break;
            case MinimapMode.Overlay:
                minimapImage.color = OverlapMapColor;
                minimapImage.gameObject.SetActive(true);
                background.gameObject.SetActive(false);
                break;
            case MinimapMode.Full:
                minimapImage.color = FullMapColor;
                minimapImage.gameObject.SetActive(true);
                background.gameObject.SetActive(true);
                break;
        }
    }

    public enum MinimapMode
{
    Hidden,
    Overlay,
    Full
}

public enum MinimapTileVisibility { Unseen, Explored, Visible }

public class MinimapTileData
{
    public bool isWall;
    public MinimapTileVisibility visibility;
}
}