using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DioramaItemCapture
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Icon Sheets")]
    public static void IconSheets()
    {
        const string folder="Docs/Art/Previews/Diorama/Items";
        var paths=Directory.GetFiles("Assets/Resources/UI/ItemIcons","*.png").OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var labels=new System.Text.StringBuilder("page,cell,name\n");
        for(int start=0;start<paths.Length;start+=48)
        {
            var sheet=new Texture2D(1024,768,TextureFormat.RGB24,false);
            var background=new Color(.16f,.19f,.17f);sheet.SetPixels(Enumerable.Repeat(background,1024*768).ToArray());
            for(int i=0;i<48&&start+i<paths.Length;i++)
            {
                var icon=new Texture2D(2,2);icon.LoadImage(File.ReadAllBytes(paths[start+i]));
                var pixels=icon.GetPixels();for(int p=0;p<pixels.Length;p++)pixels[p]=Color.Lerp(background,pixels[p],pixels[p].a);
                sheet.SetPixels(i%8*128,(5-i/8)*128,128,128,pixels);
                labels.AppendLine($"{start/48+1},{i+1},{Path.GetFileNameWithoutExtension(paths[start+i])}");
                UnityEngine.Object.DestroyImmediate(icon);
            }
            sheet.Apply();File.WriteAllBytes(folder+$"/IconSheet_{start/48+1:00}.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
        }
        File.WriteAllText(folder+"/IconSheets.csv",labels.ToString());
    }
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Items")]
    public static void Capture()
    {
        const string folder="Docs/Art/Previews/Diorama/Items";Directory.CreateDirectory(folder);
        var catalog=DioramaItemCatalog.Load();var original=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;
        try
        {
            var camera=new GameObject("Floor camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.80f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.21f,.25f,.21f);camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.cullingMask=1<<31;
            camera.transform.position=new Vector3(0,-1.4f,-2.5f);camera.transform.LookAt(new Vector3(0,0,-.24f),Vector3.up);
            var light=new GameObject("Floor light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=camera.transform.rotation;light.cullingMask=1<<31;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
            var entries=catalog.Items.Select(e=>(e.Name,e.Prefab.gameObject,e.Icon)).Concat(catalog.Props.Select(p=>(p.Id,p.Prefab,(Sprite)null))).ToArray();
            Texture2D sheet=null;int page=0;
            var labels=new System.Text.StringBuilder("page,cell,name\n");
            for(int i=0;i<entries.Length;i++)
            {
                if(i%16==0){sheet=new Texture2D(1024,1024,TextureFormat.RGB24,false);sheet.SetPixels(Enumerable.Repeat(new Color(.08f,.09f,.1f),1024*1024).ToArray());page++;}
                var (name,prefab,icon)=entries[i];var obj=UnityEngine.Object.Instantiate(prefab);
                var bounds=DioramaItemAuthoring.Bounds(obj);obj.transform.position-=new Vector3(bounds.center.x,bounds.center.y,0);
                foreach(var transform in obj.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=31;
                foreach(var behaviour in obj.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                var rt=RenderTexture.GetTemporary(256,256,24);var previous=RenderTexture.active;
                var image=new Texture2D(256,256,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                    File.WriteAllBytes(folder+"/Floor_"+DioramaItemAuthoring.Safe(name)+".png",image.EncodeToPNG());
                    int col=i%4,row=3-(i%16)/4;sheet.SetPixels(col*256,row*256,256,256,image.GetPixels());
                    labels.AppendLine($"{page},{i%16+1},{name}");
                }
                finally {camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(obj);}
                if(i%16==15||i==entries.Length-1){sheet.Apply();File.WriteAllBytes(folder+$"/FloorSheet_{page:00}.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);}
            }
            File.WriteAllText(folder+"/FloorSheets.csv",labels.ToString());
            string html="<!doctype html><meta charset='utf-8'><title>Diorama item review</title><style>body{background:#1d231e;color:#eee;font:15px system-ui}main{display:grid;grid-template-columns:repeat(4,1fr);gap:16px}article{background:#30382f;padding:10px}img{width:45%;image-rendering:auto}h2{font-size:14px}</style><h1>Final floor scale and menu icon</h1><main>";
            foreach(var e in catalog.Items)html+=$"<article><h2>{System.Net.WebUtility.HtmlEncode(e.Name)}</h2><img src='Floor_{DioramaItemAuthoring.Safe(e.Name)}.png'><img src='../../../../../{AssetDatabase.GetAssetPath(e.Icon)}'></article>";
            File.WriteAllText(folder+"/Review.html",html+"</main>");
        }
        finally {RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
        Debug.Log("Diorama floor-item capture complete.");
    }
}
