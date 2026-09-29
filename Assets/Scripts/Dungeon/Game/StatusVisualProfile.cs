using UnityEngine;

[CreateAssetMenu(menuName = "Game/Status Visual Profile")]
public sealed class StatusVisualProfile : ScriptableObject
{
    public CombatEffectStage Aura = new();
    public int Priority = 10;
}
