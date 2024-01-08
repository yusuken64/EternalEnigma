using System;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
	public bool Opened { get; set; }
	public Vector3Int Position { get; internal set; }

	abstract internal void DoInteraction();
	abstract internal string GetInteractionText();

	internal void Setup(Vector3Int position)
	{
		this.Position = position;
	}
}
