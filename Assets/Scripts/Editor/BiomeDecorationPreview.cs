using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BiomeDecorationPreview
{
    [MenuItem("Tools/Eternal Enigma/Art/Build Biome Decoration Gallery")]
    public static void Build()
    {
        var catalog=BiomeDecorationCatalog.Load();if(catalog==null)throw new InvalidOperationException("Import decorations first.");
        var root=new GameObject("Biome decoration gallery");
        try {
            foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) {
                float y=(int)biome*3;
                Label(root.transform,biome.ToString(),new Vector3(-1,y,-.3f),new Vector2(2.8f,.7f),5);
                for(int k=0;k<5;k++) {
                    var entry=catalog.Get(biome,(BiomeDecorationKind)k);
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab);obj.transform.SetParent(root.transform,false);obj.transform.localPosition=new Vector3(2+k*2.5f,y,0);obj.transform.localScale=Vector3.one*1.8f;
                    if(k==4)for(int panel=0;panel<3;panel++) {
                        var arrow=(GameObject)PrefabUtility.InstantiatePrefab(catalog.Get(biome,BiomeDecorationKind.SignPanel).Prefab);arrow.transform.SetParent(obj.transform,false);arrow.transform.localPosition=new Vector3(0,-.03f,-1.05f+panel*.23f);
                    }
                }
            }
            string path="Assets/Resources/BiomeDecorations/Gallery.prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
            Capture(root,new Vector3(6,10,-.5f),14,"Docs/Art/Previews/BiomeDecorations.png");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/EnvironmentPlayground.unity");bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/EnvironmentPlayground.unity",OpenSceneMode.Additive);
            try {
                var playground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<EnvironmentPlayground>(true)).Single();
                if(playground.DecorationGallery!=null)UnityEngine.Object.DestroyImmediate(playground.DecorationGallery.gameObject);
                var gallery=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);gallery.SetActive(false);playground.DecorationGallery=gallery.transform;
                EditorUtility.SetDirty(playground);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
            AssetDatabase.SaveAssets();Debug.Log("Saved all-biome gallery and Environment Playground binding; key 6 opens it.");
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    public static void Capture(GameObject root,Vector3 center,float zoom,string path)
    {
        var cameraObject=new GameObject("Decoration capture camera");var camera=cameraObject.AddComponent<Camera>();var target=RenderTexture.GetTemporary(1600,1200,24);
        var lightObject=new GameObject("Temporary preview key light");var key=lightObject.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.cullingMask=1<<30;key.transform.rotation=Quaternion.Euler(35,-25,0);
        var ambient=RenderSettings.ambientLight;var ambientMode=RenderSettings.ambientMode;
        var active=RenderTexture.active;var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);
        var transforms=root.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        try {
            foreach(var t in transforms)t.gameObject.layer=30;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.18f);camera.orthographic=true;camera.orthographicSize=zoom;
            camera.transform.position=center+new Vector3(0,-23,-28);camera.transform.LookAt(center,Vector3.up);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally {for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=ambientMode;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);UnityEngine.Object.DestroyImmediate(image);}
    }
    static void Label(Transform parent,string value,Vector3 position,Vector2 size,float font)
    {
        var text=new GameObject(value).AddComponent<TextMeshPro>();text.transform.SetParent(parent,false);text.transform.localPosition=position;text.text=value;text.fontSize=font;text.rectTransform.sizeDelta=size;text.alignment=TextAlignmentOptions.Center;
    }
}
