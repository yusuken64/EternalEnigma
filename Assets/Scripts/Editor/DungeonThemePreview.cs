using System;
using System.Linq;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Generation;
using TWC;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TileWorldDungeonGenerator))]
public sealed class DungeonThemePreviewInspector : Editor
{
    OverworldBiome biome;
    DungeonEnvironmentKind environment;
    int seed=12345;
    bool throne, legacy = true;
    int tier;
    DungeonFloorRole role;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        using(new EditorGUI.DisabledScope(Application.isPlaying || DungeonThemePreview.Building))
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Editor preview",EditorStyles.boldLabel);
            biome=(OverworldBiome)EditorGUILayout.EnumPopup("Biome",biome);
            environment=(DungeonEnvironmentKind)EditorGUILayout.EnumPopup("Environment",environment);
            seed=EditorGUILayout.IntField("Seed",seed);throne=EditorGUILayout.Toggle("Throne layout",throne);
            legacy=EditorGUILayout.Toggle("Legacy layout",legacy);
            tier=EditorGUILayout.IntSlider("Tier",tier,0,4);
            role=(DungeonFloorRole)EditorGUILayout.EnumPopup("Floor role",role);
            if(GUILayout.Button("Build theme preview")) DungeonThemePreview.Build((TileWorldDungeonGenerator)target,new DungeonVisualSelection {Biome=biome,Environment=environment},seed,throne,legacy,tier,role);
            if(GUILayout.Button("Restore authored Grassland preview")) DungeonThemePreview.Clear();
        }
    }
}

[InitializeOnLoad]
public static class DungeonThemePreview
{
    static GameObject host,world,authored;
    static bool authoredActive;
    static TileWorldCreatorAsset asset;
    static Color ambient,lightColor;
    static float intensity;
    static Light light;
    public static bool Building {get;private set;}
    public static GameObject World=>world;
    public static DungeonFloor Floor {get;private set;}
    public static event Action Completed;
    static DungeonThemePreview()
    {
        AssemblyReloadEvents.beforeAssemblyReload+=Clear;
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode) Clear();};
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing+=(s,removing)=>Clear();
    }
    public static void Build(TileWorldDungeonGenerator generator,DungeonVisualSelection selection,int seed,bool throne,bool legacy=true,int tier=0,DungeonFloorRole role=DungeonFloorRole.Regular)
    {
        Clear();
        selection.UseBiomePresentation=!legacy;
        if(!legacy) throne=role!=DungeonFloorRole.Regular;
        authored=generator.TileWorldCreator.worldObject;authoredActive=authored.activeSelf;authored.SetActive(false);
        ambient=RenderSettings.ambientLight;
        light=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
        if(light!=null){lightColor=light.color;intensity=light.intensity;}
        host=new GameObject("Dungeon theme preview controller") {hideFlags=HideFlags.HideAndDontSave};
        world=new GameObject("Dungeon theme preview output") {hideFlags=HideFlags.DontSave};
        var creator=host.AddComponent<TileWorldCreator>();
        asset=DungeonPresentation.CloneTemplate((throne?generator.ThroneTileWorldCreator:generator.TileWorldCreator).twcAsset);asset.hideFlags=HideFlags.HideAndDontSave;
        creator.twcAsset=asset;creator.worldObject=world;
        var catalog=generator.ThemeCatalog!=null?generator.ThemeCatalog:Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
        catalog.Apply(asset,selection,throne);
        if(!legacy) CoreDungeonLayerGenerator.Configure(asset,DungeonLayoutProfile.Options(seed,selection.Biome,tier,role));
        if(!selection.IsLegacy)
        {var t=catalog.Get(selection);RenderSettings.ambientLight=t.Ambient;if(light!=null){light.color=t.LightColor;light.intensity=t.LightIntensity;}}
        creator.SetCustomRandomSeed(seed);Building=true;
        creator.OnBlueprintLayersComplete+=c=>{CoreLayoutCache.ClearResultFlags(asset);c.ExecuteAllBuildLayers(true);};
        creator.OnBuildLayersComplete+=c=>
        {
            CoreLayoutCache.TryGetDungeon(c,out var floor);Floor=floor;
            if(!selection.IsLegacy) DungeonPresentation.Decorate(c,floor,catalog.Get(selection));
            else DungeonPresentation.TrackLegacyMeshes(world);
            DungeonPresentation.PreviewScenery(c,floor,catalog.Get(selection));
            Building=false;SceneView.RepaintAll();Completed?.Invoke();
        };
        var random=UnityEngine.Random.state;
        try {creator.ExecuteAllBlueprintLayers();} catch {Clear();throw;}
        finally {UnityEngine.Random.state=random;}
    }
    public static void Clear()
    {
        if(world!=null) {DungeonPresentation.ClearOutput(world);UnityEngine.Object.DestroyImmediate(world);}
        if(host!=null) UnityEngine.Object.DestroyImmediate(host);
        if(asset!=null) DungeonPresentation.ReleaseTemplate(asset);
        if(authored!=null) authored.SetActive(authoredActive);
        if(authored!=null) RenderSettings.ambientLight=ambient;
        if(light!=null){light.color=lightColor;light.intensity=intensity;}
        host=null;world=null;authored=null;asset=null;light=null;Floor=null;Building=false;
    }
}
