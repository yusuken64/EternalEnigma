using UnityEngine;

/// <summary>Stable identity of an actor required by a floor objective.</summary>
[DisallowMultipleComponent]
public sealed class EncounterActor : MonoBehaviour
{
    public string Id;
    public bool Invulnerable;
    public bool Resolved;
    internal static bool Required(Character actor) => actor != null && actor.GetComponent<EncounterActor>() is { Resolved: false };
    internal static bool Protected(Character actor) => actor != null && actor.GetComponent<EncounterActor>() is { Invulnerable: true, Resolved: false };
}
