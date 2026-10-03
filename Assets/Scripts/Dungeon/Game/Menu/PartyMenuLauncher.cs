using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PartyMenuLauncher : MonoBehaviour
{
    private Func<bool> ready;
    private Canvas canvas;
    private TMP_Text inventory,skills;
    private bool? lastPad;
    public static void Create(Transform parent,Func<bool> ready,Action<PartyMenuTab> open)
    {
        var canvas=GameUISkin.Canvas("Party menu shortcuts",parent,85);
        var launcher=canvas.gameObject.AddComponent<PartyMenuLauncher>();launcher.canvas=canvas;launcher.ready=ready;
        var safe=GameUISkin.Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaPanel>();
        var inventory=GameUISkin.Button(safe,"Inventory [Q]",new Vector2(.57f,.935f),new Vector2(.71f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Inventory);});
        var skills=GameUISkin.Button(safe,"Skills [R]",new Vector2(.72f,.935f),new Vector2(.85f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Skills);});
        inventory.navigation=skills.navigation=new Navigation{mode=Navigation.Mode.None};
        launcher.inventory=inventory.GetComponentInChildren<TMP_Text>();launcher.skills=skills.GetComponentInChildren<TMP_Text>();
    }
    private void Update()
    {
        canvas.enabled=ready() && !Common.Instance.GlobalSettings.IsOpen && !Common.Instance.Travel.IsTransitioning && MenuUIInputModule.Active?.HasDialog!=true;
        bool pad=MenuUIInputModule.Active?.UsingGamepad==true;
        if(lastPad==pad)return;lastPad=pad;
        inventory.text=pad?"Inventory [X / Square]":"Inventory [Q]";
        skills.text=pad?"Skills [LB / L1]":"Skills [R]";
    }
}
