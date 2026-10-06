using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

/// <summary>Free inn: rest to restore the party's HP/SP (damage persists between runs) or save the game.</summary>
public class InnDialog : Dialog
{
    public TextMeshProUGUI StatusText;
    public Button RestButton;
    public Button SaveButton;
    public Button CancelButton;

    private TownServices services;

    #if UNITY_EDITOR
    public static InnDialog AuthorLayout(Transform parent)
    {
        var panel = GameUISkin.Panel(parent, new Vector2(.25f, .25f), new Vector2(.75f, .75f));
        panel.name = "Inn";
        var dialog = panel.gameObject.AddComponent<InnDialog>();
        GameUISkin.Label(panel.transform, "Inn", new Vector2(.08f, .78f), new Vector2(.92f, .94f), 34);
        dialog.StatusText = GameUISkin.Label(panel.transform, "", new Vector2(.08f, .57f), new Vector2(.92f, .77f));
        dialog.RestButton = GameUISkin.Button(panel.transform, "Rest (free)", new Vector2(.1f, .39f), new Vector2(.9f, .53f), dialog.Rest_Clicked);
        dialog.SaveButton = GameUISkin.Button(panel.transform, "Save game", new Vector2(.1f, .22f), new Vector2(.9f, .36f), dialog.Save_Clicked);
        dialog.CancelButton = GameUISkin.Button(panel.transform, "Back", new Vector2(.1f, .05f), new Vector2(.9f, .19f), dialog.Cancel_Clicked);
        panel.gameObject.SetActive(false);
        return dialog;
    }
#endif

    public static InnDialog Create(Transform parent)=>AuthoredUI.Require<InnDialog>(parent);
    private void Awake()
    {
        RestButton.onClick.AddListener(Rest_Clicked);SaveButton.onClick.AddListener(Save_Clicked);CancelButton.onClick.AddListener(Cancel_Clicked);
    }
    public override void PrepareTown(TownInteractionContext context)
    {
        services = context.Services;
        StatusText.text = services.NeedsRest ? "Rest to restore your party's HP, SP and hunger?" : "Your party is fully rested.";
    }

    internal override void SetFirstSelect() => (services != null && services.NeedsRest ? RestButton : SaveButton).Select();

    public void Rest_Clicked()
    {
        services.Rest(out var reason);
        CloseDialog();
        TownMenu.ShowMessage(reason ?? "Your party is fully restored.");
    }

    public void Save_Clicked()
    {
        bool saved = services.SaveGame(out var error);
        CloseDialog();
        TownMenu.ShowMessage(saved ? "Game saved." : error);
    }

    public void Cancel_Clicked() => CloseDialog();
}
