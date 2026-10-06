using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class PartyMenuContext : IPartyMenuContext
{
    protected readonly List<PartyMenuHero> heroes = new();
    private readonly Dictionary<string,List<Skill>> skills = new();
    public IReadOnlyList<PartyMenuHero> Heroes => heroes;
    protected abstract List<InventoryItem> Bag { get; }
    protected IEnumerable<Skill> Skills(PartyMenuHero hero)
    {
        if(hero.DungeonActor!=null)return hero.DungeonActor.Skills;
        if(!skills.TryGetValue(hero.Id,out var list))
        {
            list=new();skills.Add(hero.Id,list);
            foreach(var name in hero.TownActor.Skills ?? new())
            {
                var definition=Common.Instance.SkillManager.GetSkillByName(name);
                if(definition==null)continue;
                var instance=UnityEngine.Object.Instantiate(definition);instance.Rank=Math.Max(1,hero.TownActor.GetRank(name));list.Add(instance);
            }
        }
        return list;
    }
    public virtual List<PartyMenuEntry> Entries(PartyMenuHero hero,PartyMenuTab tab)
    {
        if(tab==PartyMenuTab.Skills)return Skills(hero).Where(s=>s!=null).OrderBy(s=>s.ActivationType).Select(s=>new PartyMenuEntry {
            Skill=s,Title=$"{s.SkillName}  R{s.Rank}" + (s.ActivationType==ActivationType.Active?$"  {s.SPCost} SP":"  Passive"),
            Description=s.Description+(s.UsesArrows?$"\nAmmunition: {(s.ArrowCostMode==ArrowCostMode.PerTarget?"1 per target":s.ArrowCost.ToString())} arrows":""),
            Section=s.ActivationType==ActivationType.Active?"Active skills":"Passive skills",Icon=s.Icon }).ToList();
        return hero.Equipment.GetEquippedItems().Cast<InventoryItem>().Concat(Bag).Where(i=>i?.ItemDefinition!=null).Select(i=>new PartyMenuEntry {
            Item=i,Title=i.ItemName+(i.HasStacks?$"  x{i.StackStock}":"")+(hero.Equipment.IsEquipped(i)?"  [Equipped]":""),
            Equipped=hero.Equipment.IsEquipped(i),Section=hero.Equipment.IsEquipped(i)?"Equipped":"Shared bag",
            Icon=GamePresentationProfile.Current?.ItemIcons.Length>(int)i.ItemDefinition.DroppedItemVisual ? GamePresentationProfile.Current.ItemIcons[(int)i.ItemDefinition.DroppedItemVisual] : null,
            Description=i.ItemDefinition.Description+(i is EquipableInventoryItem equipment ? "\n"+equipment.EquipmentSlot+"\n"+string.Join("\n",equipment.GetEquipmentStatModification().DescribeEffect()):"") }).ToList();
    }
    public abstract List<PartyMenuAction> Actions(PartyMenuHero hero,PartyMenuEntry entry);
    public virtual string Restriction(PartyMenuHero hero,PartyMenuEntry entry) => entry.Skill?.ActivationType==ActivationType.Passive?"Passive skill: applied automatically.":"";
    public string HeroDetails(PartyMenuHero hero)
    {
        var stats=hero.DungeonActor!=null?hero.DungeonActor.FinalStats:TownUtilityService.StatsFor(hero.TownActor);
        int hp=hero.DungeonActor!=null?hero.DungeonActor.Vitals.HP:RecoveryMath.SavedVital(hero.TownActor.Hp,stats.HPMax);
        int sp=hero.DungeonActor!=null?hero.DungeonActor.Vitals.SP:RecoveryMath.SavedVital(hero.TownActor.Sp,stats.SPMax);
        return $"{hero.Name}   HP {hp}/{stats.HPMax}   SP {sp}/{stats.SPMax}\nStrength {stats.Strength}   Defense {stats.Defense}";
    }
    public virtual void Dispose()
    {
        foreach(var list in skills.Values)foreach(var skill in list)UnityEngine.Object.Destroy(skill);
        skills.Clear();
    }
    protected void Add(TownAlly ally)=>heroes.Add(new PartyMenuHero {Id=ally.Id,Name=ally.Name,Portrait=ally.Portrait,TownActor=ally});
}

public sealed class TownPartyMenuContext : PartyMenuContext
{
    private readonly Town town;
    private readonly TownUtilityService utilities;
    protected override List<InventoryItem> Bag=>town.TownPlayer.Inventory;
    public TownPartyMenuContext(Town town)
    {
        this.town=town;
        foreach(var hero in town.TownPlayer.RecruitedAllies)Add(hero);
        utilities=new TownUtilityService(town.TownPlayer.RecruitedAllies,Bag,town.SaveProgress);
    }
    public override string Restriction(PartyMenuHero hero,PartyMenuEntry entry)
    {
        if(entry.Item is EquipableInventoryItem || entry.Item?.ItemDefinition is MaterialItemDefinition)return "";
        utilities.Describe(hero.TownActor,entry.Skill,entry.Item,out _,out string reason);return reason ?? "";
    }
    public override List<PartyMenuAction> Actions(PartyMenuHero hero,PartyMenuEntry entry)
    {
        if(entry.Item is EquipableInventoryItem equipment)
        {
            string reason=!hero.Equipment.IsEquipped(equipment) && !HeroClass.AllowsItem(hero.TownActor.PrimaryClass,hero.TownActor.SecondaryClass,equipment)?HeroClass.EquipmentRestriction(hero.TownActor.PrimaryClass,hero.TownActor.SecondaryClass,equipment):null;
            return new(){new PartyMenuAction {Label=entry.Equipped?"Unequip":"Equip",UnavailableReason=reason,Execute=menu=>
                {town.Services.ToggleEquipment(hero.TownActor,entry.Item,out var failure);menu.Complete(failure??"Equipment updated.");}}};
        }
        if(entry.Item?.ItemDefinition is MaterialItemDefinition)return new(){new PartyMenuAction {Label=$"Sell for {TownServices.SellPrice(entry.Item):N0} gold",Execute=menu=>
            {town.Services.Sell(entry.Item,out var reason);menu.Complete(reason??"Sold.");}}};
        if(entry.Skill?.ActivationType==ActivationType.Passive)return new();
        utilities.Describe(hero.TownActor,entry.Skill,entry.Item,out _,out string unavailable);
        return new(){new PartyMenuAction {Label=entry.Skill!=null?"Cast":"Use",UnavailableReason=unavailable,Execute=menu=>Begin(menu,hero,entry)}};
    }
    private void Begin(PartyMenu menu,PartyMenuHero hero,PartyMenuEntry entry)
    {
        if(!utilities.Describe(hero.TownActor,entry.Skill,entry.Item,out var utility,out var reason)){menu.Complete(reason);return;}
        void Commit(TownAlly target,InventoryItem item)
        {utilities.Execute(hero.TownActor,entry.Skill,entry.Item,target,item,out var message);menu.Complete(message);}
        if(utility.Targeting==SkillTargeting.InventoryItem)
            menu.Pick("Choose an item",utilities.InventoryTargets(hero.TownActor,utility).Select(i=>(i.ItemName+(i.HasStacks?$" x{i.StackStock}":"")+(hero.Equipment.IsEquipped(i)?" [Equipped]":""),(Action)(()=>Commit(null,i)))).ToList());
        else if(utility.Targeting==SkillTargeting.SelectedTarget)
            menu.Pick("Choose a party member",utilities.Targets(hero.TownActor,utility).Select(h=>(h.Name,(Action)(()=>Commit(h,null)))).ToList());
        else Commit(hero.TownActor,null);
    }
}

public sealed class OverworldPartyMenuContext : PartyMenuContext
{
    private readonly OverworldScene world;
    private readonly List<InventoryItem> bag;
    protected override List<InventoryItem> Bag=>bag;
    public OverworldPartyMenuContext(OverworldScene world)
    {
        this.world=world;
        var save=Common.Instance.GameSaveData;
        bag=save.TownSaveData.InventoryItems.Select(i=>i.Restore(Common.Instance.ItemManager)).ToList();
        foreach(var data in save.TownSaveData.RecruitedAlliesData)
        {
            var live=data.AllyId==save.ProtagonistId?world.Player:world.Followers.FirstOrDefault(h=>h.Id==data.AllyId);
            if(live==null)continue;
            RestoreHero(live,data);Add(live);
        }
    }
    public override List<PartyMenuEntry> Entries(PartyMenuHero hero, PartyMenuTab tab)
    {
        if(tab!=PartyMenuTab.Capabilities)return base.Entries(hero,tab);
        var context=world.Context;
        return EternalEnigma.Core.Progression.CampaignGuidance.Acquired(context).Select(capability=>new PartyMenuEntry {
            Title=EternalEnigma.Core.Capabilities.CapabilityCatalog.DisplayName(capability), Section="Acquired capabilities",
            Description="Source: "+EternalEnigma.Core.Progression.CampaignGuidance.CapabilitySource(context,capability,
                id=>CampaignParty.Resolve(id,TownSceneLoader.Default)?.Name??id)+"\n"+
                (context.Held.Contains(capability)?"Available":"Unavailable: add the required companion to your travelling party at a town.")
        }).ToList();
    }
    public static void RestoreHero(TownAlly live,TownAllyData data)
    {
        live.Id=data.AllyId;live.Name=data.AllyName;live.Hp=data.Hp;live.Sp=data.Sp;live.HighestLevel=data.HighestLevel;live.Level=data.Level;live.Experience=data.Experience;
        live.Skills=data.Skills?.ToList()??new();live.SkillRanks=data.SkillRanks?.Select(r=>new SkillRankSaveData{SkillName=r.SkillName,Rank=r.Rank}).ToList()??new();
        HeroClassBinding.Apply(live,data,Common.Instance.GameSaveData);
        foreach(var previous in live.Equipment.GetEquippedItems().ToArray())live.Equipment.UnEquip(previous);
        foreach(var serialized in data.Equipment)
            if(serialized.Restore(Common.Instance.ItemManager) is EquipableInventoryItem equipment)live.Equipment.Equip(equipment);
        live.RefreshEquipmentVisuals();
        live.EnsureStartingSkills();
    }
    public override string Restriction(PartyMenuHero hero,PartyMenuEntry entry)=> entry.Item==null && entry.Skill==null?"Choose a capability when interacting with an obstacle.":entry.Item is EquipableInventoryItem?"Equipment can be changed while travelling.":
        entry.Skill?.ActivationType==ActivationType.Passive?base.Restriction(hero,entry):"Inspect only while travelling. Use items and recovery skills in town, or actions in a dungeon.";
    public override List<PartyMenuAction> Actions(PartyMenuHero hero,PartyMenuEntry entry)
    {
        if(entry.Item is not EquipableInventoryItem equipment)return new();
        string reason=!hero.Equipment.IsEquipped(equipment)&&!HeroClass.AllowsItem(hero.TownActor.PrimaryClass,hero.TownActor.SecondaryClass,equipment)?HeroClass.EquipmentRestriction(hero.TownActor.PrimaryClass,hero.TownActor.SecondaryClass,equipment):null;
        return new(){new PartyMenuAction {Label=entry.Equipped?"Unequip":"Equip",UnavailableReason=reason,Execute=menu=>
        {
            if(!EquipmentTransferService.Toggle(hero.Equipment,bag,entry.Item,hero.TownActor.PrimaryClass,hero.TownActor.SecondaryClass,out var failure))
            {menu.Complete(failure);return;}
            var common=Common.Instance;var save=common.GameSaveData;
            save.TownSaveData.InventoryItems=ItemSaveData.Capture(bag);
            var data=save.TownSaveData.RecruitedAlliesData.Single(h=>h.AllyId==hero.Id);
            data.Equipment=ItemSaveData.Capture(hero.Equipment.GetEquippedItems());
            CampaignParty.Capture(common);
            hero.TownActor.RefreshEquipmentVisuals();
            SaveSystem.Capture(Common.Instance);
            menu.Complete("Equipment changed.");
        }}};
    }
}

public sealed class DungeonPartyMenuContext : PartyMenuContext, IPartyMenuEntryHandler
{
    private readonly Game game;
    protected override List<InventoryItem> Bag=>game.PlayerController.Inventory.InventoryItems;
    public DungeonPartyMenuContext(Game game)
    {
        this.game=game;
        foreach(var ally in PartyRules.PartyMembers(game))heroes.Add(new PartyMenuHero {Id=ally.TownAllyId,Name=ally.CharacterName,Portrait=ally.Portrait,DungeonActor=ally});
    }
    private string Eligibility(PartyMenuHero hero)=>hero.DungeonActor!=game.PlayerController.ControlledAlly || !game.PlayerController.CanOpenMenu() ?
        "Only the controlled hero awaiting an action can act this turn.":null;
    public bool OpenEntry(PartyMenu menu, PartyMenuHero hero, PartyMenuEntry entry)
    {
        if (entry.Item == null) return false;
        var dialog = MenuManager.Instance.ActionDialog;
        dialog.Setup(null, entry.Item, hero.DungeonActor);
        dialog.ValidateActor = () => Eligibility(hero) == null;
        dialog.RefreshAvailability();
        MenuManager.Open(dialog);
        return true;
    }
    public override List<PartyMenuAction> Actions(PartyMenuHero hero,PartyMenuEntry entry)
    {
        if(entry.Skill?.ActivationType==ActivationType.Passive)return new();
        var actor=hero.DungeonActor;string unavailable=Eligibility(hero);
        if(entry.Skill!=null)
        {
            if(unavailable==null&&!actor.CanCast(entry.Skill,out unavailable)){}
            return new(){new PartyMenuAction {Label="Cast",UnavailableReason=unavailable,Execute=menu=>
            {
                if(Eligibility(hero)!=null || !actor.CanCast(entry.Skill,out var reason)){menu.Complete("That hero cannot cast now.");return;}
                var manager=MenuManager.Instance;
                if(entry.Skill.Targeting==SkillTargeting.InventoryItem)manager.OpenInventoryTargetingMenu(actor,entry.Skill);
                else if(entry.Skill.RequiresTargetSelection)manager.OpenTargetingMenu(actor,entry.Skill);
                else {manager.CloseAllMenus();actor.SetAction(new SkillAction(actor,entry.Skill,actor));}
            }}};
        }
        if (unavailable == null && entry.Item is EquipableInventoryItem equipment && !actor.Equipment.IsEquipped(equipment))
            unavailable = HeroClass.EquipmentRestriction(actor.PrimaryClass, actor.SecondaryClass, equipment);
        if(unavailable==null && !new UseInventoryItemAction(game.PlayerController.Inventory,actor,entry.Item).CanBegin(actor))unavailable="This item cannot be used now.";
        var actions=new List<PartyMenuAction>{new() {Label=entry.Item is EquipableInventoryItem?(entry.Equipped?"Unequip":"Equip"):"Use",UnavailableReason=unavailable,
            Execute=menu=>{if(Eligibility(hero)==null)MenuManager.Instance.UseInventoryItem(actor,entry.Item);else menu.Complete(Eligibility(hero));}}};
        // Preserve the dungeon's throw/drop commands and their existing validation/replay.
        foreach(string label in new[]{"Throw","Drop"})
            actions.Add(new PartyMenuAction{Label=label,UnavailableReason=Eligibility(hero) ?? (entry.Equipped?"Unequip this item first.":null),Execute=menu=>OpenEntry(menu,hero,entry)});
        return actions;
    }
}
