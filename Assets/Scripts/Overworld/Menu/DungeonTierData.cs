using UnityEngine;

[CreateAssetMenu(fileName = "DungeonTierData", menuName = "Game/DungeonTierData")]
public class DungeonTierData : ScriptableObject
{
	public string TierName;
	public int StartFloor;
	public int EndFloor;
}