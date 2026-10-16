using System;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignSummaryDialog : Dialog
{
    [Serializable] public sealed class HeroCard
    {
        public RectTransform Root;
        public Image Portrait;
        public TMP_Text Name, Details;
    }
    public TMP_Text Statistics;
    public HeroCard[] Heroes;
    public Button ContinueButton, SaveButton;

    internal static CampaignSummaryDialog Create(Transform parent, CampaignCompletionRecord record)
    {
        var dialog = Instantiate(Resources.Load<CampaignSummaryDialog>("UI/CampaignSummary"), parent, false);
        foreach (var label in dialog.GetComponentsInChildren<TMP_Text>(true))
            if (label.text == "HALL OF FAME")
            {
                Fit(label.transform, .25f, .87f, .75f, .98f);
                GameUISkin.RibbonHeader(label);
            }
        dialog.Statistics.text = record.Statistics;
        int count = Mathf.Min(record.Party.Length, dialog.Heroes.Length);
        for (int i = 0; i < dialog.Heroes.Length; i++)
        {
            var card = dialog.Heroes[i]; card.Root.gameObject.SetActive(i < count);
            if (i >= count) continue;
            StyleHeroCard(card);
            Fit(card.Root, .04f + i * .92f / count, .36f, .04f + (i + 1) * .92f / count - .012f, .78f);
            var hero = record.Party[i];
            card.Name.text = hero.Name;
            card.Portrait.sprite = CampaignParty.Resolve(hero.Id, TownSceneLoader.Default)?.Portrait;
            card.Portrait.enabled = card.Portrait.sprite != null;
            card.Details.text = $"<b>Level {hero.Level} · {hero.PrimaryClass}</b>" +
                (string.IsNullOrEmpty(hero.SecondaryClass) ? "" : " / " + hero.SecondaryClass) +
                $"\nHP {hero.MaxHP}   Mana {hero.MaxMana}\n{hero.Weapon}";
        }
        dialog.CloseAction = () => Destroy(dialog.gameObject);
        return dialog;
    }

    private static void StyleHeroCard(HeroCard card)
    {
        var surface = GameUISkin.PanelGraphic(card.Root);
        if (surface != null)
        {
            surface.sprite = null;
            surface.type = Image.Type.Simple;
            surface.color = new Color(.20f, .27f, .31f);
            surface.raycastTarget = false;
        }
        card.Name.color = Color.white;
        card.Details.color = new Color(.93f, .94f, .91f);
    }

    private void Awake()
    {
        ContinueButton.onClick.AddListener(CloseDialog);
        SaveButton.onClick.AddListener(() =>
        {
            if (CampaignSaving.Commit(Common.Instance, "completed", null, Facing.Down, out var error))
            { SaveButton.GetComponentInChildren<TMP_Text>().text = "Campaign saved"; SaveButton.interactable = false; ContinueButton.Select(); }
            else Statistics.text = error;
        });
    }
    internal override void SetFirstSelect() => ContinueButton.Select();

#if UNITY_EDITOR
    public static CampaignSummaryDialog AuthorLayout()
    {
        var canvas = GameUISkin.Canvas("Hall of Fame", null, 150);
        GameUISkin.Rect("Input shield", canvas.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>().color = new Color(.035f, .055f, .09f, .96f);
        var dialog = canvas.gameObject.AddComponent<CampaignSummaryDialog>();
        var panel = GameUISkin.Panel(canvas.transform, new Vector2(.045f, .07f), new Vector2(.955f, .93f));
        var title = GameUISkin.Label(panel.transform, "HALL OF FAME", new Vector2(.25f, .87f), new Vector2(.75f, .98f), 52);
        title.alignment = TextAlignmentOptions.Center;
        GameUISkin.RibbonHeader(title);
        var subtitle = GameUISkin.Label(panel.transform, "The world is restored. Remember the heroes who brought you home.", new Vector2(.07f, .79f), new Vector2(.93f, .87f), 26);
        subtitle.alignment = TextAlignmentOptions.Center;
        dialog.Heroes = new HeroCard[4];
        for (int i = 0; i < dialog.Heroes.Length; i++)
        {
            var root = GameUISkin.Panel(panel.transform, new Vector2(.04f + i * .23f, .36f), new Vector2(.258f + i * .23f, .78f)).rectTransform;
            root.name = "Champion " + (i + 1);
            var name = GameUISkin.Label(root, "Hero", new Vector2(.04f, .83f), new Vector2(.96f, .97f), 30);
            name.alignment = TextAlignmentOptions.Center; name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 30;
            var portrait = GameUISkin.Rect("Portrait", root, new Vector2(.15f, .32f), new Vector2(.85f, .81f)).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            var details = GameUISkin.Label(root, "Level and equipment", new Vector2(.04f, .035f), new Vector2(.96f, .32f), 23);
            details.alignment = TextAlignmentOptions.Center; details.enableAutoSizing = true; details.fontSizeMin = 16; details.fontSizeMax = 23;
            dialog.Heroes[i] = new HeroCard { Root = root, Name = name, Portrait = portrait, Details = details };
            StyleHeroCard(dialog.Heroes[i]);
        }
        dialog.Statistics = GameUISkin.Label(panel.transform, "Campaign statistics", new Vector2(.05f, .17f), new Vector2(.95f, .33f), 27);
        dialog.Statistics.alignment = TextAlignmentOptions.Center; dialog.Statistics.enableAutoSizing = true;
        dialog.Statistics.fontSizeMin = 18; dialog.Statistics.fontSizeMax = 27;
        dialog.SaveButton = GameUISkin.Button(panel.transform, "Save completed campaign", new Vector2(.13f, .045f), new Vector2(.49f, .135f), null);
        dialog.ContinueButton = GameUISkin.Button(panel.transform, "Continue exploring", new Vector2(.51f, .045f), new Vector2(.87f, .135f), null);
        dialog.SaveButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = dialog.ContinueButton };
        dialog.ContinueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = dialog.SaveButton };
        canvas.gameObject.SetActive(false);
        return dialog;
    }
#endif
}
