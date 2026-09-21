using System;
using System.Collections;
using System.Collections.Generic;
using TWC;
using UnityEngine;

public class TileWorldDungeonGenerator : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;
	public TileWorldCreator ThroneTileWorldCreator;
	public string FloorLayerName;

	public TileWorldDungeon TileWorldDungeonPrefab;

	public TileWorldDungeon GeneratedDungeon;

	private void Awake()
	{
        if (Common.Instance.CampaignContext != null)
        {
            TileWorldCreator.twcAsset = Instantiate(TileWorldCreator.twcAsset);
            ThroneTileWorldCreator.twcAsset = Instantiate(ThroneTileWorldCreator.twcAsset);
        }
		TileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		TileWorldCreator.OnBuildLayersComplete += BuildComplete;

		ThroneTileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		ThroneTileWorldCreator.OnBuildLayersComplete += BuildComplete;
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
		_twc.ExecuteAllBuildLayers(true);
	}

	private void BuildComplete(TileWorldCreator _twc)
	{
		var newDungeon = Instantiate(TileWorldDungeonPrefab);
		newDungeon.Setup(_twc);
		GeneratedDungeon = newDungeon;
	}
}
