using System;
using System.IO;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEngine;
using EternalEnigma.Core.World;
using UnityEditor.SceneManagement;

public static class DungeonThemeAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Repair Active Ground Heights")]
    public static void RepairActiveGroundHeights()
    {
        var game = Game.Instance;
        if (!Application.isPlaying || game?.CurrentDungeon == null) throw new InvalidOperationException("Requires an active dungeon.");
        CaptureActiveGround("Before");
        foreach (var creator in new[] { game.DungeonGenerator.TileWorldCreator, game.DungeonGenerator.ThroneTileWorldCreator })
        {
            if (!creator.twcAsset.mapBuildLayers.OfType<DungeonThemeTileLayer>().Any()) continue;
            foreach (Transform layer in creator.worldObject.transform)
            {
                var definition = creator.twcAsset.mapBuildLayers.OfType<DungeonThemeTileLayer>().FirstOrDefault(l => layer.name == l.layerName + "_layer");
                if (definition != null)
                {
                    bool carpet = definition.layerName.ToLowerInvariant().Contains("carpet");
                    if (carpet && !game.CurrentDungeon.IsThroneFloor)
                    {
                        var paving = creator.twcAsset.mapBuildLayers.OfType<DungeonThemeTileLayer>().First(l => l.layerName.ToLowerInvariant().Contains("floor"));
                        definition.Preset = paving.Preset; layer.gameObject.SetActive(true);
                        foreach (var renderer in layer.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = paving.Preset.fillTile.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                    }
                    DungeonPresentation.SetGroundHeight(layer, carpet ? (game.CurrentDungeon.IsThroneFloor ? -.015f : 0) : definition.Offset.z);
                }
                else if (layer.name == "Theme cosmetics") DungeonPresentation.SetGroundHeight(layer);
            }
        }
        CaptureActiveGround("After");
        Debug.Log("Repaired active themed dungeon ground heights without regenerating the floor or changing units/items.");
    }

    static void CaptureActiveGround(string suffix)
    {
        Directory.CreateDirectory("Temp/GroundHeight");
        var camera = Game.Instance.PlayerController.CameraController.Camera;
        var old = camera.targetTexture; var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1280,800,24); var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply();
            File.WriteAllBytes("Temp/GroundHeight/" + suffix + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = old; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Capture All Themes")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
        var generator=UnityEngine.Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
        if(generator==null) throw new InvalidOperationException("Open DungeonScene before capturing previews.");
        if(DungeonThemePreview.Building) throw new InvalidOperationException("Wait for the current preview to finish.");
        int index=0;
        Action completed=null;
        void Next()
        {
            if(index==32) {DungeonThemePreview.Completed-=completed;DungeonThemePreview.Clear();Debug.Log("Captured all 32 biome/layout previews.");return;}
            var selection=new DungeonVisualSelection {Biome=(OverworldBiome)(index/4),Environment=(DungeonEnvironmentKind)((index/2)%2)};
            DungeonThemePreview.Build(generator,selection,12345,index%2==1);
        }
        void Tick() {EditorApplication.update-=Tick;Next();}
        completed=()=> {Capture(((OverworldBiome)(index/4))+"_"+((DungeonEnvironmentKind)((index/2)%2))+"_"+(index%2==1?"Throne":"Regular"));Debug.Log("Captured dungeon theme "+index);index++;EditorApplication.update+=Tick;};
        DungeonThemePreview.Completed+=completed;Next();
    }
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Restore Preview")]
    public static void RestorePreview()=>DungeonThemePreview.Clear();
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Capture Baseline")]
    public static void CaptureBaseline()
    {
        Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
        var generator = UnityEngine.Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
        foreach (var creator in new[] { generator.TileWorldCreator, generator.ThroneTileWorldCreator })
        {
            var a = creator.twcAsset;
            Debug.Log(a.name + " world=" + a.worldName + " size=" + a.cellSize + " layers: " + string.Join("; ", a.mapBuildLayers.Select(l => l.layerName + ":" + l.GetType().Name + (l is InstantiateTiles t ? " presets=" + string.Join(",",t.tiles.Select(p=>p.preset != null ? p.preset.name : "null")) : ""))));
        }
        Capture("Baseline");
    }
    public static void Capture(string name)
    {
        var go = new GameObject("Theme capture camera");
        var camera = go.AddComponent<Camera>();
        var world = DungeonThemePreview.World != null ? DungeonThemePreview.World : GameObject.Find("TileWorldCreator_Map");
        var renderers = world.GetComponentsInChildren<MeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        camera.orthographic=true; camera.orthographicSize=Mathf.Max(bounds.size.y*.55f,bounds.size.x*.55f/1.6f);
        camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-100);
        camera.farClipPlane=300; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.08f,.10f);
        var old = camera.targetTexture;
        var rt = RenderTexture.GetTemporary(1280, 800, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1280,800,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1280,800),0,0); texture.Apply(); File.WriteAllBytes("Docs/Art/Previews/DungeonThemes/"+name+".png",texture.EncodeToPNG()); }
        finally { camera.targetTexture=old; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }
}


