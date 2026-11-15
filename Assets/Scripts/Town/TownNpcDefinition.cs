using UnityEngine;

[CreateAssetMenu(menuName="Game/Town/NPC")]
public sealed class TownNpcDefinition : ScriptableObject
{
    public string Id, DisplayName, Greeting;
    public TownNpcDialogueTopic[] DialogueTopics = System.Array.Empty<TownNpcDialogueTopic>();
    public GameObject Prefab;
    public Sprite Portrait;
}

[System.Serializable]
public sealed class TownNpcDialogueTopic
{
    public string Topic;
    [TextArea(2, 5)] public string Answer;
}
