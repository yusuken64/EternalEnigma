using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuDeveloperControls : MonoBehaviour
{
    public Button Toggle;
    public GameObject[] Controls;
    public Button FirstControl;
    private bool open;
    public void Initialize()
    {
        open = false;
        Toggle.gameObject.SetActive(true);
        foreach (var control in Controls) control.SetActive(false);
        Toggle.onClick.RemoveListener(ToggleControls);
        Toggle.onClick.AddListener(ToggleControls);
    }

    private void ToggleControls()
    {
        open = !open;
        foreach (var control in Controls) control.SetActive(open);
        (open && FirstControl != null ? FirstControl : Toggle).Select();
    }
}
