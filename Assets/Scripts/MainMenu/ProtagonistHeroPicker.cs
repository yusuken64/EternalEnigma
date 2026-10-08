using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Lets a new campaign begin as an existing hero, with that hero's fixed classes.</summary>
public sealed class ProtagonistHeroPicker : MonoBehaviour
{
    private const int GridColumns = 6;
    private IReadOnlyList<TownAlly> heroes;
    private Action<TownAlly> confirmed;
    private Action cancelled;
    [SerializeField] private Transform choices;
    [SerializeField] private TextMeshProUGUI details;
    [SerializeField] private TextMeshProUGUI pageLabel;
    [SerializeField] private RawImage portrait;
    [SerializeField] private Button previous;
    [SerializeField] private Button next;
    [SerializeField] private ProtagonistPreview preview;
    public Button[] ChoiceButtons;
    public Button BackButton;
    private TownAlly selected;
    private bool finished;

    public static ProtagonistHeroPicker Show(IReadOnlyList<TownAlly> roster,Action<TownAlly> confirmed,Action cancelled)
    {
        if(roster==null || roster.Count==0 || roster.Any(h=>h==null || h.PrimaryClass==null))throw new ArgumentException("Each selectable hero needs a fixed primary class.");
        var picker=AuthoredUI.Require<ProtagonistHeroPicker>();
        picker.heroes=roster;picker.confirmed=confirmed;picker.cancelled=cancelled;picker.finished=false;picker.selected=null;
        picker.gameObject.SetActive(true);
        picker.BackButton.onClick.RemoveAllListeners();
        picker.BackButton.onClick.AddListener(picker.Back);
        picker.Select(roster[0]);picker.BuildChoices();
        var input = MenuUIInputModule.Active;
        if (input != null) input.PushDialog(picker,picker.transform,picker.ChoiceButtons[0].gameObject,picker.Back);
        else picker.ChoiceButtons[0].Select();
        return picker;
    }
#if UNITY_EDITOR
    public static ProtagonistHeroPicker AuthorLayout(IReadOnlyList<TownAlly> roster, Action<TownAlly> confirmed, Action cancelled)
    {
        var canvas = GameUISkin.Canvas("ProtagonistHeroPicker", null, 1000);
        var picker = canvas.gameObject.AddComponent<ProtagonistHeroPicker>();
        picker.heroes = roster; picker.confirmed = confirmed; picker.cancelled = cancelled;
        GameUISkin.Panel(canvas.transform, Vector2.zero, Vector2.one).color = Color.white;
        GameUISkin.Label(canvas.transform, "ETERNAL ENIGMA  /  A NEW JOURNEY", new Vector2(.05f, .92f), new Vector2(.95f, .97f), 22);
        GameUISkin.Label(canvas.transform, "Choose your hero", new Vector2(.05f, .82f), new Vector2(.95f, .92f), 48);

        var frame = GameUISkin.Panel(canvas.transform, new Vector2(.05f, .23f), new Vector2(.34f, .81f));
        picker.portrait = GameUISkin.Rect("Hero portrait", frame.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
        picker.portrait.raycastTarget = false;
        picker.preview = picker.portrait.gameObject.AddComponent<ProtagonistPreview>();
        picker.details = GameUISkin.Label(canvas.transform, "", new Vector2(.05f, .12f), new Vector2(.34f, .22f), 23);
        picker.details.enableAutoSizing = true; picker.details.fontSizeMin = 17;
        picker.choices = GameUISkin.Rect("Heroes", canvas.transform, new Vector2(.38f, .2f), new Vector2(.95f, .81f));
        picker.pageLabel = GameUISkin.Label(canvas.transform, "", new Vector2(.39f, .08f), new Vector2(.95f, .16f), 22);
        picker.pageLabel.alignment = TextAlignmentOptions.Center;
        picker.BackButton=GameUISkin.Button(canvas.transform, "Back", new Vector2(.05f, .025f), new Vector2(.2f, .087f), picker.Back);
        return picker;
    }
#endif

    private void BuildChoices()
    {
        if (previous != null) previous.gameObject.SetActive(false);
        if (next != null) next.gameObject.SetActive(false);
        pageLabel.text = $"{heroes.Count} HEROES     MOVE: PREVIEW     CONFIRM: CHOOSE";
        pageLabel.fontSize = 20;
        pageLabel.rectTransform.anchorMin = new Vector2(.39f, .08f);
        pageLabel.rectTransform.anchorMax = new Vector2(.95f, .16f);
        pageLabel.rectTransform.offsetMin = pageLabel.rectTransform.offsetMax = Vector2.zero;

        var gridBackground = choices.GetComponent<Image>() ?? choices.gameObject.AddComponent<Image>();
        gridBackground.color = new Color(.10f, .15f, .19f);
        gridBackground.raycastTarget = false;

        if (ChoiceButtons.Length < heroes.Count)
        {
            var expanded = new Button[heroes.Count];
            Array.Copy(ChoiceButtons, expanded, ChoiceButtons.Length);
            for (int i = ChoiceButtons.Length; i < expanded.Length; i++)
                expanded[i] = Instantiate(ChoiceButtons[0], choices, false);
            ChoiceButtons = expanded;
        }

        int columns = Mathf.Min(GridColumns, heroes.Count);
        int rows = Mathf.CeilToInt(heroes.Count / (float)columns);
        for (int i = 0; i < ChoiceButtons.Length; i++)
        {
            var button = ChoiceButtons[i];
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(i < heroes.Count);
            if (i >= heroes.Count) continue;

            var hero = heroes[i];
            button.name = hero.Name + "  -  " + hero.PrimaryClass.DisplayName;
            var rect = (RectTransform)button.transform;
            int column = i % columns, row = i / columns;
            rect.anchorMin = new Vector2((column + .04f) / columns, 1f - (row + .96f) / rows);
            rect.anchorMax = new Vector2((column + .96f) / columns, 1f - (row + .04f) / rows);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var colors = button.colors;
            colors.normalColor = new Color(.20f, .27f, .31f);
            colors.highlightedColor = colors.normalColor;
            colors.selectedColor = new Color(1f, .76f, .27f);
            colors.pressedColor = new Color(.92f, .57f, .20f);
            colors.fadeDuration = .08f;
            button.colors = colors;

            var portraitImage = button.transform.Find("Hero portrait")?.GetComponent<Image>();
            if (portraitImage == null)
            {
                var portraitObject = new GameObject("Hero portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(button.transform, false);
                portraitImage = portraitObject.GetComponent<Image>();
            }
            var portraitRect = portraitImage.rectTransform;
            portraitRect.anchorMin = new Vector2(.05f, .22f);
            portraitRect.anchorMax = new Vector2(.95f, .96f);
            portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
            portraitImage.sprite = hero.Portrait;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = hero.Name;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 20;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 20;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = new Vector2(.02f, .01f);
            label.rectTransform.anchorMax = new Vector2(.98f, .23f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.transform.SetAsLastSibling();

            button.onClick.AddListener(() => Begin(hero));
            button.GetComponent<ClassPickerFocus>().Focused = () =>
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != button.gameObject)
                    button.Select();
                Select(hero);
            };
        }

        for (int i = 0; i < heroes.Count; i++)
        {
            var button = ChoiceButtons[i];
            int column = i % columns;
            int up = i - columns, down = i + columns;
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = column > 0 ? ChoiceButtons[i - 1] : button,
                selectOnRight = column < columns - 1 && i + 1 < heroes.Count ? ChoiceButtons[i + 1] : button,
                selectOnUp = up >= 0 ? ChoiceButtons[up] : button,
                selectOnDown = down < heroes.Count ? ChoiceButtons[down] : BackButton
            };
        }
        BackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = ChoiceButtons[heroes.Count - 1], selectOnDown = ChoiceButtons[0],
            selectOnLeft = ChoiceButtons[0], selectOnRight = ChoiceButtons[0] };
    }

    private void Select(TownAlly hero)
    {
        if (finished || hero == null || selected == hero) return;
        selected = hero;
        preview.Show(hero, portrait);
        string classes = hero.PrimaryClass.DisplayName +
            (hero.SecondaryClass != null ? " / " + hero.SecondaryClass.DisplayName : "");
        details.text = $"<size=29>{hero.Name}</size>\n{classes}\n{hero.PrimaryClass.Role}";
    }

    private void Begin(TownAlly hero) => Finish(() => confirmed?.Invoke(hero));

    /// <summary>Swaps the skin's plain button for one that needs a select before it activates.</summary>

    private void Back() => Finish(cancelled);

    private void Finish(Action callback)
    {
        if (finished) return;
        finished = true;
        MenuUIInputModule.Active?.PopDialog(this);
        gameObject.SetActive(false);

        callback?.Invoke();
    }

    private void OnDestroy() => MenuUIInputModule.Active?.PopDialog(this);
}
