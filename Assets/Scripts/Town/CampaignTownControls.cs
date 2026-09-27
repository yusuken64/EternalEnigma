using UnityEngine;

public sealed class CampaignTownControls : MonoBehaviour
{
    public Town Town;
    public Vector3Int Exit => new(10, 0, 0);
    private void Start()
    {
        // TownPlayer handles interacting with the south gate using normal movement input.
        gameObject.AddComponent<CampaignHUD>().Town = Town;
    }
}
