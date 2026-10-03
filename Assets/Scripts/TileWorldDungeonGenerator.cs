using System;
using System.Collections;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Generation;
using TWC;
using UnityEngine;

public class TileWorldDungeonGenerator : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;
	public TileWorldCreator ThroneTileWorldCreator;
	public string FloorLayerName;

	public TileWorldDungeon TileWorldDungeonPrefab;

	public TileWorldDungeon GeneratedDungeon;

	public DungeonFloor CurrentFloor { get; private set; }
    public DungeonThemeCatalog ThemeCatalog;
    public DungeonEncounterVisualSettings EncounterVisuals = new();
    public DungeonVisualSelection CurrentVisuals { get; private set; }
    private TileWorldCreatorAsset regularTemplate, throneTemplate;
    private TileWorldCreatorAsset regularRuntime, throneRuntime;
    private Color originalAmbient;
    private Light themeLight;
    private Color originalLightColor;
    private float originalLightIntensity;

	private void Awake()
	{
        regularTemplate=TileWorldCreator.twcAsset;
        throneTemplate=ThroneTileWorldCreator.twcAsset;
        originalAmbient=RenderSettings.ambientLight;
        themeLight=RenderSettings.sun;
        if(themeLight==null) foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) if(light.type==LightType.Directional) {themeLight=light;break;}
        if(themeLight!=null) {originalLightColor=themeLight.color;originalLightIntensity=themeLight.intensity;}
        TileWorldCreator.twcAsset = DungeonPresentation.CloneTemplate(TileWorldCreator.twcAsset);
        TileWorldCreator.twcAsset.hideFlags = HideFlags.DontSave;
        ThroneTileWorldCreator.twcAsset = DungeonPresentation.CloneTemplate(ThroneTileWorldCreator.twcAsset);
        ThroneTileWorldCreator.twcAsset.hideFlags = HideFlags.DontSave;
        regularRuntime=TileWorldCreator.twcAsset;throneRuntime=ThroneTileWorldCreator.twcAsset;

		TileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		TileWorldCreator.OnBuildLayersComplete += BuildComplete;

		ThroneTileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		ThroneTileWorldCreator.OnBuildLayersComplete += BuildComplete;
	}

	private void OnDestroy()
	{
        if(TileWorldCreator!=null) {TileWorldCreator.OnBlueprintLayersComplete-=BluePrintComplete;TileWorldCreator.OnBuildLayersComplete-=BuildComplete;}
        if(ThroneTileWorldCreator!=null) {ThroneTileWorldCreator.OnBlueprintLayersComplete-=BluePrintComplete;ThroneTileWorldCreator.OnBuildLayersComplete-=BuildComplete;}
        DungeonPresentation.ReleaseTemplate(regularRuntime);DungeonPresentation.ReleaseTemplate(throneRuntime);
	}

	internal void GenerateDungeon()
	{
		if (GeneratedDungeon != null)
		{
			Destroy(GeneratedDungeon.gameObject);
		}
		GeneratedDungeon = null;
		PreparePresentation(TileWorldCreator, false);
		SetCampaignSeed(TileWorldCreator);
		TileWorldCreator.ExecuteAllBlueprintLayers();
	}

	internal void GenerateThroneRoom()
	{
		if (GeneratedDungeon != null)
		{
			Destroy(GeneratedDungeon.gameObject);
		}
		GeneratedDungeon = null;
		PreparePresentation(ThroneTileWorldCreator, true);
		SetCampaignSeed(ThroneTileWorldCreator);
		ThroneTileWorldCreator.ExecuteAllBlueprintLayers();
	}

    private void PreparePresentation(TileWorldCreator creator, bool throne)
    {
        var common=Common.Instance;
        CurrentVisuals=common.GameSaveData?.DungeonSaveData?.VisualSelection ?? DungeonVisualSelection.Resolve(common.CampaignContext,EncounterVisuals);
        var run = common.GameSaveData?.DungeonSaveData;
        var visuals = CurrentVisuals; visuals.UseBiomePresentation = run != null && run.UseBiomeLayout; CurrentVisuals = visuals;
        if(ThemeCatalog==null) ThemeCatalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
        if(!CurrentVisuals.IsLegacy && ThemeCatalog==null) throw new InvalidOperationException("Dungeon theme catalog is missing.");
        TileWorldCreator.StopAllCoroutines();ThroneTileWorldCreator.StopAllCoroutines();
        DungeonPresentation.ClearOutput(TileWorldCreator.worldObject);
        if(ThroneTileWorldCreator.worldObject!=TileWorldCreator.worldObject) DungeonPresentation.ClearOutput(ThroneTileWorldCreator.worldObject);
        DungeonPresentation.PrepareMapRoot(creator.worldObject.transform, CurrentVisuals.IsLegacy);
        bool useSeed=creator.twcAsset.useRandomSeed;
        int seed=creator.twcAsset.randomSeed;
        var owned=throne?throneRuntime:regularRuntime;
        if(owned!=creator.twcAsset) DungeonPresentation.ReleaseTemplate(owned);
        DungeonPresentation.ReleaseTemplate(creator.twcAsset);
        creator.twcAsset=DungeonPresentation.CloneTemplate(throne ? throneTemplate : regularTemplate);
        if(throne) throneRuntime=creator.twcAsset;else regularRuntime=creator.twcAsset;
        creator.twcAsset.hideFlags=HideFlags.DontSave;
        creator.twcAsset.useRandomSeed=useSeed;creator.twcAsset.randomSeed=seed;
        ThemeCatalog?.Apply(creator.twcAsset,CurrentVisuals,throne);
        RenderSettings.ambientLight=CurrentVisuals.IsLegacy ? originalAmbient : ThemeCatalog.Get(CurrentVisuals).Ambient;
        if(themeLight!=null) {themeLight.color=CurrentVisuals.IsLegacy ? originalLightColor : ThemeCatalog.Get(CurrentVisuals).LightColor;
            themeLight.intensity=CurrentVisuals.IsLegacy ? originalLightIntensity : ThemeCatalog.Get(CurrentVisuals).LightIntensity;}
    }

    private void SetCampaignSeed(TileWorldCreator creator)
    {
        var context = Common.Instance.CampaignContext;
        if (context != null) creator.SetCustomRandomSeed(context.LocationSeed(context.State.LocationId, Game.Instance.PlayerController.Floor));
        var run = Common.Instance.GameSaveData?.DungeonSaveData;
        if (run != null && run.UseBiomeLayout)
        {
            int floor = Game.Instance.PlayerController.Floor;
            var role = floor == run.StartFloor ? DungeonFloorRole.Entry : floor == run.EndFloor ? DungeonFloorRole.Exit : DungeonFloorRole.Regular;
            CoreDungeonLayerGenerator.Configure(creator.twcAsset, DungeonLayoutProfile.Options(creator.twcAsset.randomSeed, run.LayoutBiome, run.LayoutTier, role));
        }
    }

	private void BluePrintComplete(TileWorldCreator _twc)
	{
		CoreLayoutCache.ClearResultFlags(_twc.twcAsset);
		_twc.ExecuteAllBuildLayers(true);
	}

	private void BuildComplete(TileWorldCreator _twc)
	{
		if (!CoreLayoutCache.TryGetDungeon(_twc, out var floor))
			throw new InvalidOperationException("The TWC asset has no Core Dungeon Layer actions; run Tools/Eternal Enigma/Core Layers/Rewrite Dungeon Assets.");
		CurrentFloor = floor;
        if(!CurrentVisuals.IsLegacy) DungeonPresentation.Decorate(_twc, floor, ThemeCatalog.Get(CurrentVisuals));
        else DungeonPresentation.TrackLegacyMeshes(_twc.worldObject);
		var newDungeon = Instantiate(TileWorldDungeonPrefab);
		newDungeon.Setup(_twc, floor);
		GeneratedDungeon = newDungeon;
	}
}
