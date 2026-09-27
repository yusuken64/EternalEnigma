using System;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

[Serializable]
public sealed class TreeModelChoice
{
    public GameObject Prefab;
    [Min(0)] public int Weight=1;
    public OverworldBiome[] Biomes=Array.Empty<OverworldBiome>();
    public bool Matches(OverworldBiome biome) => Prefab!=null && Weight>0 && (Biomes.Length==0 || Biomes.Contains(biome));
}

[CreateAssetMenu(menuName="Game/Art/TWC Tree Model Picker")]
public sealed class TreeModelPicker : ScriptableObject
{
    public TreeModelChoice[] Models=Array.Empty<TreeModelChoice>();
    public string Pick(OverworldBiome biome,uint hash)
    {
        int total=0;foreach(var choice in Models) if(choice.Matches(biome)) total+=choice.Weight;
        if(total==0) return biome==OverworldBiome.Desert?"Cactus":biome==OverworldBiome.Tundra?"Pine":biome==OverworldBiome.Marsh||biome==OverworldBiome.Volcanic?"DeadTree":"Tree";
        int ticket=(int)(hash%(uint)total);
        foreach(var choice in Models) if(choice.Matches(biome)) {ticket-=choice.Weight;if(ticket<0)return choice.Prefab.GetComponent<BiomeModel>().ModelId;}
        return "Tree";
    }
#if UNITY_EDITOR
    public static TreeModelPicker Draw(TreeModelPicker picker)
    {
        picker=(TreeModelPicker)UnityEditor.EditorGUILayout.ObjectField("Tree model picker",picker,typeof(TreeModelPicker),false);
        if(picker!=null)
        {
            var serialized=new UnityEditor.SerializedObject(picker);serialized.Update();
            UnityEditor.EditorGUILayout.PropertyField(serialized.FindProperty("Models"),new GUIContent("Weighted tree models"),true);
            serialized.ApplyModifiedProperties();
        }
        return picker;
    }
#endif
}
