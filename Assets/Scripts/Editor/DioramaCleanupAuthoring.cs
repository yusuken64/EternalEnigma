using System;
using System.IO;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DioramaCleanupAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Prepare Cleanup")]
    public static void Build()
    {
        var own=AssetDatabase.LoadAssetAtPath<TileWorldCreator4TilesPreset>("Assets/Art/EnvironmentKit/SmartTiles/Road.asset");
        foreach(string path in new[]{"Assets/Overworld/CampaignTerrain.asset","Assets/TileWorldCreator/VillageLSystemAsset.asset","Assets/Prefabs/Dungeon/DungeonAsset.asset","Assets/Prefabs/Dungeon/DungeonThroneAsset.asset"})
        {
            var asset=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(path);
            foreach(var layer in asset.mapBuildLayers.OfType<InstantiateTiles>())
                foreach(var tile in layer.tiles??new())
                    if(tile.preset!=null&&AssetDatabase.GetAssetPath(tile.preset).StartsWith("Assets/TileWorldCreator/Tiles/"))tile.preset=own;
            // Inactive sample object layers cannot contribute to generated worlds.
            asset.mapBuildLayers.RemoveAll(l=>!l.active&&!(l is EnvironmentSmartTileLayer));
            foreach(var objects in asset.mapBuildLayers.OfType<InstantiateObjects>())
            {
                objects.prefab=OwnPrefab(objects.prefab);objects.childPrefab=OwnPrefab(objects.childPrefab);
                if(objects.randomChildPrefabs!=null)objects.randomChildPrefabs=objects.randomChildPrefabs.Select(OwnPrefab).ToList();
            }
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/EnvironmentPlayground.unity");
            var playground=Object.FindFirstObjectByType<EnvironmentPlayground>();
            foreach(var root in playground.Gallery.GetComponentsInChildren<Transform>(true).Select(t=>PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject)).Where(o=>o!=null).Distinct().ToArray())
                if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root).StartsWith("Assets/Art/EnvironmentKit/Prefabs/"))
                    PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/DungeonScene.unity");
            foreach(var creator in scene.GetRootGameObjects().Where(r=>r!=null).SelectMany(r=>r.GetComponentsInChildren<TileWorldCreator>(true)).ToArray())
                if(creator.worldObject!=null){Object.DestroyImmediate(creator.worldObject);creator.worldObject=null;}
            EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            const string folder="Assets/Art/MainMenu/Meshes";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(var filter in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)))
            {
                var source=filter.sharedMesh;string path=AssetDatabase.GetAssetPath(source);
                if(!path.StartsWith("Assets/TileWorldCreator/Tiles/"))continue;
                string target=folder+"/"+AssetDatabase.AssetPathToGUID(path)+"_"+DioramaItemAuthoring.Safe(source.name)+".asset";
                var copy=AssetDatabase.LoadAssetAtPath<Mesh>(target);if(copy==null){copy=Object.Instantiate(source);AssetDatabase.CreateAsset(copy,target);}
                filter.sharedMesh=copy;
            }
            foreach(var animator in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Animator>(true)))
                if(animator.runtimeAnimatorController==null&&AssetDatabase.GetAssetPath(animator.avatar).StartsWith("Assets/TileWorldCreator/Tiles/"))animator.avatar=null;
            EditorSceneManager.SaveScene(scene);
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        AssetDatabase.SaveAssets();
        Debug.Log("Cleanup dependencies migrated; rerun the read-only GUID/path audit before deleting packs.");
    }

    [MenuItem("Tools/Eternal Enigma/Diorama/Probe Settlement")]
    public static void Probe()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EnvironmentPlayground.unity");
            var playground=Object.FindFirstObjectByType<EnvironmentPlayground>();playground.Seed=12345;playground.ShowOverworld();
            var creator=playground.Overworld.GetComponent<TileWorldCreator>();var grid=playground.Overworld.CurrentGrid;
            var text=new System.Text.StringBuilder();
            foreach(var layer in creator.twcAsset.mapBuildLayers)text.AppendLine($"{layer.layerName} {layer.GetType().Name} active={layer.active}");
            foreach(var town in grid.TownFootprints)
            {
                text.AppendLine("TOWN "+town.LocationId+" "+town.Entrance);
                var cell=town.Cell(0,1);Vector3 p=new Vector3(cell.X+.5f,cell.Y+.5f,0)*creator.twcAsset.cellSize;
                foreach(var r in creator.worldObject.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.bounds.min.x<=p.x&&r.bounds.max.x>=p.x&&r.bounds.min.y<=p.y&&r.bounds.max.y>=p.y))
                    text.AppendLine(r.transform.parent.name+"/"+r.name+" "+r.bounds+" "+string.Join(",",r.sharedMaterials.Select(m=>m==null?"NULL":m.name+" "+m.shader.name+" "+(m.HasProperty("_Color")?m.color.ToString():""))));
            }
            var entrance=grid.TownFootprints.Select(t=>t.Entrance).OrderBy(p=>Mathf.Abs(p.X-grid.Width*.5f)+Mathf.Abs(p.Y-grid.Height*.5f)).First();
            var center=new Vector3(entrance.X+.5f,entrance.Y+.5f,0)*creator.twcAsset.cellSize;
            var camera=playground.ViewCamera;camera.transform.position=center+new Vector3(0,-12,-35).normalized*40;
            camera.transform.LookAt(center,Vector3.up);camera.orthographicSize=5;camera.aspect=1.6f;
            var ray=camera.ViewportPointToRay(new Vector3(.42f,.71f));
            var intersect=typeof(HandleUtility).GetMethod("IntersectRayMesh",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.IntersectRay(ray)))
            {
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
                object[] args={ray,mesh,renderer.localToWorldMatrix,null};
                if(intersect!=null&&(bool)intersect.Invoke(null,args))
                    text.AppendLine($"RAY {((RaycastHit)args[3]).distance} {renderer.transform.parent?.name}/{renderer.name} mesh={mesh.name} {string.Join(",",renderer.sharedMaterials.Select(m=>m==null?"NULL":m.name+" "+AssetDatabase.GetAssetPath(m)))}");
            }
            File.WriteAllText("Temp/SettlementProbe.txt",text.ToString());
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }

    static GameObject OwnPrefab(GameObject source)
    {
        if(source==null||!AssetDatabase.GetAssetPath(source).StartsWith("Assets/TileWorldCreator/Tiles/"))return source;
        const string folder="Assets/Art/Diorama/Legacy";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        string path=folder+"/"+source.name+".prefab";var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(saved!=null)return saved;
        var root=Object.Instantiate(source);
        try
        {
            foreach(var c in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string meshPath=folder+"/"+DioramaItemAuthoring.Safe(f.sharedMesh.name)+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(mesh==null){mesh=Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(mesh,meshPath);}f.sharedMesh=mesh;
            }
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>{
                string matPath=folder+"/"+m.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null){mat=new Material(Shader.Find("Standard"));mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PaintedEnvironment/Masonry.png");mat.SetFloat("_Glossiness",.1f);AssetDatabase.CreateAsset(mat,matPath);}return mat;
            }).ToArray();
            foreach(var a in root.GetComponentsInChildren<Animator>(true))if(a.runtimeAnimatorController==null)a.avatar=null;
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{Object.DestroyImmediate(root);}
    }
}
