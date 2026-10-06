using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct ResourceHUDState : IEquatable<ResourceHUDState>
{
    public readonly int Gold, ItemCount, SmallKeys;
    public readonly int? Capacity;
    public ResourceHUDState(int gold,int count,int? capacity=null,int smallKeys=0){Gold=gold;ItemCount=count;Capacity=capacity;SmallKeys=smallKeys;}
    public bool Equals(ResourceHUDState other)=>Gold==other.Gold && ItemCount==other.ItemCount && Capacity==other.Capacity && SmallKeys==other.SmallKeys;
    // Keys sit beside gold rather than in the bag, and only show once one is held.
    public string GoldLabel => $"Gold  {Gold:N0}"+(SmallKeys>0?$"   Keys  {SmallKeys:N0}":"");
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
        var hud = AuthoredUI.Require<ResourceHUD>(owner.transform);
        hud.town=owner as Town; hud.game=owner as Game; hud.world=owner as OverworldScene;
        hud.canvas=hud.GetComponent<Canvas>();
        return hud;
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
            game!=null?new ResourceHUDState(game.PlayerController.Gold,game.PlayerController.Inventory.InventoryItems.Count,game.PlayerController.Inventory.MaxItems,SmallKeys.Count):
            new ResourceHUDState(common.GameSaveData.TownSaveData.Gold,common.GameSaveData.TownSaveData.InventoryItems.Count);
        SetState(state);
    }
    public void SetState(ResourceHUDState state)
    {
        if(initialized&&last.Equals(state))return;
        initialized=true;last=state;
        GoldText.text=state.GoldLabel;BagText.text=state.BagLabel;
        BagText.color=state.Capacity.HasValue&&state.ItemCount>=state.Capacity?new Color(.59f,.23f,.13f):
            state.Capacity.HasValue&&state.ItemCount==state.Capacity-1?new Color(.62f,.39f,.08f):GameUITheme.Ink;
    }
}
