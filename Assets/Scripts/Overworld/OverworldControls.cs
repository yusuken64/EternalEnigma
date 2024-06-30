using UnityEngine;
using UnityEngine.InputSystem;

public class OverworldControls : MonoBehaviour
{
	public OverworldPlayer OverworldPlayer;

	private DungeonControls controls;

	void Awake()
    {
        controls = new DungeonControls();

		controls.Player.Move.performed += Move_performed;
		controls.Player.Move.canceled += Move_canceled;
    }

	private void Move_canceled(InputAction.CallbackContext obj)
	{
		OverworldPlayer.ControllerHeld = false;
	}

	private void Move_performed(InputAction.CallbackContext obj)
	{
		var direction = obj.ReadValue<Vector2>();
		var directionInt = new Vector3Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));
		var facing = Character.GetFacing(directionInt);
		OverworldPlayer.SetFacing(facing);
		OverworldPlayer.ControllerHeld = true;
	}

	private void OnEnable()
	{
		controls.Player.Enable();
	}

	private void OnDisable()
	{
		controls.Player.Disable();
	}
}
