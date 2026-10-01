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
    private const int PageSize = 8;
    private IReadOnlyList<TownAlly> heroes;
    private Action<TownAlly> confirmed;
    private Action cancelled;
    private Transform choices;
    private TextMeshProUGUI details;
    private TextMeshProUGUI pageLabel;
    private RawImage portrait;
    private Button previous;
    private Button next;
    private ProtagonistPreview preview;
    private TownAlly selected;
    private int page;
    private bool finished;

    public static ProtagonistHeroPicker Show(IReadOnlyList<TownAlly> roster, Action<TownAlly> confirmed, Action cancelled)
    {
        if (roster == null || roster.Count == 0) throw new ArgumentException("The hero roster is empty.", nameof(roster));
        if (roster.Any(hero => hero == null || hero.PrimaryClass == null))
            throw new ArgumentException("Each selectable hero needs a fixed primary class.", nameof(roster));

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
        picker.previous = GameUISkin.Button(canvas.transform, "Previous", new Vector2(.39f, .11f), new Vector2(.52f, .18f), () => picker.ChangePage(-1));
        picker.pageLabel = GameUISkin.Label(canvas.transform, "", new Vector2(.53f, .11f), new Vector2(.66f, .18f), 22);
        picker.pageLabel.alignment = TextAlignmentOptions.Center;
        picker.next = GameUISkin.Button(canvas.transform, "Next", new Vector2(.67f, .11f), new Vector2(.8f, .18f), () => picker.ChangePage(1));
        GameUISkin.Button(canvas.transform, "Back", new Vector2(.05f, .025f), new Vector2(.2f, .087f), picker.Back);
        picker.Select(roster[0]);
        picker.BuildChoices();
        MenuUIInputModule.Active?.PushDialog(picker, canvas.transform, EventSystem.current?.currentSelectedGameObject, picker.Back);
        return picker;
    }

    private void BuildChoices()
    {
        foreach (Transform child in choices) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var visible = heroes.Skip(page * PageSize).Take(PageSize).ToArray();
        Button first = null;
        for (int i = 0; i < visible.Length; i++)
        {
            var hero = visible[i];
            float x = (i % 2) * .51f, top = 1 - (i / 2) / 4f;
            var button = ToSelectToActivate(GameUISkin.Button(choices, hero.Name + "  •  " + hero.PrimaryClass.DisplayName,
                new Vector2(x, top - .88f / 4f), new Vector2(x + .49f, top), () => Begin(hero)));
            var focus = button.gameObject.AddComponent<ClassPickerFocus>();
            focus.Focused = () => Select(hero);
            first ??= button;
        }
        pageLabel.text = $"{page + 1} / {Mathf.CeilToInt(heroes.Count / (float)PageSize)}";
        previous.interactable = page > 0;
        next.interactable = (page + 1) * PageSize < heroes.Count;
        first?.Select();
    }

    private void ChangePage(int direction)
    {
        int target = page + direction;
        if (target < 0 || target * PageSize >= heroes.Count) return;
        page = target;
        BuildChoices();
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
    private static Button ToSelectToActivate(Button plain)
    {
        var go = plain.gameObject;
        var graphic = plain.targetGraphic;
        var colors = plain.colors;
        var transition = plain.transition;
        var sprites = plain.spriteState;
        var navigation = plain.navigation;
        var onClick = plain.onClick;
        DestroyImmediate(plain);
        var button = go.AddComponent<SelectToActivateButton>();
        button.targetGraphic = graphic; button.transition = transition;
        button.colors = colors; button.spriteState = sprites; button.navigation = navigation;
        button.onClick = onClick;
        return button;
    }
    private void Back() => Finish(cancelled);

    private void Finish(Action callback)
    {
        if (finished) return;
        finished = true;
        MenuUIInputModule.Active?.PopDialog(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void OnDestroy() => MenuUIInputModule.Active?.PopDialog(this);
}
