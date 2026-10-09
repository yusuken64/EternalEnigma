using System;
using UnityEngine;

public enum DungeonPickupSize { Ordinary, Elongated, Chest }

/// <summary>Individual authoring adjustments, baked into dungeon floor visual children only.</summary>
[CreateAssetMenu(menuName = "Game/Art/Dungeon Pickup Presentation")]
public sealed class DungeonPickupPresentation : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public string Name, Source;
        public GameObject Prefab;
        public DungeonPickupSize Size;
        public Vector3 Euler, Shape = Vector3.one;
        [Range(.3f,.7f)] public float TargetRatio = .43f;
        public Vector2 CellCenter = Vector2.one;
        public Vector3 ParentScale = Vector3.one;
        public float BakedScale, ProjectedRatio, WorldHeightRatio;
    }
    public string HeroPrefab, HeroRigPrefab;
    public float HeroHeight, HeroScreenHeight;
    public Vector3 CameraOffset;
    public float OrthographicSize;
    public Entry[] Items = Array.Empty<Entry>();
}
