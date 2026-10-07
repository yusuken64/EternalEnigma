using System.Text;
using UnityEngine;

public static class DioramaArtIdentity
{
    public static string For(CampaignOverworld map)
    {
        var value=new StringBuilder("diorama-ground-v1|");
        Add(map.Template); Add(map.CosmeticKit); Add(PaintedGroundStyle.Load()); Add(DioramaCatalog.Load());
        foreach(var binding in map.LayerBindings) value.Append(binding.CoreLayer).Append('=').Append(binding.BlueprintLayer).Append('|');
        return Hash128.Compute(value.ToString()).ToString();
        void Add(Object asset)
        {
            if(asset==null) return;
            value.Append(JsonUtility.ToJson(asset));
#if UNITY_EDITOR
            string path=UnityEditor.AssetDatabase.GetAssetPath(asset);
            if(path.Length>0) value.Append(UnityEditor.AssetDatabase.GetAssetDependencyHash(path));
#endif
            if(asset is PaintedGroundStyle style)
                foreach(var material in new[]{style.DryGround,style.Water,style.Bridge})
                {
                    if(material==null) continue;
                    value.Append(material.GetInstanceID()).Append(material.shader.name);
                    if(material.HasProperty("_Color")) value.Append(material.color);
                    foreach(string name in PaintedGroundStyle.TextureNames)
                    {var property="_"+name;if(material.HasProperty(property))value.Append(material.GetTexture(property)?.GetInstanceID()??0);}
                    if(material.HasProperty("_MainTex")) value.Append(material.mainTexture?.GetInstanceID()??0);
                }
        }
    }
}
