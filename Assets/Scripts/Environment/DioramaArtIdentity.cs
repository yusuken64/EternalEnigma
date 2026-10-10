using System.Text;
using UnityEngine;

public static class DioramaArtIdentity
{
    public static string For(CampaignOverworld map)
    {
        var value=new StringBuilder("diorama-style-v2|");
        Add(map.EffectiveTemplate); Add(map.EffectiveKit); Add(PaintedGroundStyle.Load()); Add(DioramaCatalog.Load());
        if(map.StyleProfile!=null)
            foreach(var profile in map.StyleProfile.Composition())
            {
                Add(profile);Add(profile.Template);Add(profile.CosmeticKit);Add(profile.GroundStyle);
                foreach(var dependency in profile.Dependencies)Add(dependency);
            }
        foreach(var binding in map.LayerBindings) value.Append(binding.CoreLayer).Append('=').Append(binding.BlueprintLayer).Append('|');
        return Hash128.Compute(value.ToString()).ToString();
        void Add(Object asset)
        {
            if(asset==null) return;
            // JsonUtility accepts script objects, but not native assets such as meshes,
            // textures, shaders or prefabs. Their identity is local to this terrain cache.
            value.Append(asset.GetType().FullName).Append(':').Append(asset.GetInstanceID()).Append('|');
            if(asset is ScriptableObject || asset is MonoBehaviour) value.Append(JsonUtility.ToJson(asset));
            if(asset is TWC.TileWorldCreatorAsset terrain)
            {
                foreach(var layer in terrain.mapBlueprintLayers)
                {
                    value.Append(layer.guid).Append(layer.layerName).Append(layer.active);
                    foreach(var step in layer.stack)
                    {
                        if(step.action==null){value.Append("missing-action");continue;}
                        value.Append(step.action.GetType().FullName).Append(JsonUtility.ToJson(step.action));
                        foreach(var field in step.action.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic))
                            if(field.FieldType==typeof(System.Guid))value.Append(field.Name).Append(field.GetValue(step.action));
                    }
                }
                foreach(var layer in terrain.mapBuildLayers)value.Append(layer.GetType().FullName).Append(JsonUtility.ToJson(layer));
            }
            if(asset is Material materialAsset)
            {
                value.Append(materialAsset.shader?.name);
                if(materialAsset.HasProperty("_MainTex"))value.Append(materialAsset.mainTexture?.GetInstanceID());
                if(materialAsset.HasProperty("_Color"))value.Append(materialAsset.color);
            }
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
