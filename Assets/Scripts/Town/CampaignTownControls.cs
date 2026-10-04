using UnityEngine;

public sealed class CampaignTownControls : MonoBehaviour
{
    public Town Town;
    public Vector3Int Exit => Town.Plan.Exit.ToCell();
    private void Start()
    {
        // TownPlayer handles interacting with the south gate using normal movement input.
        AuthoredUI.Require<CampaignHUD>(transform).Town = Town;
    }
}
