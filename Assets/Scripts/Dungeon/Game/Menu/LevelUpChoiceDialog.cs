using System;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelUpChoiceDialog : Dialog
{
    public TMP_Text Heading;
    public Button StrButton, IntButton, AgiButton, LaterButton;
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
        Heading.text=$"{hero.Name} — Lv {level}  •  {pending} point(s) to spend";
        var before=dungeon!=null?dungeon.FinalStats:TownUtilityService.StatsFor(town);
        foreach(var (button,attribute) in new[]{(StrButton,HeroAttribute.Str),(IntButton,HeroAttribute.Int),(AgiButton,HeroAttribute.Agi)})
        {
            var proposed=points.Added(attribute);
            var oldBonus=HeroAttributes.ToModification(points);
            var newBonus=HeroAttributes.ToModification(proposed);
            var after=before+new StatModification {
                HPMax=newBonus.HPMax-oldBonus.HPMax,SPMax=newBonus.SPMax-oldBonus.SPMax,
                HungerMax=newBonus.HungerMax-oldBonus.HungerMax,Strength=newBonus.Strength-oldBonus.Strength,
                MagicPower=newBonus.MagicPower-oldBonus.MagicPower,Defense=newBonus.Defense-oldBonus.Defense,
                HitBonus=newBonus.HitBonus-oldBonus.HitBonus,Evasion=newBonus.Evasion-oldBonus.Evasion,
                CritChance=newBonus.CritChance-oldBonus.CritChance,
                SPRegenAcccumlateThreshold=newBonus.SPRegenAcccumlateThreshold-oldBonus.SPRegenAcccumlateThreshold
            };
            button.GetComponentInChildren<TMP_Text>().text=$"{attribute.ToString().ToUpperInvariant()} {points.Get(attribute)}→{proposed.Get(attribute)}"+
                (preferred==attribute?"  ★ Recommended":"")+"\n"+StatPreview.Diff(before,after);
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{
                if(!spend(attribute))return;
                if((hero.DungeonActor?.PendingAttributePoints ?? hero.TownActor.PendingAttributePoints)>0)Configure();
                else CloseDialog();
            });
        }
        LaterButton.onClick.RemoveAllListeners();LaterButton.onClick.AddListener(CloseDialog);
        StrButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=IntButton,selectOnUp=LaterButton};
        IntButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=AgiButton,selectOnUp=StrButton};
        AgiButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=LaterButton,selectOnUp=IntButton};
        LaterButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=StrButton,selectOnUp=AgiButton};
        CloseAction=()=>{if(hero?.DungeonActor!=null)hero.DungeonActor.AttributePromptPending=false;finished?.Invoke();Destroy(gameObject);};
    }
    internal override void SetFirstSelect() => ((hero?.DungeonActor?.PrimaryClass?.PreferredAttribute ?? hero?.TownActor?.PrimaryClass?.PreferredAttribute ?? HeroAttribute.Str) switch {
        HeroAttribute.Int => IntButton, HeroAttribute.Agi => AgiButton, _ => StrButton }).Select();
}
