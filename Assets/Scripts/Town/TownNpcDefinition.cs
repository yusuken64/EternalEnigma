using UnityEngine;

[CreateAssetMenu(menuName="Game/Town/NPC")]
public sealed class TownNpcDefinition : ScriptableObject
{
    public string Id, DisplayName, Greeting;
    public GameObject Prefab;
    public Sprite Portrait;
}
