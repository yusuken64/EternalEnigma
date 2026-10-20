using System.Linq;
using UnityEngine;

/// <summary>Quiet hostile footprint; selection changes brightness as well as colour.</summary>
public sealed class EnemyGroundMarker : MonoBehaviour
{
    private Enemy owner;
    private SpriteRenderer marker;
    private Vector3 restScale;
    private Sprite original;
    private bool friendly;

    private void Awake()
    {
        owner = GetComponent<Enemy>();
        marker = GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == "Circle");
        if (marker == null) return;
        original = marker.sprite;
        friendly = owner.Team == Team.Player;
        SelectionRing.Apply(marker, hostile: !friendly);
        restScale = marker.transform.localScale;
    }

    private void LateUpdate()
    {
        if (marker == null || owner == null) return;
        if (friendly != (owner.Team == Team.Player))
        {
            friendly = owner.Team == Team.Player;
            marker.sprite = original;
            SelectionRing.Apply(marker, hostile: !friendly);
        }
        marker.enabled = owner.DisplayedVitals.HP > 0 && !EnemyBehavior.IsDisguised(owner);
        bool selected = MenuManager.Instance != null && MenuManager.Instance.TargetDialog.CameraTarget == owner;
        marker.color = selected ? new Color(1f, .85f, .48f, 1f) : owner.Team == Team.Player ?
            new Color(.23f, .69f, .58f, .8f) : new Color(.85f, .39f, .31f, .68f);
        marker.transform.localScale = restScale * (selected ? 1.08f : 1f);
    }
}
