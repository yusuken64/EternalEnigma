using JuicyChickenGames.Menu;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Minimap : MonoBehaviour
{
    public MinimapTileData[,] dungeonMap;

    public Texture2D minimapTexture;
    private Color32[] pixels;
    public RawImage minimapImage;
    public Image Frame;
    [SerializeField] private TMP_Text locationLabel;
    [SerializeField] private GameObject locationPanel;
    public Color OverlapMapColor;
    public Color FullMapColor;

    public Color VisibleGroundColor;
    public Color ExploredGroundColor;
    public Color PlayerColor;
    public Color AllyColor;
    public Color EnemyColor;
    public Color ItemColor;


    private TileWorldDungeon _currentDungeon;
    private MinimapMode currentMode;
    private bool dockHidden;
    private bool terminalHidden;
    private readonly HashSet<Vector3Int> previousVisible = new(), dirtyCells = new(), previousMarkers = new();
    private bool fullRefresh, drawingMarkers;
    private bool Incremental => _currentDungeon?.Floor?.Variant == EternalEnigma.Core.World.DungeonVariant.Overworld;
    internal void InvalidateTerrain() => fullRefresh = true;

    public FogOverlay FogOverlay;

    internal void Initialize(TileWorldDungeon currentDungeon)
    {
        if (minimapTexture != null)
            Destroy(minimapTexture);

        _currentDungeon = currentDungeon;
        previousVisible.Clear(); dirtyCells.Clear(); previousMarkers.Clear(); fullRefresh = true;
        minimapTexture = new Texture2D(_currentDungeon.dungeonWidth * 3, _currentDungeon.dungeonHeight * 3, TextureFormat.RGBA32, false);
        pixels = new Color32[minimapTexture.width * minimapTexture.height];
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
        if (Incremental)
        {
            foreach (var cell in previousVisible)
                if (!visibleTiles.Contains(cell))
                { dungeonMap[cell.x, cell.y].visibility = MinimapTileVisibility.Explored; dirtyCells.Add(cell); }
        }
        else
            for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
                for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
                    if (dungeonMap[x, y].visibility == MinimapTileVisibility.Visible)
                        dungeonMap[x, y].visibility = MinimapTileVisibility.Explored;

        // Apply combined visibility
        foreach (var pos in visibleTiles)
        {
            if (pos.x >= 0 && pos.x < _currentDungeon.dungeonWidth &&
                pos.y >= 0 && pos.y < _currentDungeon.dungeonHeight)
            {
                if (dungeonMap[pos.x, pos.y].visibility != MinimapTileVisibility.Visible) dirtyCells.Add(pos);
                dungeonMap[pos.x, pos.y].visibility = MinimapTileVisibility.Visible;
            }
        }
        previousVisible.Clear();
        foreach (var pos in visibleTiles)
            if (pos.x >= 0 && pos.y >= 0 && pos.x < _currentDungeon.dungeonWidth && pos.y < _currentDungeon.dungeonHeight) previousVisible.Add(pos);
    }

    // Floor Sense: every unseen walkable tile becomes Explored.
    public void RevealLayout()
    {
        if (_currentDungeon == null || dungeonMap == null) return;
        fullRefresh = true;
        for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
            for (int y = 0; y < _currentDungeon.dungeonHeight; y++)
                if (!dungeonMap[x, y].isWall && dungeonMap[x, y].visibility == MinimapTileVisibility.Unseen)
                    dungeonMap[x, y].visibility = MinimapTileVisibility.Explored;
    }

    public void UpdateMinimapWithVisibleTiles(HashSet<Vector3Int> visibleTiles)
    {
        var reveal = Game.Instance.FloorReveal;
        bool refreshAll = !Incremental || fullRefresh;
        drawingMarkers = false;
        if (refreshAll)
            for (int x = 0; x < _currentDungeon.dungeonWidth; x++)
                for (int y = 0; y < _currentDungeon.dungeonHeight; y++) PaintGround(new Vector3Int(x, y));
        else
        {
            foreach (var cell in dirtyCells) PaintGround(cell);
            foreach (var cell in previousMarkers) PaintGround(cell);
        }
        previousMarkers.Clear(); drawingMarkers = Incremental;

        if (Game.Instance.Finale is { } finale)
            foreach (var location in finale.Session.Floor.Enemies.Where(e => e.IsMimic && !finale.Session.Revealed.Contains(e.Id)))
                PaintCell(location.Position.X, location.Position.Y, location.IsTown ? new Color(.95f,.85f,.45f) : new Color(.65f,.75f,1f));

        // Draw player
        var playerPos = _currentDungeon.WorldToCell(Game.Instance.PlayerController.ControlledAlly.transform.position);
        PaintCell(playerPos.x, playerPos.y, PlayerColor);
        var playerController = Game.Instance.PlayerController;

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
                    if (enemy.GetComponent<FinaleActor>()?.Hidden == true) break;
                    if (DungeonSight.OverlapsVisible(visibleTiles, Character.ToBounds(enemy.FootPrint, displayedCell)) || Game.Instance.Finale?.Session.HiddenMimics == 0 || (reveal != null && reveal.EnemiesRevealedTurns > 0))
                    {
                        PaintCell(displayedCell.x, displayedCell.y, enemy.Team == Team.Player ? AllyColor :
                            EnemyBehavior.IsDisguised(enemy) ? ItemColor : EnemyColor);
                    }
                    break;
                default:
                    break;
            }
        }

        foreach (var interactable in Game.Instance.CurrentDungeon.Interactables)
        {
            if (interactable is TrailCoin) continue;
            if (interactable is Trap trap)
            {
                if (trap.VisualObject.activeSelf)
                {
                    if (reveal?.TrapsRevealed == true || visibleTiles.Contains(new Vector3Int(interactable.Position.x, interactable.Position.y, 0)))
                    {
                        PaintCell(interactable.Position.x, interactable.Position.y,
                    interactable is Stairs ? new Color(.3f,.9f,1f) :
                    interactable is DungeonProp scenery && scenery.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard ? new Color(1f,.3f,.12f) :
                    interactable is DungeonProp doorProp && doorProp.IsDoor ? new Color(.85f,.55f,.2f) : ItemColor);
                if (interactable is Stairs) PaintPixel(interactable.Position.x*3+1,interactable.Position.y*3+1,Color.white);
                    }
                }
            }
            else if (visibleTiles.Contains(new Vector3Int(interactable.Position.x, interactable.Position.y, 0)) ||
                (interactable is DungeonProp prop && prop.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard && dungeonMap[prop.Position.x,prop.Position.y].visibility != MinimapTileVisibility.Unseen) ||
                (reveal != null && ((reveal.LayoutRevealed && interactable is Stairs) || (reveal.TreasureRevealed &&
                    (interactable is Gold or DroppedItem || interactable is DungeonProp container && container.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Container)))))
            {
                PaintCell(interactable.Position.x, interactable.Position.y,
                    interactable is Stairs ? new Color(.3f,.9f,1f) :
                    interactable is DungeonProp scenery && scenery.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard ? new Color(1f,.3f,.12f) :
                    interactable is DungeonProp doorProp && doorProp.IsDoor ? new Color(.85f,.55f,.2f) : ItemColor);
                if (interactable is Stairs) PaintPixel(interactable.Position.x*3+1,interactable.Position.y*3+1,Color.white);
            }
        }

        var facing = Dungeon.GetFacingOffset(playerController.ControlledAlly.CurrentFacing);
        if (playerPos.x >= 0 && playerPos.y >= 0 && playerPos.x < _currentDungeon.dungeonWidth && playerPos.y < _currentDungeon.dungeonHeight)
            PaintPixel(playerPos.x*3+1+facing.x,playerPos.y*3+1+facing.y,Color.white);
        drawingMarkers = false;
        minimapTexture.SetPixels32(pixels);
        minimapTexture.Apply(false);

        FogOverlay.UpdateFog(dungeonMap, refreshAll ? null : dirtyCells);
        dirtyCells.Clear(); fullRefresh = false;
    }

    private void PaintGround(Vector3Int cell)
    {
        var tile = dungeonMap[cell.x, cell.y];
        PaintCell(cell.x, cell.y, tile.isWall || tile.visibility == MinimapTileVisibility.Unseen ? Color.clear :
            tile.visibility == MinimapTileVisibility.Visible ? VisibleGroundColor : ExploredGroundColor);
    }

    private void PaintCell(int x,int y,Color color)
    {
        if(x<0||y<0||x>=_currentDungeon.dungeonWidth||y>=_currentDungeon.dungeonHeight) return;
        if (drawingMarkers) previousMarkers.Add(new Vector3Int(x, y));
        Color32 value = color;
        int width = _currentDungeon.dungeonWidth * 3;
        for(int dy=0;dy<3;dy++)
        {
            int start = (y * 3 + dy) * width + x * 3;
            pixels[start] = pixels[start + 1] = pixels[start + 2] = value;
        }
    }

    private void PaintPixel(int x, int y, Color color) => pixels[y * _currentDungeon.dungeonWidth * 3 + x] = color;

    public void Update()
    {
        bool hide = MenuManager.Instance != null && MenuManager.Instance.Opened && MenuManager.Instance.CurrentDialog != MenuManager.Instance.TargetDialog;
        if (hide != dockHidden) { dockHidden = hide; UpdateMinimapMode(); }
        if (dockHidden || terminalHidden) return;
        if (PlayerInputHandler.Instance.mapPressed)
            CycleMode();
    }

    public void CycleMode()
    {
        currentMode = (MinimapMode)(((int)currentMode + 1) % System.Enum.GetValues(typeof(MinimapMode)).Length);
        AudioManager.Instance?.PlayCue(SoundCue.Navigate);
        UpdateMinimapMode();
    }

    private void UpdateMinimapMode()
    {
        // The location belongs to the map. Keep it available when only the map
        // texture is toggled off, and hide the whole group for menus/terminal mode.
        if (locationPanel != null) locationPanel.SetActive(!dockHidden && !terminalHidden);
        if (dockHidden || terminalHidden)
        {
            foreach (var panel in GetComponentsInChildren<Image>(true)) panel.enabled = false;
            minimapImage.gameObject.SetActive(false);
            return;
        }
        foreach (var panel in GetComponentsInChildren<Image>(true)) panel.enabled = currentMode == MinimapMode.Full;
        if (locationPanel != null)
            foreach (var panel in locationPanel.GetComponentsInChildren<Image>(true)) panel.enabled = true;
        if (Frame != null) Frame.enabled = currentMode != MinimapMode.Hidden;
        switch (currentMode)
        {
            case MinimapMode.Hidden:
                minimapImage.gameObject.SetActive(false);
                break;
            case MinimapMode.Overlay:
                minimapImage.color = OverlapMapColor;
                minimapImage.gameObject.SetActive(true);
                break;
            case MinimapMode.Full:
                minimapImage.color = FullMapColor;
                minimapImage.gameObject.SetActive(true);
                break;
        }
    }

#if UNITY_EDITOR
    public void AuthorLocationLabel(TMP_Text label)
    {
        locationLabel = label;
        var existing = transform.Find("Map location");
        var panel = existing != null ? (RectTransform)existing :
            GameUISkin.Rect("Map location", transform, Vector2.up, Vector2.one);
        panel.anchorMin = new Vector2(0, 1); panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(.5f, 0);
        panel.offsetMin = new Vector2(0, 8); panel.offsetMax = new Vector2(0, 62);
        locationPanel = panel.gameObject;
        var backing = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        backing.color = new Color(.10f, .15f, .17f, .92f); backing.raycastTarget = false;
        label.transform.SetParent(panel, false);
        label.name = "Floor location";
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16, 7); label.rectTransform.offsetMax = new Vector2(-16, -7);
        label.margin = Vector4.zero; label.fontSize = 26; label.fontSizeMin = 22; label.fontSizeMax = 26;
        label.enableAutoSizing = true; label.alignment = TextAlignmentOptions.Center;
        label.color = GameUITheme.LightInk; label.raycastTarget = false;
        label.text = "Floor 2 / 5";
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    public void SetTerminalPresentation(bool hidden)
    {
        if (terminalHidden == hidden) return;
        terminalHidden = hidden;
        UpdateMinimapMode();
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
