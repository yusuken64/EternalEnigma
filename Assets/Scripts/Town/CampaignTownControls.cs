using System.Linq;
using UnityEngine;

public sealed class CampaignTownControls : MonoBehaviour
{
    public Town Town;
    private Vector2 rosterScroll;
    public Vector3Int Exit => new(10, 0, 0);
    private void Start()
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Southern exit to overworld"; marker.transform.SetParent(transform);
        marker.transform.position = Town.WalkableMap.CellToWorld(Exit) + new Vector3(.5f, .5f, -.1f) * Town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        marker.transform.localScale = new Vector3(1, 1, .1f) * Town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        Destroy(marker.GetComponent<Collider>());
        marker.GetComponent<Renderer>().material.color = Color.cyan;
    }
    private void OnGUI()
    {
        if (!Town.IsReady || Common.Instance.Travel.IsTransitioning || Common.Instance.CampaignContext == null) return;
        GUILayout.BeginArea(new Rect(16, 16, 340, 380), GUI.skin.box);
        GUILayout.Label("Southern exit: follow the clear corridor south.");
        if (Town.TownPlayer.ControllingTownAlly.TilemapPosition == Exit && GUILayout.Button("Leave town")) Common.Instance.Travel.ExitTown(Town);
        var context = Common.Instance.CampaignContext;
        GUILayout.Label("Party: protagonist + three companions");
        rosterScroll = GUILayout.BeginScrollView(rosterScroll);
        foreach (string id in context.Roster.ToArray())
        {
            bool selected = context.Active.Contains(id);
            if (GUILayout.Button((selected ? "Bench " : "Select ") + id))
            {
                Town.WriteSaveData();
                var ids = selected ? context.Active.Where(x => x != id).ToArray() : context.Active.Concat(new[] { id }).ToArray();
                if (context.SetParty(ids)) Town.RefreshCampaignParty();
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
