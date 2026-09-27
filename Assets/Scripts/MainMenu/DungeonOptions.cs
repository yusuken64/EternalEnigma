using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonOptions : MonoBehaviour
{
    private Button control;
    private Button speed;
    private TMP_Text explanation;
    public static void AddTo(GlobalSettings settings)
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
            () => { DungeonPreferences.FullControl = !DungeonPreferences.FullControl; script.Refresh(); });
        script.speed = GameUISkin.Button(panel.transform,"",new Vector2(.06f,.43f),new Vector2(.94f,.56f),
            () => { DungeonPreferences.AnimationMode = (DungeonAnimationMode)(((int)DungeonPreferences.AnimationMode+1)%3); script.Refresh(); });
        script.explanation = GameUISkin.Label(panel.transform,"",new Vector2(.06f,.10f),new Vector2(.94f,.37f),24);
        GameUISkin.Button(panel.transform,"Event history",new Vector2(.06f,.01f),new Vector2(.94f,.09f),() => { settings.Exit_Clicked(); GameMessages.ShowHistory(); });
        settings.TabGroup.AddTab(new TabContent { TabButton = button, Content = panel.gameObject });
        script.Refresh();
    }
    private void Refresh()
    {
        control.GetComponentInChildren<TMP_Text>().text = "Full Control: " + (DungeonPreferences.FullControl ? "On" : "Off");
        speed.GetComponentInChildren<TMP_Text>().text = "Game Speed: " + DungeonPreferences.SpeedLabel;
        explanation.text = "Full Control asks for each hero's action. Summons use AI. Changes take effect next round.\n\n" +
            (DungeonPreferences.AnimationMode == DungeonAnimationMode.Current ? "Current: animate visible actions." :
             DungeonPreferences.AnimationMode == DungeonAnimationMode.ControllingHero ? "Controlling hero: animate your hero and actions affecting them." :
             "Your action only: animate your command and its results. Other actions resolve instantly.");
    }
    private void OnEnable() { if (control != null) Refresh(); }
}
