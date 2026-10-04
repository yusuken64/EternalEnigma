using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UnifiedPresentationAuthoring
{
    [MenuItem("Tools/Eternal Enigma/UI/Refresh Presentation Icons")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring prefabs.");
        const string profilePath="Assets/Resources/UI/GamePresentationProfile.asset";
        var profile=AssetDatabase.LoadAssetAtPath<GamePresentationProfile>(profilePath);
        if(profile==null){profile=ScriptableObject.CreateInstance<GamePresentationProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
        profile.AllyTemplate=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Dungeon/Ally.prefab");
        profile.CoinIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bamao/BamaoUIPack/Sprites/Shop/icon_coin.png");
        profile.BagIcon=RenderIcon(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/3D Props - Adorable Items/Adorable 3D Items/Prefabs/bag.prefab"),"Bag",false);
        profile.ItemIcons=new Sprite[11];
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Dungeon"}))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<DroppedItem>(AssetDatabase.GUIDToAssetPath(guid));
            if(prefab!=null && profile.ItemIcons[(int)prefab.DroppedItemVisual]==null)
                profile.ItemIcons[(int)prefab.DroppedItemVisual]=RenderIcon(prefab.gameObject,prefab.DroppedItemVisual.ToString(),true);
        }
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
        AssetDatabase.SaveAssets();
        Debug.Log("Updated presentation icons. UI layouts remain authored in prefabs.");
    }

    private static Sprite RenderIcon(GameObject prefab,string name,bool xy)
    {
        const string folder="Assets/Resources/UI/ItemIcons";
        Directory.CreateDirectory(folder);
        string path=folder+"/"+name+".png";
        if(File.Exists(path))return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var root=UnityEngine.Object.Instantiate(prefab);root.transform.position=new Vector3(10000,10000,10000);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        foreach(var script in root.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
        var cameraObject=new GameObject("Icon capture",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
        var target=new RenderTexture(128,128,24);var image=new Texture2D(128,128,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        var lightObject=new GameObject("Icon light",typeof(Light));var light=lightObject.GetComponent<Light>();
        var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;
        try
        {
            var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            var bounds=renderers[0].bounds;foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.orthographic=true;camera.orthographicSize=bounds.size.magnitude*.57f;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.transform.position=bounds.center+(xy?new Vector3(0,-.4f,-1):new Vector3(0,.3f,-1)).normalized*bounds.size.magnitude*3;
            camera.transform.LookAt(bounds.center,xy?Vector3.up:Vector3.up);camera.targetTexture=target;
            light.type=LightType.Directional;light.cullingMask=1<<31;light.intensity=1;light.shadows=LightShadows.None;
            light.transform.rotation=camera.transform.rotation;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.7f,.7f);
            camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
