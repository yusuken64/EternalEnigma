using System;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public class WalkableMap : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;

	private bool[,] _walkableMap;

	private void Awake()
	{
		TileWorldCreator.OnBlueprintLayersComplete += blueprintLayersComplete;
	}

	private void OnDestroy()
	{
		TileWorldCreator.OnBlueprintLayersComplete -= blueprintLayersComplete;
	}

	private void blueprintLayersComplete(TileWorldCreator _twc)
	{
		_walkableMap = CoreLayoutCache.TryGetTown(_twc, out var plan)
			? plan.Layers[TownLayers.Walkable].ToArray()
			: throw new InvalidOperationException("Core town plan missing.");
	}

	internal Vector3 CellToWorld(Vector3Int newMapPosition)
	{
		return GridMovement.CellToWorld(newMapPosition, TileWorldCreator.twcAsset.cellSize);
	}

	internal bool CanWalkTo(Vector3Int from, Vector3Int to)
	{
		return GridMovement.CanStep(from, to, cell => GridMovement.IsWalkable(_walkableMap, cell),
			DiagonalMovement.AllowCornerCutting);
	}

    internal AStar.Node[,] GetAStarGrid()
    {
        var grid = new AStar.Node[_walkableMap.GetLength(0),_walkableMap.GetLength(1)];
        for (int x=0;x<grid.GetLength(0);x++) for (int y=0;y<grid.GetLength(1);y++)
            grid[x,y] = new AStar.Node(x,y,_walkableMap[x,y],0);
        return grid;
    }
}
