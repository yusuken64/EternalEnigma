#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class EconomyShopAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Author Economy Shop")]
    public static void Author()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Town.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Additive);
        try
        {
            var shop = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopMenuDialog>(true)).Single();
            if (shop.BuyModeButton != null)
            {
                foreach (var hint in shop.GetComponentsInChildren<MenuControlHints>(true))
                {
                    var label = hint.GetComponent<TMPro.TMP_Text>();
                    if (label != null) label.fontSize = 18;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return;
            }
            foreach (Transform child in shop.transform)
                if (child != shop.BuyConfirmationDialog.transform) child.gameObject.SetActive(false);
            var panel = GameUISkin.Panel(shop.transform, new Vector2(.12f,.12f), new Vector2(.88f,.9f));
            shop.StatusText = GameUISkin.Label(panel.transform, "Shop", new Vector2(.05f,.82f), new Vector2(.95f,.97f), 30);
            shop.BuyModeButton = GameUISkin.Button(panel.transform, "Buy", new Vector2(.05f,.72f), new Vector2(.48f,.8f), null);
            shop.SellModeButton = GameUISkin.Button(panel.transform, "Sell", new Vector2(.52f,.72f), new Vector2(.95f,.8f), null);
            shop.BackButton = GameUISkin.Button(panel.transform, "Back", new Vector2(.72f,.02f), new Vector2(.95f,.10f), null);
            MenuControlHints.Bind(GameUISkin.Label(panel.transform, "", new Vector2(.05f,.02f), new Vector2(.7f,.10f), 18));
            var viewport = GameUISkin.Rect("Shop inventory", panel.transform, new Vector2(.05f,.13f), new Vector2(.95f,.69f));
            viewport.gameObject.AddComponent<Image>().color = new Color(1,1,1,.03f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = GameUISkin.Rect("Rows", viewport, new Vector2(0,1), Vector2.one);
            content.pivot = new Vector2(.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            shop.scrollView = viewport.gameObject.AddComponent<ScrollRect>();
            shop.scrollView.viewport = viewport; shop.scrollView.content = content;
            shop.scrollView.horizontal = false; shop.scrollView.movementType = ScrollRect.MovementType.Clamped;
            shop.scrollView.scrollSensitivity = 40;
            shop.Container = content;
            var row = GameUISkin.Button(null, "", Vector2.zero, Vector2.one, null);
            row.name = "Shop offer";
            foreach (var label in row.GetComponentsInChildren<TMPro.TMP_Text>()) Object.DestroyImmediate(label.gameObject);
            var view = row.gameObject.AddComponent<ShopMenuItem>(); view.BuyButton = row;
            view.ItemText = GameUISkin.Label(row.transform, "Item", new Vector2(.025f,.08f), new Vector2(.81f,.92f), 24);
            view.CostText = GameUISkin.Label(row.transform, "100G", new Vector2(.82f,.08f), new Vector2(.98f,.92f), 24);
            view.CostText.alignment = TMPro.TextAlignmentOptions.MidlineRight;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 82;
            shop.ShopItemPrefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, "Assets/Prefabs/Menu/EconomyShopItem.prefab").GetComponent<ShopMenuItem>();
            Object.DestroyImmediate(row.gameObject);
            shop.BuyConfirmationDialog.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
