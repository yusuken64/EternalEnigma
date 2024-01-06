using System;
using System.Collections;
using System.Collections.Generic;
using TWC;
using UnityEngine;

public class TileWorldDungeonGenerator : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;
	public string FloorLayerName;

	public TileWorldDungeon TileWorldDungeonPrefab;

	public TileWorldDungeon GeneratedDungeon;

	private void Awake()
	{
		TileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		TileWorldCreator.OnBuildLayersComplete += BuildComplete;
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

	private void BluePrintComplete(TileWorldCreator _twc)
	{
		_twc.ExecuteAllBuildLayers(true);
	}

	private void BuildComplete(TileWorldCreator _twc)
	{
		var newDungeon = Instantiate(TileWorldDungeonPrefab);
		newDungeon.Setup(TileWorldCreator);
		GeneratedDungeon = newDungeon;
	}
}
