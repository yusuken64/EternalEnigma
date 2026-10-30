using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuDeveloperControls : MonoBehaviour
{
    public Button Toggle;
    public GameObject[] Controls;
    public Button FirstControl;
    private bool open;
    private bool initialized;
    private TMP_Text toggleLabel;
    public void Initialize()
    {
        open = false;
        Toggle.gameObject.SetActive(true);
        foreach (var control in Controls) control.SetActive(false);
        toggleLabel = Toggle.GetComponentInChildren<TMP_Text>(true);
        if (toggleLabel != null)
        {
            toggleLabel.fontSizeMax = toggleLabel.fontSize;
            toggleLabel.enableAutoSizing = true;
            toggleLabel.fontSizeMin = Mathf.Min(20, toggleLabel.fontSizeMax);
            RefreshLabel();
        }
        Toggle.onClick.RemoveListener(ToggleControls);
        Toggle.onClick.AddListener(ToggleControls);
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        bool visible = MenuUIInputModule.Active?.HasDialog != true;
        if (Toggle.gameObject.activeSelf != visible) Toggle.gameObject.SetActive(visible);
        foreach (var control in Controls)
            if (control.activeSelf != (visible && open)) control.SetActive(visible && open);
    }

    private void ToggleControls()
    {
        open = !open;
        foreach (var control in Controls) control.SetActive(open);
        RefreshLabel();
        (open && FirstControl != null ? FirstControl : Toggle).Select();
    }

    public bool Close()
    {
        if (!open) return false;
        ToggleControls();
        return true;
    }

    private void RefreshLabel()
    {
        if (Toggle is SelectToActivateButton button)
            button.AudioRole = open ? SelectToActivateButton.SoundRole.Cancel : SelectToActivateButton.SoundRole.Confirm;
        if (toggleLabel != null) toggleLabel.text = open ? "Close developer" : "Developer";
    }
}
