#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authors the book preview from the same card and frame assets as the campaign slot browser.</summary>
public static class BookDungeonAuthoring
{
    [MenuItem("Tools/Eternal Enigma/UI/Build Book Dungeon Prompt")]
    public static void BuildPrompt()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build the book prompt outside Play mode.");
        var canvas = GameUISkin.Canvas("Book dungeon preview", null, 2100);
        try
        {
            var view = canvas.gameObject.AddComponent<BookDungeonPrompt>();
            GameUISkin.Rect("Modal backdrop", canvas.transform, Vector2.zero, Vector2.one)
                .gameObject.AddComponent<Image>().color = new Color(0,0,0,.65f);
            var panel = GameUISkin.Panel(canvas.transform, new Vector2(.16f,.10f), new Vector2(.84f,.90f));
            var title = GameUISkin.Label(panel.transform,"Enter the story",new Vector2(.05f,.89f),new Vector2(.95f,.98f),36);
            title.alignment = TextAlignmentOptions.Center;
            GameUISkin.RibbonHeader(title);
            var cardObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/CampaignSlotButton.prefab"),panel.transform);
            cardObject.name = "Illustrated book card";
            var rect = (RectTransform)cardObject.transform;
            rect.anchorMin = new Vector2(.06f,.31f); rect.anchorMax = new Vector2(.94f,.87f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var button = cardObject.GetComponent<Button>();
            button.GetComponentInChildren<TMP_Text>().text = "";
            button.enabled = false;
            view.Card = cardObject.GetComponent<CampaignSlotView>() ?? cardObject.AddComponent<CampaignSlotView>();
            view.Card.Button = button;
            view.Card.SelectToActivateButton = button as SelectToActivateButton;
            view.Card.Portraits = Array.Empty<Image>();
            view.Card.Artwork = GameUISkin.Rect("Biome artwork",cardObject.transform,new Vector2(.015f,.025f),new Vector2(.985f,.975f))
                .gameObject.AddComponent<RawImage>();
            view.Card.Artwork.raycastTarget = false;
            var backgrounds = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
            view.Card.Artwork.texture = backgrounds.Neutral;
            var shade = GameUISkin.Rect("Information shade",cardObject.transform,new Vector2(.015f,.025f),new Vector2(.985f,.975f))
                .gameObject.AddComponent<Image>();
            shade.color = new Color(0,0,0,.38f); shade.raycastTarget = false;
            view.Card.Label = GameUISkin.Label(cardObject.transform,"<b>The Other Country</b>\n<size=65%>A country between the pages</size>",
                new Vector2(.055f,.64f),new Vector2(.945f,.93f),42);
            view.Card.Label.color = Color.white;
            view.Card.Label.alignment = TextAlignmentOptions.TopLeft;
            view.Card.Label.enableAutoSizing = true; view.Card.Label.fontSizeMin = 28; view.Card.Label.fontSizeMax = 42;
            var shadow = view.Card.Label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.04f,.06f,.08f,.85f); shadow.effectDistance = new Vector2(1.5f,-1.5f);
            view.Details = GameUISkin.Label(panel.transform,"Floors 1–5 · Not yet cleared",new Vector2(.08f,.225f),new Vector2(.92f,.295f),29);
            view.Details.alignment = TextAlignmentOptions.Center;
            view.Requirement = GameUISkin.Label(panel.transform,"An illustrated country waits between these pages.",
                new Vector2(.08f,.14f),new Vector2(.92f,.225f),25);
            view.Requirement.alignment = TextAlignmentOptions.Center;
            view.Requirement.enableAutoSizing = true; view.Requirement.fontSizeMin = 20; view.Requirement.fontSizeMax = 25;
            view.Enter = GameUISkin.Button(panel.transform,"Enter",new Vector2(.10f,.045f),new Vector2(.46f,.125f),null);
            view.Back = GameUISkin.Button(panel.transform,"Back",new Vector2(.54f,.045f),new Vector2(.90f,.125f),null,
                SelectToActivateButton.SoundRole.Cancel);
            canvas.gameObject.SetActive(false);
            const string path = "Assets/Resources/UI/BookDungeonPrompt.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject,path);
            if (saved == null || saved.GetComponent<BookDungeonPrompt>() == null)
                throw new InvalidOperationException("Failed to save the authored book dungeon prompt at " + path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (Resources.Load<BookDungeonPrompt>("UI/BookDungeonPrompt") == null)
                throw new InvalidOperationException("The saved book dungeon prompt could not be loaded from Resources.");
        }
        finally { Object.DestroyImmediate(canvas.gameObject); }
        Debug.Log("Authored the illustrated book dungeon preview using campaign slot artwork and card styling.");
    }
}
#endif
