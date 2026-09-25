using System;
using System.Collections;
using System.Collections.Generic;
using EternalEnigma.Core.World;
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

	private void Awake()
	{
        TileWorldCreator.twcAsset = Instantiate(TileWorldCreator.twcAsset);
        TileWorldCreator.twcAsset.hideFlags = HideFlags.DontSave;
        ThroneTileWorldCreator.twcAsset = Instantiate(ThroneTileWorldCreator.twcAsset);
        ThroneTileWorldCreator.twcAsset.hideFlags = HideFlags.DontSave;

		TileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		TileWorldCreator.OnBuildLayersComplete += BuildComplete;

		ThroneTileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		ThroneTileWorldCreator.OnBuildLayersComplete += BuildComplete;
	}

	private void OnDestroy()
	{
		TileWorldCreator.OnBlueprintLayersComplete -= BluePrintComplete;
		TileWorldCreator.OnBuildLayersComplete -= BuildComplete;

		ThroneTileWorldCreator.OnBlueprintLayersComplete -= BluePrintComplete;
		ThroneTileWorldCreator.OnBuildLayersComplete -= BuildComplete;

		if (TileWorldCreator != null && TileWorldCreator.twcAsset != null) Destroy(TileWorldCreator.twcAsset);
		if (ThroneTileWorldCreator != null && ThroneTileWorldCreator.twcAsset != null) Destroy(ThroneTileWorldCreator.twcAsset);
	}

	internal void GenerateDungeon()
	{
		if (GeneratedDungeon != null)
		{
			Destroy(GeneratedDungeon.gameObject);
		}
		GeneratedDungeon = null;
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
		SetCampaignSeed(ThroneTileWorldCreator);
		ThroneTileWorldCreator.ExecuteAllBlueprintLayers();
	}

    private void SetCampaignSeed(TileWorldCreator creator)
    {
        var context = Common.Instance.CampaignContext;
        if (context != null) creator.SetCustomRandomSeed(context.LocationSeed(context.State.LocationId, Game.Instance.PlayerController.Floor));
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
		var newDungeon = Instantiate(TileWorldDungeonPrefab);
		newDungeon.Setup(_twc, floor);
		GeneratedDungeon = newDungeon;
	}
}
