using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct ResourceHUDState : IEquatable<ResourceHUDState>
{
    public readonly int Gold, ItemCount;
    public readonly int? Capacity;
    public ResourceHUDState(int gold,int count,int? capacity=null){Gold=gold;ItemCount=count;Capacity=capacity;}
    public bool Equals(ResourceHUDState other)=>Gold==other.Gold && ItemCount==other.ItemCount && Capacity==other.Capacity;
    public string BagLabel => Capacity.HasValue ? $"Bag  {ItemCount:N0}/{Capacity.Value:N0}"+(ItemCount>=Capacity.Value?"  Full":"") : $"Bag  {ItemCount:N0} items";
}

public sealed class ResourceHUD : MonoBehaviour
{
    public TMP_Text GoldText, BagText;
    public Image CoinIcon, BagIcon;
    private Town town;
    private Game game;
    private OverworldScene world;
    private ResourceHUDState last;
    private bool initialized;
    private Canvas canvas;

    public static ResourceHUD Ensure(MonoBehaviour owner)
    {
        var hud=owner.GetComponentInChildren<ResourceHUD>(true);
        if(hud!=null)return hud;
        var prefab=Resources.Load<ResourceHUD>("UI/ResourceHUD");
        hud=prefab!=null?Instantiate(prefab,owner.transform):Build(owner.transform);
        hud.town=owner as Town;hud.game=owner as Game;hud.world=owner as OverworldScene;
        hud.canvas=hud.GetComponent<Canvas>();
        return hud;
    }
    public static ResourceHUD Build(Transform parent)
    {
        var canvas=GameUISkin.Canvas("ResourceHUD",parent,90);
        var hud=canvas.gameObject.AddComponent<ResourceHUD>();
        var safe=GameUISkin.Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaPanel>();
        var panel=GameUISkin.Panel(safe,Vector2.one,Vector2.one).rectTransform;
        panel.pivot=Vector2.one;panel.anchoredPosition=new Vector2(-24,-100);panel.sizeDelta=new Vector2(280,96);
        hud.CoinIcon=Icon(panel,new Vector2(12,-12));hud.BagIcon=Icon(panel,new Vector2(12,-52));
        hud.GoldText=Label(panel,-12);hud.BagText=Label(panel,-52);
        var profile=GamePresentationProfile.Current;
        hud.CoinIcon.sprite=profile?.CoinIcon;hud.BagIcon.sprite=profile?.BagIcon;
        foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
        return hud;
    }
    private static Image Icon(Transform parent,Vector2 position)
    {
        var rect=GameUISkin.Rect("Icon",parent,new Vector2(0,1),new Vector2(0,1));
        rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(32,32);
        var image=rect.gameObject.AddComponent<Image>();image.preserveAspect=true;return image;
    }
    private static TMP_Text Label(Transform parent,float y)
    {
        var text=GameUISkin.Label(parent,"",new Vector2(0,1),new Vector2(0,1),23);
        text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.anchoredPosition=new Vector2(52,y);text.rectTransform.sizeDelta=new Vector2(216,32);
        text.alignment=TextAlignmentOptions.MidlineLeft;text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=23;return text;
    }
    private void LateUpdate()
    {
        var common=Common.Instance;
        if(canvas==null)canvas=GetComponent<Canvas>();
        bool ready=common!=null && !common.Travel.IsTransitioning && !common.GlobalSettings.IsOpen &&
            (town!=null&&town.IsReady || game!=null&&game.IsReady || world!=null&&world.IsReady);
        canvas.enabled=ready;
        if(!ready)return;
        var state=town!=null?new ResourceHUDState(town.TownPlayer.Gold,town.TownPlayer.Inventory.Count):
            game!=null?new ResourceHUDState(game.PlayerController.Gold,game.PlayerController.Inventory.InventoryItems.Count,game.PlayerController.Inventory.MaxItems):
            new ResourceHUDState(common.GameSaveData.TownSaveData.Gold,common.GameSaveData.TownSaveData.InventoryItems.Count);
        SetState(state);
    }
    public void SetState(ResourceHUDState state)
    {
        if(initialized&&last.Equals(state))return;
        initialized=true;last=state;
        GoldText.text=$"Gold  {state.Gold:N0}";BagText.text=state.BagLabel;
        BagText.color=state.Capacity.HasValue&&state.ItemCount>=state.Capacity?new Color(.59f,.23f,.13f):
            state.Capacity.HasValue&&state.ItemCount==state.Capacity-1?new Color(.62f,.39f,.08f):GameUITheme.Ink;
    }
}
