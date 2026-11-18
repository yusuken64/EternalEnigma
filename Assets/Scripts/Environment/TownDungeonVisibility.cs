using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Town roof cutaways driven by the dungeon's shared party visibility.</summary>
public sealed class TownDungeonVisibility : MonoBehaviour
{
    private DungeonFloor floor;
    private TownRoofVisual[] roofs;
    private float cellSize;
    private bool preview;
    public void Initialize(DungeonFloor value, float size, bool isPreview)
    {
        floor = value; cellSize = size; preview = isPreview;
        roofs = GetComponentsInChildren<TownRoofVisual>(true);
    }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (roofs == null) return;
        var game = Game.Instance;
        if (!preview && (game == null || !game.IsReady || game.CurrentDungeon?.Floor != floor)) return;
        foreach (var roof in roofs)
        {
            if (roof == null || roof.Room == null) continue;
            bool visibleInterior = preview;
            if (!visibleInterior)
            {
                var fog = FogOverlay.Instance;
                var cells = roof.Room.Floor;
                if (fog != null)
                    for (int i = 0; i < cells.Count; i++)
                    {
                        var p = cells[i];
                        if (fog.IsCurrentlyVisible(new Vector3(p.X, p.Y, 0) * cellSize)) { visibleInterior = true; break; }
                    }
            }
            roof.gameObject.SetActive(!visibleInterior);
        }
    }
}
