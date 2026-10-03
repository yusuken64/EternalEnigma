#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

internal static class BiomeDecorationCapture
{
    public static IEnumerator Audit(GameObject world,string name)
    {
        var root=world.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Biome decorations");Assert.That(root,Is.Not.Null);
        var source=Camera.main;Assert.That(source,Is.Not.Null);
        yield return null;yield return null;
        foreach(var label in root.GetComponentsInChildren<TextMeshPro>()) {
            var signLabel=label.GetComponent<BiomeSignLabel>();
            if(signLabel!=null&&Common.Instance?.CampaignContext!=null)
                Assert.That(signLabel.DisplayName,Is.EqualTo(Common.Instance.CampaignContext.GetTownDisplayName(label.name.Substring("Destination ".Length))));
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.lineCount,Is.LessThanOrEqualTo(2),label.text);
            Assert.That(label.textBounds.size.x,Is.LessThanOrEqualTo(label.rectTransform.rect.width+.01f),label.text);
            Assert.That(label.textBounds.size.y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+.01f),label.text);
        }
        Directory.CreateDirectory("Docs/Art/Verification");Directory.CreateDirectory("Docs/Art/Previews");
        string file="Docs/Art/Verification/BiomeRender_"+name+".csv";
        File.WriteAllText(file,"enabled,samples,median_camera_render_ms,decoration_triangles,decoration_meshes\n");
        var cameraObject=new GameObject("Biome audit camera");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(source);
        camera.transform.rotation=source.transform.rotation;
        var sign=root.GetComponentInChildren<BiomeSignLabel>();var effect=root.GetComponentsInChildren<BiomeDecorationEffect>().FirstOrDefault(e=>e.FloorVisible);
        var center=sign!=null?sign.transform.parent.position:effect!=null?effect.transform.position:Game.Instance!=null?Game.Instance.PlayerController.ControlledAlly.transform.position:root.position;
        camera.transform.position=center-camera.transform.forward*50;camera.orthographic=true;camera.orthographicSize=20;
        source.tag="Untagged";camera.tag="MainCamera";
        var target=RenderTexture.GetTemporary(1600,1200,24);
        try {
            camera.targetTexture=target;
            foreach(bool enabled in new[]{false,true}) {
                root.gameObject.SetActive(enabled);yield return null;yield return null;
                for(int warm=0;warm<5;warm++)camera.Render();
                var samples=new double[30];
                for(int i=0;i<samples.Length;i++){var clock=System.Diagnostics.Stopwatch.StartNew();camera.Render();clock.Stop();samples[i]=clock.Elapsed.TotalMilliseconds;}
                Array.Sort(samples);
                var owners=root.GetComponentsInChildren<EnvironmentMeshOwner>(true);
                File.AppendAllText(file,$"{enabled},30,{samples[15].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)},{(enabled?owners.Sum(o=>o.TriangleCount):0)},{(enabled?owners.Sum(o=>o.Meshes.Count):0)}\n");
            }
            camera.orthographicSize=5;yield return null;yield return null;camera.Render();
            var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try {image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes("Docs/Art/Previews/BiomeGameplay_"+name+".png",image.EncodeToPNG());}
            finally{UnityEngine.Object.Destroy(image);RenderTexture.active=active;}
        }
        finally {root.gameObject.SetActive(true);source.tag="MainCamera";camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(cameraObject);}
    }
}
#endif
