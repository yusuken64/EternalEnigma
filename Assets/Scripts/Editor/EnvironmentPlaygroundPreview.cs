using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EnvironmentPlaygroundPreview
{
    private static EnvironmentPlayground Playground => Object.FindFirstObjectByType<EnvironmentPlayground>();
    [MenuItem("Tools/Eternal Enigma/Art/Preview Gallery")]
    public static void Gallery() { Playground.ShowGallery(); Capture(); }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Town")]
    public static void Town() { Playground.ShowTown(); }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Town Facade")]
    public static void TownFacade()
    {
        var p=Playground;p.ShowTown();CoreLayoutCache.TryGetTown(p.TownCreator,out var plan);
        var houses=plan.Layers[EternalEnigma.Core.World.TownLayers.Houses];
        var cell=Enumerable.Range(0,plan.Width*plan.Height).Select(i=>new Vector2Int(i%plan.Width,i/plan.Width)).First(v=>houses[v.x,v.y]);
        var center=new Vector3(cell.x+1.4f,cell.y+.5f,0)*p.TownCreator.twcAsset.cellSize;
        p.ViewCamera.orthographicSize=4;p.ViewCamera.transform.position=center+new Vector3(0,-12,-16);p.ViewCamera.transform.LookAt(center,Vector3.up);
    }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Mountain Tops")]
    public static void MountainTops()
    {
        var p=Playground;p.ShowOverworld();var creator=p.Overworld.GetComponent<TWC.TileWorldCreator>();
        var layer=creator.twcAsset.mapBlueprintLayers.First(l=>l.layerName==SmartEnvironmentMasks.Summits);
        var mask=creator.GetMapOutputFromBlueprintLayer(layer.guid);
        var cell=Enumerable.Range(0,mask.Length).Select(i=>new Vector2Int(i%mask.GetLength(0),i/mask.GetLength(0))).First(v=>mask[v.x,v.y]);
        var center=new Vector3(cell.x+.5f,cell.y+.5f,-1.23f)*creator.twcAsset.cellSize;
        p.ViewCamera.orthographicSize=10;p.ViewCamera.transform.position=center+new Vector3(0,-12,-25);p.ViewCamera.transform.LookAt(center,Vector3.up);
    }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Overworld")]
    public static void World() { Playground.ShowOverworld(); }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Smart Rules")]
    public static void Rules() { Playground.ShowRules(); }
    [MenuItem("Tools/Eternal Enigma/Art/Preview World Overview")]
    public static void Overview() { Playground.WorldOverview(); }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Coast")]
    public static void Coast()
    {
        var p=Playground;p.ShowOverworld();var grid=p.Overworld.CurrentGrid;var mask=SmartEnvironmentMasks.World(grid,SmartEnvironmentMasks.Coast);
        var cell=Enumerable.Range(0,grid.Width*grid.Height).Select(i=>new Vector2Int(i%grid.Width,i/grid.Width))
            .Where(v=>v.x>1&&v.y>1&&v.x<grid.Width-2&&v.y<grid.Height-2&&mask[v.x,v.y]&&(!mask[v.x-1,v.y]||!mask[v.x+1,v.y]||!mask[v.x,v.y-1]||!mask[v.x,v.y+1]))
            .OrderBy(v=>Mathf.Abs(v.x-grid.Width*.5f)+Mathf.Abs(v.y-grid.Height*.5f)).First();
        var center=new Vector3(cell.x+.5f,cell.y+.5f,0)*p.Overworld.Template.cellSize;
        p.ViewCamera.orthographicSize=16;p.ViewCamera.transform.position=center+new Vector3(0,-12,-25);p.ViewCamera.transform.LookAt(center,Vector3.up);
    }
    [MenuItem("Tools/Eternal Enigma/Art/Inspect Generated Playground")]
    public static void Inspect()
    {
        foreach (var c in Object.FindObjectsByType<TWC.TileWorldCreator>(FindObjectsSortMode.None))
        {
            Debug.Log(c.name + "\n" + string.Join("\n", c.twcAsset.mapBuildLayers.Select(l => l.layerName + " active=" + l.active + " source=" + l.assignedGenerationLayerGuid)) + "\n" +
                string.Join("\n",c.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>(true).Select(o => o.name + " props="+o.PropCount+" tris="+o.TriangleCount+" active="+o.gameObject.activeInHierarchy)));
        }
    }
    [MenuItem("Tools/Eternal Enigma/Art/Capture Playground")]
    public static void Capture()
    {
        var p = Playground;
        var camera = p.ViewCamera;
        var target = RenderTexture.GetTemporary(1600, 1000, 24);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
            Directory.CreateDirectory("Temp/EnvironmentPreview");
            File.WriteAllBytes($"Temp/EnvironmentPreview/View{p.View}.png", image.EncodeToPNG());
            Debug.Log($"Captured playground view {p.View}");
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
    }
}
