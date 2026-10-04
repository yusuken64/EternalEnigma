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
    private int page;
    private bool finished;

    public static ProtagonistHeroPicker Show(IReadOnlyList<TownAlly> roster,Action<TownAlly> confirmed,Action cancelled)
    {
        if(roster==null || roster.Count==0 || roster.Any(h=>h==null || h.PrimaryClass==null))throw new ArgumentException("Each selectable hero needs a fixed primary class.");
        var picker=AuthoredUI.Require<ProtagonistHeroPicker>();
        picker.heroes=roster;picker.confirmed=confirmed;picker.cancelled=cancelled;picker.finished=false;picker.page=0;picker.selected=null;
        picker.gameObject.SetActive(true);
        picker.previous.onClick.RemoveAllListeners();picker.next.onClick.RemoveAllListeners();picker.BackButton.onClick.RemoveAllListeners();
        picker.previous.onClick.AddListener(()=>picker.ChangePage(-1));picker.next.onClick.AddListener(()=>picker.ChangePage(1));picker.BackButton.onClick.AddListener(picker.Back);
        picker.Select(roster[0]);picker.BuildChoices();
        MenuUIInputModule.Active?.PushDialog(picker,picker.transform,EventSystem.current?.currentSelectedGameObject,picker.Back);
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
        picker.previous = GameUISkin.Button(canvas.transform, "Previous", new Vector2(.39f, .11f), new Vector2(.52f, .18f), () => picker.ChangePage(-1));
        picker.pageLabel = GameUISkin.Label(canvas.transform, "", new Vector2(.53f, .11f), new Vector2(.66f, .18f), 22);
        picker.pageLabel.alignment = TextAlignmentOptions.Center;
        picker.next = GameUISkin.Button(canvas.transform, "Next", new Vector2(.67f, .11f), new Vector2(.8f, .18f), () => picker.ChangePage(1));
        picker.BackButton=GameUISkin.Button(canvas.transform, "Back", new Vector2(.05f, .025f), new Vector2(.2f, .087f), picker.Back);
        return picker;
    }
#endif

    private void BuildChoices()
    {
        var visible=heroes.Skip(page*PageSize).Take(PageSize).ToArray();
        for(int i=0;i<ChoiceButtons.Length;i++)
        {
            var button=ChoiceButtons[i];button.onClick.RemoveAllListeners();button.gameObject.SetActive(i<visible.Length);
            if(i>=visible.Length)continue;
            var hero=visible[i];button.name=hero.Name+"  â€¢  "+hero.PrimaryClass.DisplayName;
            button.GetComponentInChildren<TMP_Text>().text=button.name;
            button.onClick.AddListener(()=>Begin(hero));button.GetComponent<ClassPickerFocus>().Focused=()=>Select(hero);
        }
        pageLabel.text=$"{page+1} / {Mathf.CeilToInt(heroes.Count/(float)PageSize)}";
        previous.interactable=page>0;next.interactable=(page+1)*PageSize<heroes.Count;
        ChoiceButtons[0].Select();
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
