using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ProtagonistClassPicker : MonoBehaviour
{
    private IReadOnlyList<ClassDefinition> classes;
    private Action<ClassDefinition, ClassDefinition> confirmed;
    private Action cancelled;
    private ClassDefinition primaryClass;
    [SerializeField] private Transform choices;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI details;
    public AuthoredButton ChoiceTemplate;
    public ProtagonistPreview Preview;
    public RawImage Portrait;
    public Button BackButton;
    private bool secondaryStep;
    private bool finished;

    public static ProtagonistClassPicker Show(IReadOnlyList<ClassDefinition> classes,Action<ClassDefinition,ClassDefinition> confirmed,Action cancelled)
    {
        if(classes==null || classes.Count==0){confirmed?.Invoke(null,null);return null;}
        var picker=AuthoredUI.Require<ProtagonistClassPicker>();picker.classes=classes;picker.confirmed=confirmed;picker.cancelled=cancelled;
        picker.finished=false;picker.secondaryStep=false;picker.primaryClass=null;picker.gameObject.SetActive(true);
        picker.BackButton.onClick.RemoveAllListeners();picker.BackButton.onClick.AddListener(picker.Back);
        var configuration=UnityEngine.Object.FindFirstObjectByType<MainMenu>()?.TownConfiguration ?? TownSceneLoader.Default;
        picker.Preview.Show(configuration?.StartingParty.FirstOrDefault(),picker.Portrait);
        picker.BuildChoices();MenuUIInputModule.Active?.PushDialog(picker,picker.transform,EventSystem.current?.currentSelectedGameObject,picker.Back);return picker;
    }
#if UNITY_EDITOR
    public static ProtagonistClassPicker AuthorLayout(IReadOnlyList<ClassDefinition> classes,
        Action<ClassDefinition, ClassDefinition> confirmed, Action cancelled)
    {

        var canvas = GameUISkin.Canvas("ProtagonistClassPicker", null, 1000);
        var picker = canvas.gameObject.AddComponent<ProtagonistClassPicker>();
        picker.classes = classes; picker.confirmed = confirmed; picker.cancelled = cancelled;
        GameUISkin.Panel(canvas.transform, Vector2.zero, Vector2.one).color = Color.white;
        GameUISkin.Label(canvas.transform, "ETERNAL ENIGMA  /  A NEW JOURNEY", new Vector2(.05f, .92f), new Vector2(.95f, .97f), 22);
        picker.title = GameUISkin.Label(canvas.transform, "Choose your primary class", new Vector2(.05f, .82f), new Vector2(.95f, .92f), 48);
        var portrait = GameUISkin.Panel(canvas.transform, new Vector2(.05f, .21f), new Vector2(.34f, .81f));
        var raw = GameUISkin.Rect("Protagonist", portrait.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        var preview = picker.Preview = raw.gameObject.AddComponent<ProtagonistPreview>();picker.Portrait=raw;
        var configuration = UnityEngine.Object.FindFirstObjectByType<MainMenu>()?.TownConfiguration ?? TownSceneLoader.Default;

        GameUISkin.Label(canvas.transform, "YOUR PROTAGONIST", new Vector2(.07f, .22f), new Vector2(.32f, .27f), 22).alignment = TextAlignmentOptions.Center;
        picker.details = GameUISkin.Label(canvas.transform, "", new Vector2(.38f, .51f), new Vector2(.94f, .81f), 26);
        picker.details.enableAutoSizing = true; picker.details.fontSizeMin = 20; picker.details.fontSizeMax = 28;
        picker.choices = GameUISkin.Rect("Classes", canvas.transform, new Vector2(.38f, .2f), new Vector2(.95f, .5f));
        GameUISkin.Label(canvas.transform,
            "Your choice shapes the protagonist's combat style. Explore the same campaign, recruit companions, and unlock routes through their abilities and your discoveries.",
            new Vector2(.05f, .095f), new Vector2(.72f, .18f), 24);
        picker.BackButton=GameUISkin.Button(canvas.transform, "Back", new Vector2(.05f, .025f), new Vector2(.2f, .087f), picker.Back);
        return picker;
    }
#endif

    private void BuildChoices()
    {
        foreach (Transform child in choices) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        title.text = secondaryStep ? "Choose a secondary class" : "Choose your primary class";
        var available = classes.Where(c => !secondaryStep || c.Id != primaryClass.Id).ToList();
        int count = available.Count + (secondaryStep ? 1 : 0);
        Button first = null;
        for (int i = 0; i < count; i++)
        {
            var cls = secondaryStep && i == 0 ? null : available[i - (secondaryStep ? 1 : 0)];
            var row=ChoiceTemplate.Spawn(choices,cls==null?"Begin with primary only":cls.DisplayName,()=>Choose(cls),cls==null?null:Resources.Load<Sprite>("UI/"+cls.DisplayName));
            var button=row.Button;button.GetComponent<ClassPickerFocus>().Focused=()=>ShowDetails(cls);
            first ??= button;
        }
        first?.Select();
        ShowDetails(secondaryStep ? null : available[0]);
    }

    private void Choose(ClassDefinition cls)
    {
        if (secondaryStep) { Finish(() => confirmed?.Invoke(primaryClass, cls)); return; }
        primaryClass = cls; secondaryStep = true; BuildChoices();
    }

    private void Back()
    {
        if (secondaryStep) { secondaryStep = false; BuildChoices(); }
        else Finish(cancelled);
    }

    private void ShowDetails(ClassDefinition cls)
    {
        var primary = secondaryStep ? primaryClass : cls;
        var skills = TownAlly.StartingSkillNames(primary, secondaryStep ? cls : null);
        string start = skills.Count > 0 ? string.Join(", ", skills) : "No class mastery";
        if (cls == null)
        {
            details.text = $"<size=36>{primary.DisplayName}</size>\n{primary.Role}\n\nStart with: {start}\nPrimary skills: all tiers, up to rank 5.\nYou can begin without a secondary class.";
            return;
        }
        string weapons = string.Join(", ", cls.AllowedWeapons);
        string tierOne = string.Join(", ", cls.Skills.Where(s => s != null && s.Skill != null && s.Tier == 1).Select(s => s.Skill.SkillName));
        details.text = $"<size=36>{cls.DisplayName}</size>  <color=#765535>{cls.Role}</color>\nWeapons: {weapons}\n" +
            (secondaryStep ? "Secondary: tiers 1-2, rank 3 maximum; no secondary masteries.\nPrimary keeps full progression.\n" :
            "Sets starting stat bonuses and growth each level.\nPrimary skills: all tiers, up to rank 5.\n") +
            $"Start with: {start}\n<size=22>Tier 1 training: {tierOne}</size>";
    }

    private void Finish(Action callback)
    {
        if (finished) return;
        finished = true; MenuUIInputModule.Active?.PopDialog(this);
        gameObject.SetActive(false); callback?.Invoke();
    }

    private void OnDestroy() => MenuUIInputModule.Active?.PopDialog(this);
}
