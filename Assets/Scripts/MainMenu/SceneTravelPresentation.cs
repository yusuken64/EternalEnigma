using System;
using System.Collections;
using UnityEngine;

public enum SceneTravelKind
{
    TownToDungeon,
    DungeonToTown,
    TownToOverworld,
    OverworldToTown,
    HouseToBookDungeon,
    BookDungeonToHouse
}

/// <summary>A scene-independent description retained until the destination is ready.</summary>
public sealed class SceneTravelPresentation
{
    public const float DepartureSeconds = .45f;
    public const float ArrivalSeconds = .65f;
    public const float NearScale = .15f;
    public const float WideScale = 2.4f;
    public readonly SceneTravelKind Kind;
    public readonly Vector3 SourceAnchor;
    public readonly string DestinationIdentity;
    public readonly Func<SceneTravelArrival> ResolveArrival;
    public Camera SourceCamera;
    public Func<IEnumerator> PrepareDeparture;

    public SceneTravelPresentation(SceneTravelKind kind, Vector3 sourceAnchor,
        string destinationIdentity = null, Func<SceneTravelArrival> resolveArrival = null)
    {
        Kind = kind;
        SourceAnchor = sourceAnchor;
        DestinationIdentity = destinationIdentity;
        ResolveArrival = resolveArrival;
        SourceCamera = Camera.main;
    }

    public bool Inward => Kind == SceneTravelKind.TownToDungeon || Kind == SceneTravelKind.OverworldToTown ||
        Kind == SceneTravelKind.HouseToBookDungeon;
    public float DepartureScale => Inward ? NearScale : WideScale;
    public float ArrivalScale => Inward ? WideScale : NearScale;

    // Explicit endpoints keep the camera settled precisely at its gameplay pose.
    public static float DepartureEase(float t) => t <= 0 ? 0 : t >= 1 ? 1 : Mathf.Pow(2, 10 * (t - 1));
    public static float ArrivalEase(float t) => t <= 0 ? 0 : t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);
}

public readonly struct SceneTravelArrival
{
    public readonly Camera Camera;
    public readonly Vector3 Anchor;
    public SceneTravelArrival(Camera camera, Vector3 anchor) { Camera = camera; Anchor = anchor; }
}
