using UnityEngine;

/// <summary>Actual mesh support point, baked after dungeon-only pose calibration.</summary>
public sealed class DungeonPickupFootprint : MonoBehaviour
{
    public Vector3 SupportPoint;
    public float GroundZ => transform.TransformPoint(SupportPoint).z;
}
