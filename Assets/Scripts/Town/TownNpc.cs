using UnityEngine;

/// <summary>Ambient townsfolk have no hero stats, inventory, combat, or persistent state.</summary>
public sealed class TownNpc : MonoBehaviour
{
    public TownNpcDefinition Definition;
    public Vector3Int Cell;
    public Transform Visual;
    public void Greet(Vector3Int player)
    {
        TurnToward(Visual, player - Cell);
        Visual.GetComponentInChildren<Animator>()?.SetTrigger("Greeting");
        var context = Common.Instance.CampaignContext;
        string hint = context == null ? "" : EternalEnigma.Core.Progression.CampaignGuidance.TownHint(context, context.State.LocationId,
            id => CampaignParty.Resolve(id, TownSceneLoader.Default)?.Name ?? id);
        TownMenu.ShowMessage(Definition.DisplayName + ": " + Definition.Greeting + (hint.Length == 0 ? "" : "\n\n" + hint));
    }
    public static void TurnToward(Transform visual, Vector3Int direction)
    {
        // Authored model faces south, up is -Z in the gameplay world.
        visual.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.x,-direction.y)*Mathf.Rad2Deg);
    }
}
