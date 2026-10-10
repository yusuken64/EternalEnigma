#if UNITY_EDITOR
using System;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OverworldStyleAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Art/Create Overworld Style Profile")]
    public static void CreateDefault()
    {
        const string path="Assets/Resources/EnvironmentKit/OverworldStyle.asset";
        const string scenePath="Assets/Scenes/Overworld.unity";
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        try
        {
            var map=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CampaignOverworld>(true)).Single();
            var profile=AssetDatabase.LoadAssetAtPath<OverworldStyleProfile>(path);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<OverworldStyleProfile>();
                profile.Template=map.Template;profile.CosmeticKit=map.CosmeticKit;
                profile.GroundStyle=PaintedGroundStyle.Load();
                var grid=OverworldGridGenerator.Generate(CampaignGenerator.Generate(42));
                foreach(var layer in map.Template.mapBlueprintLayers.Where(l=>l.active))
                {
                    string core=map.LayerBindings.FirstOrDefault(b=>b.BlueprintLayer==layer.layerName)?.CoreLayer;
                    if(core==null && grid.Layers.ContainsKey(layer.layerName))core=layer.layerName;
                    core??=layer.layerName switch
                    {
                        SmartEnvironmentMasks.Mountains=>OverworldLayers.Mountains,
                        SmartEnvironmentMasks.Roads=>OverworldLayers.Roads,
                        SmartEnvironmentMasks.Walls=>OverworldLayers.TownFootprints,
                        SmartEnvironmentMasks.Houses=>OverworldLayers.Towns,
                        SmartEnvironmentMasks.Coast=>OverworldLayers.Water,
                        _=>null
                    };
                    profile.Layers.Add(new OverworldStyleBinding
                    {
                        BlueprintGuid=layer.guid.ToString(),CoreLayer=core,
                        Role=SmartEnvironmentMasks.IsDerived(layer.layerName)?OverworldLayerRole.DerivedTerrain:
                            layer.layerName==OverworldCosmetics.Layer?OverworldLayerRole.PlannedCosmetic:
                            core!=null?OverworldLayerRole.CoreMask:OverworldLayerRole.DerivedTerrain,
                        AllowEmpty=true
                    });
                }
                // Include the complete authored dependency graph in player cache identity.
                profile.Dependencies=AssetDatabase.GetDependencies(new[] {AssetDatabase.GetAssetPath(map.Template),AssetDatabase.GetAssetPath(map.CosmeticKit),AssetDatabase.GetAssetPath(profile.GroundStyle)})
                    .Select(AssetDatabase.LoadMainAssetAtPath).Where(a=>a!=null && !(a is MonoScript)).ToArray();
                profile.Validate(grid,map.Template);
                AssetDatabase.CreateAsset(profile,path);
            }
            foreach(var binding in profile.Layers)
            {
                var layer=map.Template.mapBlueprintLayers.Single(l=>l.guid==Guid.Parse(binding.BlueprintGuid));
                if(new[] {SmartEnvironmentMasks.Mountains,SmartEnvironmentMasks.Roads,SmartEnvironmentMasks.Walls,SmartEnvironmentMasks.Houses,SmartEnvironmentMasks.Coast}.Contains(layer.layerName))
                    binding.Projection=layer.layerName;
            }
            EditorUtility.SetDirty(profile);
            map.StyleProfile=profile;
            EditorUtility.SetDirty(map);EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Authored composed overworld style: "+path);
        }
        finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
#endif
