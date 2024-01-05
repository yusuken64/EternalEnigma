using System;
using System.Collections;
using System.Collections.Generic;
using TWC;
using UnityEngine;

public class TestPlayer : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;
	public string StartPlayerLayer;
	public string FloorLayerName;
	public Camera Camera;
	public Vector3 CameraOffset;

	public Vector3Int PlayerPosition;

	private void Awake()
	{
		TileWorldCreator.OnBlueprintLayersComplete += BluePrintComplete;
		TileWorldCreator.OnBuildLayersComplete += BuildComplete;
		TileWorldCreator.ExecuteAllBlueprintLayers();
	}

	private void BluePrintComplete(TileWorldCreator _twc)
	{
		_twc.ExecuteAllBuildLayers(true);
	}

	private void BuildComplete(TileWorldCreator _twc)
	{
		var startMap = _twc.GetMapOutputFromBlueprintLayer(StartPlayerLayer);
		var startIndex = FindFirstTrueElement(startMap);

		PlayerPosition = new Vector3Int(startIndex.Item1, startIndex.Item2, 0);
		UpdateWorldPosition();
	}

	private void UpdateWorldPosition()
	{
		//var floorMap = TileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		//var tileDetected = floorMap[PlayerPosition.x, PlayerPosition.y];

		//Debug.Log($"Moving to {PlayerPosition.x},{PlayerPosition.y} {(tileDetected ? "walkable" : "unwalkable")}");
		////var cellSize = TileWorldCreator.twcAsset.cellSize;
		var cellSize = 2;
		var tileWorldPosition = PlayerPosition * (int)cellSize;
		this.transform.position = tileWorldPosition;
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.W))
		{
			var newPositon = this.PlayerPosition + new Vector3Int(0, 1, 0);
			if (CanMoveTo(newPositon))
			{
				PlayerPosition = newPositon;
				UpdateWorldPosition();
			}
		}
		if (Input.GetKeyDown(KeyCode.A))
		{
			var newPositon = this.PlayerPosition + new Vector3Int(-1, 0, 0);
			if (CanMoveTo(newPositon))
			{
				PlayerPosition = newPositon;
				UpdateWorldPosition();
			}
		}
		if (Input.GetKeyDown(KeyCode.S))
		{
			var newPositon = this.PlayerPosition + new Vector3Int(0, -1, 0);
			if (CanMoveTo(newPositon))
			{
				PlayerPosition = newPositon;
				UpdateWorldPosition();
			}
		}
		if (Input.GetKeyDown(KeyCode.D))
		{
			var newPositon = this.PlayerPosition + new Vector3Int(1, 0, 0);
			if (CanMoveTo(newPositon))
			{
				PlayerPosition = newPositon;
				UpdateWorldPosition();
			}
		}
		if (Input.GetKeyDown(KeyCode.Q))
		{
			ModifyCurrentDungeonTile(false);
		}
		if (Input.GetKeyDown(KeyCode.E))
		{
			ModifyCurrentDungeonTile(true);
		}


		Camera.transform.position = this.transform.position + CameraOffset;
	}

	private bool CanMoveTo(Vector3Int newPositon)
	{
		var floorMap = TileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		var floorDetected = floorMap[newPositon.x, newPositon.y];

		return floorDetected;
	}

	private void ModifyCurrentDungeonTile(bool value)
	{
		TileWorldCreator.ModifyMap(FloorLayerName, PlayerPosition.x, PlayerPosition.y, value);

		TileWorldCreator.ExecuteBlueprintLayer(FloorLayerName);
	}

	static (int, int) FindFirstTrueElement(bool[,] arr)
	{
		int rows = arr.GetLength(0);
		int cols = arr.GetLength(1);

		int iIndex = -1;
		int jIndex = -1;
		bool found = false;

		for (int i = 0; i < rows; i++)
		{
			for (int j = 0; j < cols; j++)
			{
				if (arr[i, j])
				{
					iIndex = i;
					jIndex = j;
					found = true;
					break;
				}
			}
			if (found)
			{
				break;
			}
		}

		return (iIndex, jIndex);
	}
}
