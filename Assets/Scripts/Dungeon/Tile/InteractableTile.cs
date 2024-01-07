using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName ="InteractableTile", menuName = "Game/Tile/InteractableTile")]
public class InteractableTile : Tile
{
	public bool IsStair;

	virtual internal string GetInteractionText()
	{
		return "Take Stairs";
	}

	virtual internal void DoInteraction()
	{
		Game.Instance.AdvanceFloor();
	}
}
