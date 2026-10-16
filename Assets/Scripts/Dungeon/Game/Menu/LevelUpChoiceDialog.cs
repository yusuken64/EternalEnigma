using System;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelUpChoiceDialog : Dialog
{
    public TMP_Text Heading;
    public Button StrButton, IntButton, AgiButton;
    private PartyMenuHero hero;
    private Func<HeroAttribute,bool> spend;
    private Action finished;

    public static LevelUpChoiceDialog Open(Ally target, Action onFinished)
    {
        var dialog=Build(MenuManager.Instance.transform,new PartyMenuHero{
            Name=target.CharacterName,DungeonActor=target},attribute=>AttributeSpending.TrySpend(target,attribute),onFinished);
        MenuManager.Open(dialog);
        return dialog;
    }
    public static LevelUpChoiceDialog Build(Transform parent,PartyMenuHero target,Func<HeroAttribute,bool> spendPoint,Action onFinished)
    {
        var prefab=Resources.Load<LevelUpChoiceDialog>("UI/LevelUpChoiceDialog");
        if(prefab==null)throw new InvalidOperationException("LevelUpChoiceDialog prefab is missing.");
        var dialog=Instantiate(prefab,parent);
        dialog.UseGameplayDock(dialog.Heading.transform.parent);
        dialog.Heading.enableAutoSizing=true;dialog.Heading.fontSizeMin=18;dialog.Heading.fontSizeMax=30;
        dialog.hero=target;dialog.spend=spendPoint;dialog.finished=onFinished;
        dialog.Configure();return dialog;
    }
    private void Configure()
    {
        var dungeon=hero.DungeonActor;var town=hero.TownActor;
        var points=dungeon?.Attributes ?? town.Attributes;
        int pending=dungeon?.PendingAttributePoints ?? town.PendingAttributePoints;
        int level=dungeon?.Vitals.Level ?? town.Level;
        var preferred=dungeon?.PrimaryClass?.PreferredAttribute ?? town?.PrimaryClass?.PreferredAttribute ?? HeroAttribute.Str;
        Heading.text=GameplayText.Format($"{hero.Name} — Lv {level}  •  {pending} {(pending==1?"point":"points")} to spend");
        var before=dungeon!=null?dungeon.FinalStats:TownUtilityService.StatsFor(town);
        foreach(var (button,attribute) in new[]{(StrButton,HeroAttribute.Str),(IntButton,HeroAttribute.Int),(AgiButton,HeroAttribute.Agi)})
        {
            var proposed=points.Added(attribute);
            var oldBonus=HeroAttributes.ToModification(points);
            var newBonus=HeroAttributes.ToModification(proposed);
            var after=before+new StatModification {
                HPMax=newBonus.HPMax-oldBonus.HPMax,ManaMax=newBonus.ManaMax-oldBonus.ManaMax,
                HungerMax=newBonus.HungerMax-oldBonus.HungerMax,Strength=newBonus.Strength-oldBonus.Strength,
                MagicPower=newBonus.MagicPower-oldBonus.MagicPower,Defense=newBonus.Defense-oldBonus.Defense,
                HitBonus=newBonus.HitBonus-oldBonus.HitBonus,Evasion=newBonus.Evasion-oldBonus.Evasion,
                CritChance=newBonus.CritChance-oldBonus.CritChance,
                ManaRegenThreshold=newBonus.ManaRegenThreshold-oldBonus.ManaRegenThreshold
            };
            button.GetComponentInChildren<TMP_Text>().text=GameplayText.Format($"{attribute.ToString().ToUpperInvariant()} {points.Get(attribute)}→{proposed.Get(attribute)}"+
                (preferred==attribute?"  Recommended":"")+"\n"+StatPreview.Diff(before,after));
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{
                if(!spend(attribute))return;
                if((hero.DungeonActor?.PendingAttributePoints ?? hero.TownActor.PendingAttributePoints)>0)Configure();
                else CloseDialog();
            });
        }
        StrButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=IntButton,selectOnUp=AgiButton};
        IntButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=AgiButton,selectOnUp=StrButton};
        AgiButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=StrButton,selectOnUp=IntButton};
        CloseAction=()=>{if(hero?.DungeonActor!=null)hero.DungeonActor.AttributePromptPending=false;finished?.Invoke();Destroy(gameObject);};
    }
    // Attribute allocation is mandatory, including when opened from the party menu.
    internal override void OnBackRequested() { }
    internal override void SetFirstSelect() => ((hero?.DungeonActor?.PrimaryClass?.PreferredAttribute ?? hero?.TownActor?.PrimaryClass?.PreferredAttribute ?? HeroAttribute.Str) switch {
        HeroAttribute.Int => IntButton, HeroAttribute.Agi => AgiButton, _ => StrButton }).Select();
}
