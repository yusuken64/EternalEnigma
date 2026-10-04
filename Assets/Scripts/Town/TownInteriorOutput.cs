using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Generation audit metadata; lifetime follows the generated world, never the save.</summary>
public sealed class TownInteriorOutput : MonoBehaviour
{
    public int SupportingFaces, InteriorFaces, WallMountCount;
    public List<TownBirdPerch> Birds = new();
}
[Serializable] public sealed class TownBirdPerch
{
    public string Variant;
    public Vector3 Position;
    public bool Interior;
}
