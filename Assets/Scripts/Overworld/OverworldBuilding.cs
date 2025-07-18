using UnityEngine;

public abstract class OverworldBuilding : MonoBehaviour
{
	public string Name;

	public Vector3Int TilemapPosition { get; internal set; }

	public abstract void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse);
}
