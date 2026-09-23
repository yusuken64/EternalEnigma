using UnityEngine;

public class TownBuilding : MonoBehaviour
{
	public string Name;

	public Vector3Int TilemapPosition { get; internal set; }

	public TownBuildingDefinition Definition { get; internal set; }

	/// <summary>True once a shop interior/vendor was actually carved for this instance this generation; walking onto the door tile no longer opens the dialog directly.</summary>
	public bool HasInterior { get; internal set; }

	public virtual void Interact(TownPlayer townPlayer, TownAction reverse)
	{
		FindFirstObjectByType<TownMenu>().OpenBuilding(Definition, townPlayer, reverse);
	}
}
