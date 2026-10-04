using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonOptions : MonoBehaviour
{
    [SerializeField] private Button control;
    [SerializeField] private Button speed;
    [SerializeField] private TMP_Text explanation;
    private bool autoplayOwnsInput;
    #if UNITY_EDITOR
    public static void AuthorLayout(GlobalSettings settings)
    {
        if (settings.GetComponent<DungeonOptions>() != null || settings.TabGroup.TabContents.Count == 0) return;
        var script = settings.gameObject.AddComponent<DungeonOptions>();
        var first = settings.TabGroup.TabContents[0];
        var button = Instantiate(first.TabButton, first.TabButton.transform.parent);
        button.name = "Gameplay tab"; button.GetComponentInChildren<TMP_Text>().text = "Gameplay";
        button.onClick = new Button.ButtonClickedEvent();
        button.transform.SetSiblingIndex(settings.ReturntoMainButton.transform.GetSiblingIndex());
        var rect = (RectTransform)first.Content.transform;
        var panel = GameUISkin.Panel(rect.parent, rect.anchorMin, rect.anchorMax);
        panel.name = "Gameplay options"; panel.rectTransform.offsetMin = rect.offsetMin; panel.rectTransform.offsetMax = rect.offsetMax;
        GameUISkin.Label(panel.transform, "DUNGEON", new Vector2(.06f,.83f),new Vector2(.94f,.94f),32);
        script.control = GameUISkin.Button(panel.transform,"",new Vector2(.06f,.64f),new Vector2(.94f,.77f),
            () => { if (!AutoplayRunner.BlocksPlayerInput) DungeonPreferences.FullControl = !DungeonPreferences.FullControl; script.Refresh(); });
        script.speed = GameUISkin.Button(panel.transform,"",new Vector2(.06f,.43f),new Vector2(.94f,.56f),
            () => { DungeonPreferences.AnimationMode = (DungeonAnimationMode)(((int)DungeonPreferences.AnimationMode+1)%4); script.Refresh(); });
        script.explanation = GameUISkin.Label(panel.transform,"",new Vector2(.06f,.10f),new Vector2(.94f,.37f),24);
        GameUISkin.Button(panel.transform,"Event history",new Vector2(.06f,.01f),new Vector2(.94f,.09f),() => { settings.Exit_Clicked(); GameMessages.ShowHistory(); });
        settings.TabGroup.AddTab(new TabContent { TabButton = button, Content = panel.gameObject });
        // Data is bound at runtime.
    }
#endif
    public static void AddTo(GlobalSettings settings)
    {
        var view=settings.GetComponent<DungeonOptions>();
        if(view==null)throw new System.InvalidOperationException("Settings requires authored Gameplay options.");
        view.control.onClick.RemoveAllListeners(); view.speed.onClick.RemoveAllListeners();
        view.control.onClick.AddListener(()=> { if(!AutoplayRunner.BlocksPlayerInput)DungeonPreferences.FullControl=!DungeonPreferences.FullControl;view.Refresh(); });
        view.speed.onClick.AddListener(()=> { if(!AutoplayRunner.BlocksPlayerInput)DungeonPreferences.AnimationMode=(DungeonAnimationMode)(((int)DungeonPreferences.AnimationMode+1)%4);view.Refresh(); });
        view.Refresh();
    }
    private void Refresh()
    {
        autoplayOwnsInput = AutoplayRunner.BlocksPlayerInput;
        control.interactable = !autoplayOwnsInput;
        control.GetComponentInChildren<TMP_Text>().text = autoplayOwnsInput
            ? "Full Control: Off (Autoplay)"
            : "Full Control: " + (DungeonPreferences.FullControl ? "On" : "Off");
        speed.GetComponentInChildren<TMP_Text>().text = "Game Speed: " + DungeonPreferences.SpeedLabel;
        explanation.text = "Full Control asks for each hero's action. Press F or the controller's right stick to toggle it. Summons use AI. Changes take effect next round.\n\n" +
            (DungeonPreferences.AnimationMode == DungeonAnimationMode.Normal ? "Normal: animate visible actions." :
             DungeonPreferences.AnimationMode == DungeonAnimationMode.AnimateAlliedActions ? "Animate allied actions: animate allies and summons, plus enemy actions affecting an ally." :
             DungeonPreferences.AnimationMode == DungeonAnimationMode.AnimateControlledHeroActions ? "Animate only controlled hero actions: animate chains started by the hero you control when the action begins." :
             "No animations: actions and movement resolve instantly.");
    }
    private void OnEnable() { if (control != null) Refresh(); }
    private void Update()
    {
        // Options can stay open across takeover, completion, or runner destruction.
        // Update also runs at timeScale zero while playback is paused.
        if (control != null && autoplayOwnsInput != AutoplayRunner.BlocksPlayerInput)
        {
            Refresh();
            var settings = GetComponent<GlobalSettings>();
            if (settings != null) settings.TabGroup.RefreshNavigation();
        }
    }
}
