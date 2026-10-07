using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Art/Diorama Item Catalog")]
public sealed class DioramaItemCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public string Name,Source;
        public DroppedItem Prefab;
        public Sprite Icon;
        public Color Tint=Color.white;
    }
    [Serializable] public sealed class Prop {public string Id;public GameObject Prefab;}
    public Entry[] Items=Array.Empty<Entry>();
    public Prop[] Props=Array.Empty<Prop>();
    public static DioramaItemCatalog Load()=>Resources.Load<DioramaItemCatalog>("EnvironmentKit/DioramaItems");
    public void Apply(ItemDefinition definition)
    {
        var entry=Items.FirstOrDefault(e=>e.Name==definition.ItemName);if(entry==null)return;
        definition.DroppedItemPrefab=entry.Prefab;definition.Icon=entry.Icon;
    }
    public GameObject GetProp(string id)=>Props.FirstOrDefault(p=>p.Id==id)?.Prefab;
}
