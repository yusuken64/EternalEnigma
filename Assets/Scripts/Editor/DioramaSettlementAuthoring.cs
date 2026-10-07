using System;
using System.IO;
using System.Linq;
using TWC;
using UnityEditor;
using UnityEngine;

public static class DioramaSettlementAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Integrate Settlements")]
    public static void Build()
    {
        const string folder="Assets/Art/Diorama/Settlements";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();var catalog=DioramaCatalog.Load();
        var models=catalog.Models.Where(m=>!m.Id.StartsWith("Castle")).ToList();
        var sources=new[]{("CastleTower","towerSquare",3.3f),("CastleGate","wallNarrowGate",2.3f),("CastleFlag","flagBlue",2.8f)};
        var roofOnly=(ModelImporter)AssetImporter.GetAtPath("Assets/Art/KennyNL/Castle Kit/Models/towerSquareTopRoofHigh.fbx");
        if(roofOnly!=null&&roofOnly.isReadable){roofOnly.isReadable=false;roofOnly.SaveAndReimport();}
        foreach(var (id,file,height) in sources)
        {
            string source="Assets/Art/KennyNL/Castle Kit/Models/"+file+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(source);
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var root=new GameObject(id);var orientation=new GameObject("XY orientation");orientation.transform.SetParent(root.transform,false);
            orientation.transform.localRotation=Quaternion.Euler(0,0,id=="CastleTower"?0:90)*Quaternion.Euler(-90,0,0);
            var child=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source),orientation.transform);
            try
            {
                foreach(var collider in child.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                var renderers=child.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>{
                        string path=folder+"/"+original.name+"_Lit.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
                        material.mainTexture=original.mainTexture;material.color=original.HasProperty("_Color")?original.color:Color.white;
                        if(original.name.StartsWith("wall"))material.color*=.72f;
                        material.SetFloat("_Glossiness",.1f);material.SetFloat("_Metallic",0);EditorUtility.SetDirty(material);return material;
                    }).ToArray();
                }
                // Imported roots include an FBX axis correction and an authoring
                // offset. Preserve both, normalize around a separate XY wrapper.
                float factor=height/bounds.size.z;
                orientation.transform.localScale=Vector3.one*factor;
                orientation.transform.localPosition=new Vector3(-bounds.center.x,-bounds.center.y,-bounds.max.z)*factor;
                var tag=root.AddComponent<DioramaModelTag>();tag.Id=id;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+id+".prefab");
                int triangles=child.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
                models.Add(new DioramaModel{Id=id,Prefab=prefab,Height=height,Width=Mathf.Max(bounds.size.x,bounds.size.y)*height/bounds.size.z,Triangles=triangles});
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        catalog.Models=models.ToArray();EditorUtility.SetDirty(catalog);
        var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
        if(!template.mapBuildLayers.OfType<OverworldSettlementLayer>().Any())template.mapBuildLayers.Add(new OverworldSettlementLayer {
            guid=new Guid("74d21b9f-3876-46c0-9ca6-53c6fe98915b"),layerName=OverworldSettlementLayer.Layer,active=true,
            assignedGenerationLayerGuid=template.mapBlueprintLayers.First(l=>l.layerName==EternalEnigma.Core.World.OverworldLayers.Roads).guid});
        foreach(var input in OverworldGroundLayer.DefaultInputs())
            if(!template.mapBlueprintLayers.Any(l=>l.layerName==input.BlueprintLayer))template.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData(input.BlueprintLayer,true));
        var ground=template.mapBuildLayers.OfType<OverworldGroundLayer>().Single();ground.Inputs=OverworldGroundLayer.DefaultInputs();
        EditorUtility.SetDirty(template);AssetDatabase.SaveAssets();
        File.WriteAllText("Docs/Art/Previews/Diorama/Fit/SelectedCastleFBXs.txt",string.Join("\n",sources.Select(s=>"Assets/Art/KennyNL/Castle Kit/Models/"+s.Item2+".fbx"))+"\nOnly Read/Write enabled; own collider-free lit adapters.\n");
        Debug.Log("Diorama settlements: shared facade cottages, Kenney landmarks, TWC road signs.");
        Capture();
    }
    static void Capture()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var paths=new[]{DioramaFitAuthoring.HeroPath,"Assets/Art/Diorama/Settlements/CastleGate.prefab","Assets/Art/Diorama/Settlements/CastleTower.prefab","Assets/Art/Diorama/Settlements/CastleFlag.prefab"};
            float x=0;var bounds=new Bounds();bool first=true;
            foreach(string path in paths)
            {
                var obj=DioramaFitAuthoring.Spawn(path,scene);obj.GetComponent<TownAlly>()?.SetFacing(Facing.Down);
                var b=DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(obj));
                obj.transform.position+=new Vector3(x-b.min.x,-b.center.y,-b.max.z);x+=b.size.x+.6f;
                b=DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(obj));if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
            }
            var camera=new GameObject("Landmark fit").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
            camera.scene=scene;camera.enabled=false;camera.orthographic=true;camera.orthographicSize=Mathf.Max(3,bounds.size.x*.4f);
            camera.transform.position=bounds.center+new Vector3(0,-14,-20);camera.transform.LookAt(bounds.center,Vector3.up);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.32f,.39f,.27f);
            var light=new GameObject("Fit daylight").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);
            light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=camera.transform.rotation;
            DioramaFitAuthoring.Save(camera,"Docs/Art/Previews/Diorama/Fit/Landmarks.png",1280,600);
        }
        finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
    }
}
