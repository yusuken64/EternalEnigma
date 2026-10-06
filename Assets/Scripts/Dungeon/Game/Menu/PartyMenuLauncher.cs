using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PartyMenuLauncher : MonoBehaviour
{
    private Func<bool> ready;
    private Canvas canvas;
    [SerializeField] private TMP_Text inventory,skills;
    [SerializeField] private Button inventoryButton, skillsButton;
    private bool? lastPad;
    #if UNITY_EDITOR
    public static void AuthorLayout(Transform parent,Func<bool> ready,Action<PartyMenuTab> open)
    {
        var canvas=GameUISkin.Canvas("Party menu shortcuts",parent,85);
        var launcher=canvas.gameObject.AddComponent<PartyMenuLauncher>();launcher.canvas=canvas;launcher.ready=ready;
        var safe=GameUISkin.Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaPanel>();
        var inventory=GameUISkin.Button(safe,"Inventory [Q]",new Vector2(.57f,.935f),new Vector2(.71f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Inventory);});
        var skills=GameUISkin.Button(safe,"Skills [R]",new Vector2(.72f,.935f),new Vector2(.85f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Skills);});
        inventory.navigation=skills.navigation=new Navigation{mode=Navigation.Mode.None};
        launcher.inventoryButton=inventory;launcher.skillsButton=skills;
        launcher.inventory=inventory.GetComponentInChildren<TMP_Text>();launcher.skills=skills.GetComponentInChildren<TMP_Text>();
    }
#endif
    public static void Create(Transform parent,Func<bool> ready,Action<PartyMenuTab> open)
    {
        var view=AuthoredUI.Require<PartyMenuLauncher>(parent);
        view.canvas=view.GetComponent<Canvas>(); view.ready=ready;
        view.inventoryButton.onClick.RemoveAllListeners(); view.skillsButton.onClick.RemoveAllListeners();
        view.inventoryButton.onClick.AddListener(()=> { if(ready() && MenuUIInputModule.Active?.HasDialog!=true) open(PartyMenuTab.Inventory); });
        view.skillsButton.onClick.AddListener(()=> { if(ready() && MenuUIInputModule.Active?.HasDialog!=true) open(PartyMenuTab.Skills); });
    }
    private void Update()
    {
        if(ready==null)return;
        canvas.enabled=ready() && !Common.Instance.GlobalSettings.IsOpen && !Common.Instance.Travel.IsTransitioning && MenuUIInputModule.Active?.HasDialog!=true;
        bool pad=ControlDeviceState.Gamepad;
        if(lastPad==pad)return;lastPad=pad;
        inventory.text="Inventory ["+InputPrompts.Inventory+"]";
        skills.text="Skills ["+InputPrompts.Skills+"]";
    }
}
