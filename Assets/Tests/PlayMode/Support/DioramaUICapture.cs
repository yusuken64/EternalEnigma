#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Captures UI at a fixed output size without depending on Game view visibility.</summary>
public static class DioramaUICapture
{
    public static IEnumerator Save(Canvas canvas,string name)
    {
        const string folder="Docs/Art/Previews/Diorama/UI";
        Directory.CreateDirectory(folder);
        var previousMode=canvas.renderMode;var previousCamera=canvas.worldCamera;float previousDistance=canvas.planeDistance;
        var objects=canvas.GetComponentsInChildren<Transform>(true).Select(t=>(t.gameObject,t.gameObject.layer)).ToArray();
        var camera=new GameObject("Diorama UI capture").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.19f);camera.nearClipPlane=.01f;
        var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var previousTarget=RenderTexture.active;
        try
        {
            foreach(var entry in objects)entry.gameObject.layer=30;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;
            camera.aspect=1280f/720f;
            yield return null;Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())text.ForceMeshUpdate();
            camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            File.WriteAllBytes(folder+"/"+name+".png",texture.EncodeToPNG());
            var details=new System.Text.StringBuilder();
            foreach(var graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if(graphic.name!="Result icon")continue;
                var corners=new Vector3[4];graphic.rectTransform.GetWorldCorners(corners);
                details.AppendLine(graphic.name+" "+string.Join(";",corners.Select(p=>camera.WorldToViewportPoint(p).ToString("F3")))+" sprite="+graphic.sprite.rect);
            }
            if(details.Length>0)File.WriteAllText(folder+"/"+name+".txt",details.ToString());
        }
        finally
        {
            canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;canvas.planeDistance=previousDistance;
            foreach(var entry in objects)if(entry.gameObject!=null)entry.gameObject.layer=entry.layer;
            RenderTexture.active=previousTarget;camera.targetTexture=null;Object.Destroy(target);Object.Destroy(texture);Object.Destroy(camera.gameObject);
        }
        yield return null;
    }
}
#endif
