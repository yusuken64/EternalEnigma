using UnityEngine;

public class TownBuilding : MonoBehaviour
{
	public string Name;

	public Vector3Int TilemapPosition { get; internal set; }

	public TownBuildingDefinition Definition { get; internal set; }

	public virtual void Interact(TownPlayer townPlayer, TownAction reverse)
	{
		FindFirstObjectByType<TownMenu>().OpenBuilding(Definition, townPlayer, reverse);
	}
}
