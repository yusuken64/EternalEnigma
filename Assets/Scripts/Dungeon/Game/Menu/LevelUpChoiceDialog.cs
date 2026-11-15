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
    private bool celebrate;
    private Ally presentedHero;
    private Facing previousFacing;
    private CameraController presentationCamera;
    private Transform cameraTarget;

    public static LevelUpChoiceDialog Open(Ally target, Action onFinished)
    {
        var dialog=Build(MenuManager.Instance.transform,new PartyMenuHero{
            Name=target.CharacterName,DungeonActor=target},attribute=>AttributeSpending.TrySpend(target,attribute),onFinished);
        dialog.celebrate = true;
        MenuManager.Open(dialog);
        return dialog;
    }
    public static LevelUpChoiceDialog Build(Transform parent,PartyMenuHero target,Func<HeroAttribute,bool> spendPoint,Action onFinished)
    {
        var prefab=Resources.Load<LevelUpChoiceDialog>("UI/LevelUpChoiceDialog");
        if(prefab==null)throw new InvalidOperationException("LevelUpChoiceDialog prefab is missing.");
        var dialog=Instantiate(prefab,parent);
        dialog.ApplyPopupLayout();
        dialog.hero=target;dialog.spend=spendPoint;dialog.finished=onFinished;
        dialog.Configure();return dialog;
    }

    private void OnEnable()
    {
        ApplyPopupLayout();
        presentedHero = hero?.DungeonActor;
        if (presentedHero == null) return;
        previousFacing = presentedHero.CurrentFacing;
        presentedHero.SetFacing(Facing.Down);
        presentationCamera = Game.Instance?.PlayerController?.CameraController;
        cameraTarget = presentedHero.CirlcleRenderer != null ? presentedHero.CirlcleRenderer.transform : presentedHero.transform;
        presentationCamera?.FramePresentation(cameraTarget, new Vector2(.5f, .20f));
        if (!celebrate) return;
        presentedHero.HeroAnimator?.PlayOneShot(AnimatedAction.LevelUp);
        var player = CombatEffectPlayer.Get();
        if (player != null)
            player.StartCoroutine(player.Cue(CombatVisualCatalog.Instance?.LevelUp,
                CombatEffectPlayer.Body(presentedHero, presentedHero.TilemapPosition)));
    }

    private void OnDisable()
    {
        if (presentedHero != null) presentedHero.SetFacing(previousFacing);
        if (presentationCamera != null) presentationCamera.ClearPresentation(cameraTarget);
        presentedHero = null;
        presentationCamera = null;
    }
    private void OnRectTransformDimensionsChange() => ApplyPopupLayout();
    private void ApplyPopupLayout()
    {
        if (Heading == null || transform is not RectTransform surface) return;
        var panel = (RectTransform)Heading.transform.parent;
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .70f);
        panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(Mathf.Min(1160, Mathf.Max(320, surface.rect.width - 48)),
            Mathf.Min(460, Mathf.Max(340, surface.rect.height * .42f)));
    }
    private void Configure()
    {
        var dungeon=hero.DungeonActor;var town=hero.TownActor;
        var points=dungeon?.Attributes ?? town.Attributes;
        int level=dungeon?.Vitals.Level ?? town.Level;
        Heading.text=GameplayText.Format($"{hero.Name} — Lv {level}");
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
            var preview = button.GetComponent<LevelUpChoiceView>();
            if (preview == null) preview = button.gameObject.AddComponent<LevelUpChoiceView>();
            preview.Show(attribute, points.Get(attribute), proposed.Get(attribute), before, after);
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{
                if(!spend(attribute))return;
                if((hero.DungeonActor?.PendingAttributePoints ?? hero.TownActor.PendingAttributePoints)>0)Configure();
                else CloseDialog();
            });
        }
        StrButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=IntButton,selectOnLeft=AgiButton};
        IntButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=AgiButton,selectOnLeft=StrButton};
        AgiButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=StrButton,selectOnLeft=IntButton};
        CloseAction=()=>{if(hero?.DungeonActor!=null)hero.DungeonActor.AttributePromptPending=false;finished?.Invoke();Destroy(gameObject);};
    }
    // Attribute allocation is mandatory, including when opened from the party menu.
    internal override void OnBackRequested() { }
    internal override void SetFirstSelect() => ((hero?.DungeonActor?.PrimaryClass?.PreferredAttribute ?? hero?.TownActor?.PrimaryClass?.PreferredAttribute ?? HeroAttribute.Str) switch {
        HeroAttribute.Int => IntButton, HeroAttribute.Agi => AgiButton, _ => StrButton }).Select();
}
