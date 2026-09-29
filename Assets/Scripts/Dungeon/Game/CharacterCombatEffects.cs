using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterCombatEffects : MonoBehaviour
{
    public Transform CastSocket;
    public CombatEffectProfile Melee, Ranged, Confusion, Root, Steal;
    internal static CombatEffectProfile Attack(Character character, bool ranged)
    {
        var binding = character != null ? character.GetComponent<CharacterCombatEffects>() : null;
        var profile = ranged ? binding?.Ranged : binding?.Melee;
        return profile != null ? profile : ranged ? CombatVisualCatalog.Instance?.Ranged : CombatVisualCatalog.Instance?.Melee;
    }
}
