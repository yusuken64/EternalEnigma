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
		TileWorldCreator.ExecuteAllBlueprintLayers();
	}

	internal void GenerateThroneRoom()
	{
		if (GeneratedDungeon != null)
		{
			Destroy(GeneratedDungeon.gameObject);
		}
		GeneratedDungeon = null;
		ThroneTileWorldCreator.ExecuteAllBlueprintLayers();
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
