using UnityEngine;

public class CursorManager : MonoBehaviour
{
    private void Update()
    {
        ControlDeviceState.Poll();
        Cursor.visible = !ControlDeviceState.Gamepad;
        Cursor.lockState = CursorLockMode.None;
    }
}
