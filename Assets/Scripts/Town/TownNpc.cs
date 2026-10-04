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
        TownMenu.ShowMessage(Definition.DisplayName + ": " + Definition.Greeting);
    }
    public static void TurnToward(Transform visual, Vector3Int direction)
    {
        // Authored model faces south, up is -Z in the gameplay world.
        visual.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.x,-direction.y)*Mathf.Rad2Deg);
    }
}
