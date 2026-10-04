using System;
using UnityEngine;
using UnityEngine.UI;

// A modal choice that participates in the shared keyboard/controller input stack.
public sealed class CampaignChoice : MonoBehaviour
{
    public static void Show(string prompt, string confirm, Action action, Action cancel = null)
    {
        var canvas = GameUISkin.Canvas("Confirm", null, 2100);
        var choice = canvas.gameObject.AddComponent<CampaignChoice>();
        var shade = GameUISkin.Rect("Modal backdrop",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
        shade.color=new Color(0,0,0,.65f);
        var panel = GameUISkin.Panel(canvas.transform, new Vector2(.22f,.3f), new Vector2(.78f,.7f));
        GameUISkin.Label(panel.transform, prompt, new Vector2(.08f,.43f), new Vector2(.92f,.91f), 30);
        void Close(Action callback) { MenuUIInputModule.Active?.PopDialog(choice); Destroy(canvas.gameObject); callback?.Invoke(); }
        var yes = GameUISkin.Button(panel.transform, confirm, new Vector2(.08f,.12f), new Vector2(.58f,.32f), () => Close(action));
        var no = GameUISkin.Button(panel.transform, "Back", new Vector2(.62f,.12f), new Vector2(.92f,.32f), () => Close(cancel));
        MenuUIInputModule.Active?.PushDialog(choice, canvas.transform, no.gameObject, () => Close(cancel));
        no.Select();
    }
}
