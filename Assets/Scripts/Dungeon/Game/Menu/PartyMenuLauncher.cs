using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PartyMenuLauncher : MonoBehaviour
{
    private Func<bool> ready;
    private Canvas canvas;
    [SerializeField] private TMP_Text inventory,skills,equipment,stats;
    [SerializeField] private Button inventoryButton, skillsButton, equipmentButton, statsButton;
    private bool? lastPad;
    #if UNITY_EDITOR
    public static void AuthorLayout(Transform parent,Func<bool> ready,Action<PartyMenuTab> open)
    {
        var canvas=GameUISkin.Canvas("Party menu shortcuts",parent,85);
        var launcher=canvas.gameObject.AddComponent<PartyMenuLauncher>();launcher.canvas=canvas;launcher.ready=ready;
        var safe=GameUISkin.Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaPanel>();
        var inventory=GameUISkin.Button(safe,"Inventory",new Vector2(.46f,.935f),new Vector2(.58f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Inventory);});
        var equipment=GameUISkin.Button(safe,"Equipment",new Vector2(.59f,.935f),new Vector2(.71f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Equipment);});
        var skills=GameUISkin.Button(safe,"Skills",new Vector2(.72f,.935f),new Vector2(.82f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Skills);});
        var stats=GameUISkin.Button(safe,"Stats",new Vector2(.83f,.935f),new Vector2(.94f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Stats);});
        inventory.navigation=skills.navigation=equipment.navigation=stats.navigation=new Navigation{mode=Navigation.Mode.None};
        launcher.inventoryButton=inventory;launcher.skillsButton=skills;
        launcher.equipmentButton=equipment;launcher.statsButton=stats;
        launcher.inventory=inventory.GetComponentInChildren<TMP_Text>();launcher.skills=skills.GetComponentInChildren<TMP_Text>();
        launcher.equipment=equipment.GetComponentInChildren<TMP_Text>();launcher.stats=stats.GetComponentInChildren<TMP_Text>();
    }
#endif
    public static void Create(Transform parent,Func<bool> ready,Action<PartyMenuTab> open)
    {
        var view=AuthoredUI.Require<PartyMenuLauncher>(parent);
        view.canvas=view.GetComponent<Canvas>(); view.ready=ready;
        if(view.equipmentButton==null){view.equipmentButton=Instantiate(view.skillsButton,view.skillsButton.transform.parent);view.equipmentButton.name="Equipment shortcut";view.equipment=view.equipmentButton.GetComponentInChildren<TMP_Text>();}
        if(view.statsButton==null){view.statsButton=Instantiate(view.skillsButton,view.skillsButton.transform.parent);view.statsButton.name="Stats shortcut";view.stats=view.statsButton.GetComponentInChildren<TMP_Text>();}
        var buttons=new[]{view.inventoryButton,view.equipmentButton,view.skillsButton,view.statsButton};
        for(int i=0;i<buttons.Length;i++){var rect=(RectTransform)buttons[i].transform;rect.anchorMin=new Vector2(.46f+i*.12f,rect.anchorMin.y);rect.anchorMax=new Vector2(.57f+i*.12f,rect.anchorMax.y);}
        view.inventoryButton.onClick.RemoveAllListeners(); view.skillsButton.onClick.RemoveAllListeners();
        view.equipmentButton.onClick.RemoveAllListeners();view.statsButton.onClick.RemoveAllListeners();
        view.inventoryButton.onClick.AddListener(()=> { if(ready() && MenuUIInputModule.Active?.HasDialog!=true) open(PartyMenuTab.Inventory); });
        view.skillsButton.onClick.AddListener(()=> { if(ready() && MenuUIInputModule.Active?.HasDialog!=true) open(PartyMenuTab.Skills); });
        view.equipmentButton.onClick.AddListener(()=> { if(ready() && MenuUIInputModule.Active?.HasDialog!=true) open(PartyMenuTab.Equipment); });
        view.statsButton.onClick.AddListener(()=> { if(ready() && MenuUIInputModule.Active?.HasDialog!=true) open(PartyMenuTab.Stats); });
    }
    private void Update()
    {
        if(ready==null)return;
        canvas.enabled=ready() && !Common.Instance.GlobalSettings.IsOpen && !Common.Instance.Travel.IsTransitioning && MenuUIInputModule.Active?.HasDialog!=true;
        bool pad=ControlDeviceState.Gamepad;
        if(lastPad==pad)return;lastPad=pad;
        inventory.text="Inventory ["+InputPrompts.Inventory+"]";
        skills.text="Skills ["+InputPrompts.Skills+"]";
        equipment.text="Equipment";stats.text="Stats";
    }
}
