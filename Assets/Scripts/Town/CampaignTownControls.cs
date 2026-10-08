using UnityEngine;

public sealed class CampaignTownControls : MonoBehaviour
{
    public Town Town;
    public Vector3Int Exit => Town.Plan.Exit.ToCell();
}
