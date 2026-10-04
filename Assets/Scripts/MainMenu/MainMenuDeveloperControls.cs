using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuDeveloperControls : MonoBehaviour
{
    public Button Toggle;
    public GameObject[] Controls;
    private bool open;
    public void Initialize()
    {
        open = false;
        Toggle.gameObject.SetActive(true);
        foreach (var control in Controls) control.SetActive(false);
//#if UNITY_EDITOR || DEVELOPMENT_BUILD
//        Toggle.gameObject.SetActive(true);
//        Toggle.onClick.RemoveAllListeners();
//        Toggle.onClick.AddListener(() => { open=!open; foreach(var control in Controls)control.SetActive(open); });
//#else
//        Toggle.gameObject.SetActive(false);
//#endif
    }
}
