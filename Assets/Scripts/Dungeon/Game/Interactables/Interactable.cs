using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
	public bool Opened { get; set; }
	public Vector3Int Position { get; internal set; }
	abstract internal List<GameAction> GetInteractionSideEffects(Character character);
	abstract internal string GetInteractionText();

	internal void Setup(Vector3Int position)
	{
		this.Position = position;
	}
}
