using UnityEngine;

/// <summary>Actual mesh support point, baked after dungeon-only pose calibration.</summary>
public sealed class DungeonPickupFootprint : MonoBehaviour
{
    public Vector3 SupportPoint;
    public float GroundZ => transform.TransformPoint(SupportPoint).z;

    internal static void Place(Transform model, Vector3 groundCenter, float maxWidth)
    {
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        Bounds BoundsOfModel()
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        var bounds = BoundsOfModel();
        float width = Mathf.Max(bounds.size.x, bounds.size.y);
        if (width > maxWidth)
        {
            model.localScale *= maxWidth / width;
            bounds = BoundsOfModel();
        }
        float ground = model.GetComponentInChildren<DungeonPickupFootprint>()?.GroundZ ?? bounds.max.z;
        model.position += groundCenter - new Vector3(bounds.center.x, bounds.center.y, ground);
    }
}
