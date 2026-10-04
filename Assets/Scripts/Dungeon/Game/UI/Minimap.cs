using JuicyChickenGames.Menu;
using System.Collections.Generic;
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
            Destroy(minimapTexture);

        _currentDungeon = currentDungeon;
        minimapTexture = new Texture2D(_currentDungeon.dungeonWidth * 3, _currentDungeon.dungeonHeight * 3);
        minimapTexture.wrapMode = TextureWrapMode.Clamp;
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

    public void UpdateVision(HashSet<Vector3Int> visibleTiles)
    {
        // Mark previous visible tiles as explored
        for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
        {
            for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
            {
                if (dungeonMap[x, y].visibility == MinimapTileVisibility.Visible)
                    dungeonMap[x, y].visibility = MinimapTileVisibility.Explored;
            }
        }

        // Apply combined visibility
        foreach (var pos in visibleTiles)
        {
            if (pos.x >= 0 && pos.x < _currentDungeon.dungeonWidth &&
                pos.y >= 0 && pos.y < _currentDungeon.dungeonHeight)
            {
                dungeonMap[pos.x, pos.y].visibility = MinimapTileVisibility.Visible;
            }
        }
    }

    // Floor Sense: every unseen walkable tile becomes Explored.
    public void RevealLayout()
    {
        if (_currentDungeon == null || dungeonMap == null) return;
        for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
            for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
                if (!dungeonMap[x, y].isWall && dungeonMap[x, y].visibility == MinimapTileVisibility.Unseen)
                    dungeonMap[x, y].visibility = MinimapTileVisibility.Explored;
    }

    public void UpdateMinimapWithVisibleTiles(HashSet<Vector3Int> visibleTiles)
    {
        var reveal = Game.Instance.FloorReveal;
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

                PaintCell(x, y, pixelColor);
            }
        }

        // Draw player
        var playerPos = _currentDungeon.WorldToCell(Game.Instance.PlayerController.ControlledAlly.transform.position);
        PaintCell(playerPos.x, playerPos.y, PlayerColor);
        var playerController = FindFirstObjectByType<PlayerController>();

        foreach(var character in Game.Instance.AllCharacters)
        {
            var displayedCell = _currentDungeon.WorldToCell(character.transform.position);
            switch (character)
            {
                case Ally ally:
                    if (playerController.ControlledAlly == ally)
                    {
                        PaintCell(displayedCell.x, displayedCell.y, PlayerColor);
                    }
                    else
                    {
                        PaintCell(displayedCell.x, displayedCell.y, AllyColor);
                    }
                    break;
                case Enemy enemy:
                    if (DungeonSight.OverlapsVisible(visibleTiles, Character.ToBounds(enemy.FootPrint, displayedCell)) || (reveal != null && reveal.EnemiesRevealedTurns > 0))
                    {
                        PaintCell(displayedCell.x, displayedCell.y, EnemyBehavior.IsDisguised(enemy) ? ItemColor : EnemyColor);
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
                    if (visibleTiles.Contains(new Vector3Int(interactable.Position.x, interactable.Position.y, 0)))
                    {
                        PaintCell(interactable.Position.x, interactable.Position.y,
                    interactable is Stairs ? new Color(.3f,.9f,1f) :
                    interactable is DungeonProp scenery && scenery.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard ? new Color(1f,.3f,.12f) : ItemColor);
                if (interactable is Stairs) minimapTexture.SetPixel(interactable.Position.x*3+1,interactable.Position.y*3+1,Color.white);
                    }
                }
            }
            else if (visibleTiles.Contains(new Vector3Int(interactable.Position.x, interactable.Position.y, 0)) ||
                (interactable is DungeonProp prop && prop.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard && dungeonMap[prop.Position.x,prop.Position.y].visibility != MinimapTileVisibility.Unseen) ||
                (reveal != null && ((reveal.LayoutRevealed && interactable is Stairs) || (reveal.TreasureRevealed && interactable is Gold))))
            {
                PaintCell(interactable.Position.x, interactable.Position.y,
                    interactable is Stairs ? new Color(.3f,.9f,1f) :
                    interactable is DungeonProp scenery && scenery.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard ? new Color(1f,.3f,.12f) : ItemColor);
                if (interactable is Stairs) minimapTexture.SetPixel(interactable.Position.x*3+1,interactable.Position.y*3+1,Color.white);
            }
        }

        var facing = Dungeon.GetFacingOffset(playerController.ControlledAlly.CurrentFacing);
        if (playerPos.x >= 0 && playerPos.y >= 0 && playerPos.x < _currentDungeon.dungeonWidth && playerPos.y < _currentDungeon.dungeonHeight)
            minimapTexture.SetPixel(playerPos.x*3+1+facing.x,playerPos.y*3+1+facing.y,Color.white);
        minimapTexture.Apply();

        FogOverlay.UpdateFog(dungeonMap);
    }

    private void PaintCell(int x,int y,Color color)
    {
        if(x<0||y<0||x>=_currentDungeon.dungeonWidth||y>=_currentDungeon.dungeonHeight) return;
        for(int dx=0;dx<3;dx++) for(int dy=0;dy<3;dy++) minimapTexture.SetPixel(x*3+dx,y*3+dy,color);
    }

    public void Update()
    {
        if (PlayerInputHandler.Instance.mapPressed)
        {
            currentMode = (MinimapMode)(((int)currentMode + 1) % System.Enum.GetValues(typeof(MinimapMode)).Length);
            UpdateMinimapMode();
        }
    }

    private void UpdateMinimapMode()
    {
        foreach (var panel in GetComponentsInChildren<Image>(true)) panel.enabled = currentMode == MinimapMode.Full;
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
