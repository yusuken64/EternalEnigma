using UnityEngine;
using UnityEngine.InputSystem;

public class CursorManager : MonoBehaviour
{
    void Update()
    {
        // If any controller was used this frame
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        // If mouse was moved/clicked this frame
        else if (Mouse.current != null && Mouse.current.wasUpdatedThisFrame)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        // If keyboard was pressed this frame (optional)
        else if (Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
