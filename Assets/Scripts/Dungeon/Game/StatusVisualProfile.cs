using UnityEngine;

[CreateAssetMenu(menuName = "Game/Status Visual Profile")]
public sealed class StatusVisualProfile : ScriptableObject
{
    public CombatEffectStage Aura = new();
    public int Priority = 10;
    public bool HasLoop;
    public AnimatedAction Loop;
    public Sprite Icon;
    public bool Overhead;
}
