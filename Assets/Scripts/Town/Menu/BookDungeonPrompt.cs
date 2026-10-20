using System;
using System.Linq;
using EternalEnigma.Core.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A book preview using the campaign browser's artwork card, without save-slot actions.</summary>
public sealed class BookDungeonPrompt : MonoBehaviour
{
    public CampaignSlotView Card;
    public TMP_Text Details, Requirement;
    public Button Enter, Back;
    private Action entered, canceled;
    private bool open;

    public static void Show(Town town, BookDungeonDefinition definition, Action enter, Action cancel)
    {
        var view = town.GetComponentInChildren<BookDungeonPrompt>(true);
        if (view == null)
        {
            var prefab = Resources.Load<BookDungeonPrompt>("UI/BookDungeonPrompt");
            if (prefab == null) throw new InvalidOperationException("Book dungeon prompt prefab is missing.");
            view = Instantiate(prefab, town.transform, false);
        }
        view.Setup(definition, enter, cancel);
    }

    private void Setup(BookDungeonDefinition definition, Action enter, Action cancel)
    {
        if (open) return;
        var context = Common.Instance.CampaignContext;
        bool available = context.CanEnterDungeon(definition.Id);
        bool cleared = context.Completed.Contains(definition.Id);
        var catalog = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
        Card.Artwork.texture = catalog.Backgrounds.FirstOrDefault(b => b.Biome == definition.Biome)?.Texture ?? catalog.Neutral;
        Card.Label.text = $"<b>{definition.Title}</b>\n<size=65%>{definition.Biome}</size>";
        Card.Button.enabled = false;
        Card.ClickAction = null;
        Card.Selected = null;
        foreach (var portrait in Card.Portraits) portrait.gameObject.SetActive(false);
        Details.text = "Floors 1–5  ·  " + (cleared ? "Cleared — enter again" : "Not yet cleared");
        Requirement.text = available ? "An illustrated country waits between these pages." :
            "Clear the starting town dungeon to enter this book.";
        Enter.interactable = available;
        Enter.onClick = new Button.ButtonClickedEvent();
        Back.onClick = new Button.ButtonClickedEvent();
        Enter.onClick.AddListener(EnterBook);
        Back.onClick.AddListener(Close);
        entered = enter; canceled = cancel; open = true;
        Enter.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = Back, selectOnRight = Back, selectOnUp = Back, selectOnDown = Back };
        var other = available ? Enter : Back;
        Back.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = other, selectOnRight = other, selectOnUp = other, selectOnDown = other };
        SelectToActivateButton.MarkCancel(Back);
        gameObject.SetActive(true);
        var first = available ? Enter : Back;
        MenuUIInputModule.Active?.PushDialog(this, transform, first.gameObject, Close);
        first.Select();
    }

    private void EnterBook()
    {
        if (open && Enter.interactable) Finish(true);
    }

    public void Close() => Finish(false);

    private void Finish(bool enter)
    {
        if (!open) return;
        open = false;
        var callback = enter ? entered : canceled;
        entered = canceled = null;
        MenuUIInputModule.Active?.PopDialog(this);
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    private void OnDisable()
    {
        // Scene teardown and externally closed panels must release the town input lock.
        if (!open) return;
        open = false;
        MenuUIInputModule.Active?.PopDialog(this);
        var callback = canceled;
        entered = canceled = null;
        callback?.Invoke();
    }
}
