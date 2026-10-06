using System;
using UnityEngine;

[Serializable]
public sealed class CombatEffectStage
{
    public GameObject Prefab;
    public Vector3 Offset;
    public Vector3 Rotation;
    [Tooltip("Place impacts in front of the target's model and enlarge them for bigger targets.")]
    public bool FitToTarget;
    [Min(.01f)] public float Scale = 1;
    [Min(0)] public float Delay;
    [Min(.1f)] public float Lifetime = 2;
    [Tooltip("Minimum ground diameter in grid cells. Circles and auras use at least 3.")]
    [Min(0)] public float MinimumDiameterCells;
    [Tooltip("Measured diameter of the source effect at scale 1, before rotation.")]
    [Min(.01f)] public float ReferenceDiameter = 1;
}

[CreateAssetMenu(menuName = "Game/Combat Effect Profile")]
public sealed class CombatEffectProfile : ScriptableObject
{
    public CombatEffectStage Muzzle = new();
    public CombatEffectStage GroundCircle = new();
    public CombatEffectStage Projectile = new();
    public CombatEffectStage Impact = new() { FitToTarget = true };
    public CombatEffectStage Area = new();
    public AudioClip AreaSound;
    [Min(0)] public float CastSeconds = .45f;
    [Min(0)] public float ImpactSeconds = .4f;
    [Min(.1f)] public float ProjectileSpeed = 20;
    public Vector2 FlightSeconds = new(.25f, .75f);
    public bool ScaleAreaToRadius = true;
}
