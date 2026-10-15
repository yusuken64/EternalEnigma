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
        if (hud.town != null || hud.world != null) hud.FitSharedHeader();
        return hud;
    }

    private void FitSharedHeader()
    {
        // Match the dungeon resource strip: below shortcuts and above the right-hand dock.
        JuicyChickenGames.Menu.Dialog.Fit(GoldText.transform.parent,.73f,.89f,.98f,.935f);
        JuicyChickenGames.Menu.Dialog.Fit(GoldText.transform,.10f,.05f,.49f,.95f);
        JuicyChickenGames.Menu.Dialog.Fit(BagText.transform,.60f,.05f,.98f,.95f);
        JuicyChickenGames.Menu.Dialog.Fit(CoinIcon.transform,.015f,.15f,.09f,.85f);
        JuicyChickenGames.Menu.Dialog.Fit(BagIcon.transform,.51f,.15f,.59f,.85f);
        foreach (var label in new[] { GoldText, BagText })
        {
            label.fontSize=22;label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=22;
        }
    }

    private void LateUpdate()
    {
        var common=Common.Instance;
        if(canvas==null)canvas=GetComponent<Canvas>();
        // Prepare the destination HUD underneath its cover so it fades in with the scene.
        bool ready=common!=null && !common.GlobalSettings.IsOpen &&
            (town == null || FindFirstObjectByType<TownMenuManager>()?.CoversGameplayHud != true) &&
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
        GoldText.text=GameplayText.Format(state.GoldLabel);BagText.text=GameplayText.Format(state.BagLabel);
        BagText.color=state.Capacity.HasValue&&state.ItemCount>=state.Capacity?new Color(.59f,.23f,.13f):
            state.Capacity.HasValue&&state.ItemCount==state.Capacity-1?new Color(.62f,.39f,.08f):GameUITheme.Ink;
    }
}
