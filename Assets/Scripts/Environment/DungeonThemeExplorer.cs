using System;
using System.Linq;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Generation;
using TWC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Standalone playground view: uses production templates without Common, encounters, or saves.</summary>
[ExecuteAlways]
public sealed class DungeonThemeExplorer : MonoBehaviour
{
    public EnvironmentPlayground Playground;
    public TileWorldCreator Creator;
    public TileWorldCreatorAsset RegularTemplate, ThroneTemplate;
    public DungeonThemeCatalog Catalog;
    public OverworldBiome Biome;
    public DungeonEnvironmentKind Environment;
    public bool Throne;
    public bool LegacyLayout = true;
    [Range(0,4)] public int Tier;
    public DungeonFloorRole FloorRole;
    public TMP_Text Summary, EnvironmentLabel, LayoutLabel, TierLabel, LegacyLabel;
    public TMP_InputField SeedInput;
    public GameObject Controls;
    public bool IsReady {get;private set;}
    public bool IsBuilding {get;private set;}
    public GameObject WorldRoot {get;private set;}
    public DungeonFloor Floor {get;private set;}
    TileWorldCreatorAsset asset;
    bool visible,lightingCaptured;
    Color ambient,lightColor;
    float lightIntensity;
    Light light;
    DungeonVisualSelection selection;
    private void OnEnable()
    {
        if(Creator!=null) {Creator.OnBlueprintLayersComplete+=Blueprints;Creator.OnBuildLayersComplete+=Built;}
    }
    private void OnDisable()
    {
        if(Creator!=null) {Creator.OnBlueprintLayersComplete-=Blueprints;Creator.OnBuildLayersComplete-=Built;}
        RestoreLighting();
    }
    public void SetVisible(bool value)
    {
        visible=value;
        if(WorldRoot!=null) WorldRoot.SetActive(value);
        if(Controls!=null) Controls.SetActive(value);
        if(value) {if(IsReady) ApplyLighting();UpdateControls();} else RestoreLighting();
    }
    public void SelectBiome(int value) {if(IsBuilding)return;Biome=(OverworldBiome)value;Rebuild();}
    public void NextTier() { if(IsBuilding)return; Tier=(Tier+1)%5; Rebuild(); }
    public void ToggleLegacy() { if(IsBuilding)return; LegacyLayout=!LegacyLayout; if(!LegacyLayout) {FloorRole=Throne?DungeonFloorRole.Entry:DungeonFloorRole.Regular;Throne=false;} Rebuild(); }
    public void NextRole() { if(IsBuilding)return; Throne=false; FloorRole=(DungeonFloorRole)(((int)FloorRole+1)%3); Rebuild(); }
    public void NextBiome()=>SelectBiome(((int)Biome+1)%8);
    public void ToggleEnvironment() {if(IsBuilding)return;Environment=Environment==DungeonEnvironmentKind.Interior?DungeonEnvironmentKind.Outdoor:DungeonEnvironmentKind.Interior;Rebuild();}
    public void ToggleLayout() {if(IsBuilding)return;if(LegacyLayout) {Throne=!Throne;Rebuild();} else NextRole();}
    public void SetSeed(string text)
    {
        if(!IsBuilding && int.TryParse(text,out int seed)) {Playground.Seed=seed;Rebuild();}
        UpdateControls();
    }
    public void NextSeed() {if(IsBuilding)return;Playground.Seed++;Rebuild();}
    public void Rebuild()
    {
        if(IsBuilding) return;
        ClearGeometry();IsReady=false;IsBuilding=true;
        bool compact = LegacyLayout ? Throne : Throne || FloorRole != DungeonFloorRole.Regular;
        selection=new DungeonVisualSelection {Biome=Biome,Environment=Environment,UseBiomePresentation=!LegacyLayout};
        asset=DungeonPresentation.CloneTemplate(compact?ThroneTemplate:RegularTemplate);asset.worldName="Playground dungeon themes";
        Catalog.Apply(asset,selection,compact);
        if (!LegacyLayout) CoreDungeonLayerGenerator.Configure(asset, DungeonLayoutProfile.Options(Playground.Seed, Biome, Tier, Throne ? DungeonFloorRole.Entry : FloorRole));
        Creator.twcAsset=asset;
        WorldRoot=new GameObject(asset.worldName) {hideFlags=HideFlags.DontSave};Creator.worldObject=WorldRoot;
        WorldRoot.SetActive(visible);Creator.SetCustomRandomSeed(Playground.Seed);CoreLayoutCache.Clear(Creator);
        UpdateControls();var random=UnityEngine.Random.state;
        try {Creator.ExecuteAllBlueprintLayers();} catch {IsBuilding=false;ClearGeometry();UpdateControls();throw;}
        finally {UnityEngine.Random.state=random;}
    }
    void Blueprints(TileWorldCreator creator) {CoreLayoutCache.ClearResultFlags(asset);creator.ExecuteAllBuildLayers(true);}
    void Built(TileWorldCreator creator)
    {
        if(!CoreLayoutCache.TryGetDungeon(creator,out var floor)) throw new InvalidOperationException("Dungeon explorer needs Core Dungeon templates.");
        Floor=floor;
        if(!selection.IsLegacy) DungeonPresentation.Decorate(creator,floor,Catalog.Get(selection));
        else DungeonPresentation.TrackLegacyMeshes(WorldRoot);
        BiomeDecorations.Dungeon(creator,floor,selection,true);
        DungeonPresentation.PreviewScenery(creator,floor,Catalog.Get(selection));
        IsBuilding=false;IsReady=true;WorldRoot.SetActive(visible);
        if(visible) {ApplyLighting();Frame();}UpdateControls();
    }
    public void Frame()
    {
        if(!IsReady)return;
        var center=new Vector3(Floor.Width,Floor.Height,0)*asset.cellSize*.5f;
        var camera=Playground.ViewCamera;camera.orthographic=true;
        camera.orthographicSize=Mathf.Max(Floor.Height*asset.cellSize*.70f,Floor.Width*asset.cellSize*.66f/camera.aspect);
        // Leave the top quarter clear for explorer controls.
        camera.transform.position=center+new Vector3(0,-8,-35);camera.transform.LookAt(center,Vector3.up);
        camera.transform.position+=camera.transform.up*camera.orthographicSize*.27f;
    }
    void ApplyLighting()
    {
        if(!lightingCaptured)
        {
            ambient=RenderSettings.ambientLight;light=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
            if(light!=null){lightColor=light.color;lightIntensity=light.intensity;}lightingCaptured=true;
        }
        var theme=Catalog.Get(selection);RenderSettings.ambientLight=selection.IsLegacy?ambient:theme.Ambient;
        if(light!=null){light.color=selection.IsLegacy?lightColor:theme.LightColor;light.intensity=selection.IsLegacy?lightIntensity:theme.LightIntensity;}
    }
    void RestoreLighting()
    {
        if(!lightingCaptured)return;
        RenderSettings.ambientLight=ambient;if(light!=null){light.color=lightColor;light.intensity=lightIntensity;}lightingCaptured=false;
    }
    void UpdateControls()
    {
        if(EnvironmentLabel!=null) EnvironmentLabel.text=Environment.ToString();
        if(LayoutLabel!=null) LayoutLabel.text=LegacyLayout ? (Throne?"Throne room":"Regular floor") : FloorRole.ToString();
        if(SeedInput!=null) SeedInput.SetTextWithoutNotify(Playground.Seed.ToString());
        if(Summary!=null) Summary.text=IsBuilding?"Building dungeon…":$"{Biome} · {Environment} · {(LegacyLayout ? (Throne?"Throne room":"Regular floor") : FloorRole.ToString())} · Tier {Tier} · Seed {Playground.Seed}";
        if (TierLabel != null) TierLabel.text=$"Tier {Tier}";
        if (LegacyLabel != null) LegacyLabel.text=LegacyLayout?"Legacy":"Biome layout";
        if(Controls!=null) foreach(var selectable in Controls.GetComponentsInChildren<Selectable>(true)) selectable.interactable=!IsBuilding;
    }
    void ClearGeometry()
    {
        if(WorldRoot!=null) {DungeonPresentation.ClearOutput(WorldRoot);DungeonPresentation.Release(WorldRoot);WorldRoot=null;}
        if(asset!=null) {DungeonPresentation.ReleaseTemplate(asset);asset=null;}
        Floor=null;IsReady=false;
    }
    private void OnDestroy() {RestoreLighting();ClearGeometry();}
}
