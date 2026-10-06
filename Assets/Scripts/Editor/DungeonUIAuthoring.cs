using System;
using System.IO;
using System.Linq;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DungeonUIAuthoring
{
    const string Folder = "Assets/Resources/UI/Dungeon";
    static GameUITheme Theme => GameUITheme.Current;
    static void Fit(Transform transform, float x, float y, float right, float top)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(x,y); rect.anchorMax = new Vector2(right,top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static Image Role(Image image, DungeonVisualRole value)
    {
        var role = image.GetComponent<DungeonUIRole>() ?? image.gameObject.AddComponent<DungeonUIRole>();
        role.Role = value; role.Apply(); EditorUtility.SetDirty(image);
        if (PrefabUtility.IsPartOfPrefabInstance(image)) PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        return image;
    }
    static void ButtonRole(Button button, DungeonVisualRole value)
    { Role((Image)button.targetGraphic,value); Theme.StyleButton(button); }
    static Image Surface(Transform parent, string name, DungeonVisualRole role, float x, float y, float r, float t)
    {
        var rect = GameUISkin.Rect(name,parent,new Vector2(x,y),new Vector2(r,t));
        var image = Role(rect.gameObject.AddComponent<Image>(),role); image.raycastTarget=false;
        return image;
    }
    static Sprite Sprite(string path, int border=12)
    {
        path = "Assets/Bamao/BamaoUIPack/Sprites/"+path+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var wanted=new Vector4(border,border,border,border);
        if(importer.spriteBorder!=wanted){importer.spriteBorder=wanted;importer.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static GameObject Variant(string source,string name,Action<GameObject> change)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var image in root.GetComponentsInChildren<Image>(true).Where(i=>i.sprite==Theme.Panel))Role(image,DungeonVisualRole.Paper);
        foreach(var button in root.GetComponentsInChildren<Button>(true))ButtonRole(button,button is TrainerPreviewScroll?DungeonVisualRole.InputSurface:DungeonVisualRole.Secondary);
        try { root.name=name;change(root);Record(root);return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab"); }
        finally {Object.DestroyImmediate(root);}
    }
    static void Record(GameObject root)
    {
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var style=text.GetComponent<DungeonTextStyle>()??text.gameObject.AddComponent<DungeonTextStyle>();
            style.OnWood=text.color==GameUITheme.LightInk;
        }
        foreach(var component in root.GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }
    static void Shield(GameObject root)
    {
        foreach(var image in root.GetComponentsInChildren<Image>(true).Where(i=>i.name=="Input shield")) image.color=Color.clear;
    }
    static void Dock(Transform panel,Transform root)
    {
        var safe=root.Find("Dock safe area") ?? GameUISkin.Rect("Dock safe area",root,Vector2.zero,Vector2.one);
        if(safe.GetComponent<SafeAreaPanel>()==null)safe.gameObject.AddComponent<SafeAreaPanel>();
        panel.SetParent(safe,false);Fit(panel,.60f,.03f,.98f,.88f);
        Role(GameUISkin.PanelGraphic(panel),DungeonVisualRole.Wood);
    }
    static void CloseButton(Transform panel,Dialog dialog)
    {
        var button=GameUISkin.Button(panel,"",new Vector2(.91f,.92f),new Vector2(.985f,.985f),null);
        ButtonRole(button,DungeonVisualRole.Close); button.name="Close";
        button.gameObject.AddComponent<DungeonDialogClose>();
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(.98f,.98f);rect.pivot=Vector2.one;rect.sizeDelta=new Vector2(40,40);
    }
    static AuthoredButton Row(string name,bool hero=false,bool plain=false)
    {
        return Variant("Assets/Resources/UI/Authored/"+(hero?"PartyHero":plain?"PartyPlainEntry":"PartyEntry")+".prefab",name,root=>
        {
            var row=root.GetComponent<AuthoredButton>();ButtonRole(row.Button,hero?DungeonVisualRole.WoodButton:DungeonVisualRole.Secondary);
            row.Label.fontSize=24;row.Label.fontSizeMin=20;row.Label.fontSizeMax=24;
            root.GetComponent<LayoutElement>().preferredHeight=hero?64:58;
            if(row.Icon!=null)
            {
                foreach(var image in root.GetComponentsInChildren<Image>(true))if(image!=row.Icon && image!=row.Button.targetGraphic)image.enabled=false;
                var slot=GameUISkin.Rect("Square icon",root.transform,Vector2.zero,Vector2.one);
                slot.anchorMin=slot.anchorMax=new Vector2(0,.5f);slot.pivot=new Vector2(0,.5f);slot.sizeDelta=new Vector2(48,48);slot.anchoredPosition=new Vector2(8,0);
                Surface(slot,"Icon frame",DungeonVisualRole.IconFrame,0,0,1,1);
                row.Icon.transform.SetParent(slot,false);Fit(row.Icon.transform,.12f,.12f,.88f,.88f);row.Icon.preserveAspect=true;
                Fit(row.Label.transform,0,0,1,1);row.Label.rectTransform.offsetMin=new Vector2(64,0);row.Label.rectTransform.offsetMax=new Vector2(-8,0);
                var selected=Surface(slot,"Selection",DungeonVisualRole.Selection,0,0,1,1);
                selected.enabled=false;root.AddComponent<DungeonRowSelection>().Frame=selected;
            }
        }).GetComponent<AuthoredButton>();
    }
    [MenuItem("Tools/Eternal Enigma/UI/Author Dungeon Dock")]
    public static void Build()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();Directory.CreateDirectory(Folder);
        var originalSetup=EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
        Theme.DungeonWood=Sprite("Button/Popup_wood_bg",32);Theme.DungeonHeading=Sprite("Button/Popup_paper_title",24);
        Theme.DungeonPaper=Sprite("Button/Popup_paper_bg",20);Theme.DungeonWoodButton=Sprite("Button/wood button_3",16);
        Theme.DungeonPrimary=Sprite("Button/paper button green",16);Theme.DungeonSecondary=Sprite("Button/paper button white",16);
        Theme.DungeonClose=Sprite("Character sheet/Button_cross_B",0);Theme.DungeonIconFrame=Sprite("Equipment 2/F_Brown",0);
        Theme.DungeonSelection=Sprite("Equipment 2/F_select frame",0);Theme.DungeonTrack=Sprite("Select Character/State_blank",8);
        Theme.DungeonHP=Sprite("Select Character/State_HP",8);Theme.DungeonSP=Sprite("Select Character/State_blue",8);Theme.DungeonHunger=Sprite("Select Character/State_yellow",8);
        Theme.DungeonRolePrefabs=Enum.GetValues(typeof(DungeonVisualRole)).Cast<DungeonVisualRole>().Select(role=>
        {
            var image=Surface(null,role.ToString(),role,0,0,1,1);
            try{return PrefabUtility.SaveAsPrefabAsset(image.gameObject,Folder+"/Role"+role+".prefab");}
            finally{Object.DestroyImmediate(image.gameObject);}
        }).ToArray();
        var hero=Row("Hero",true);var entry=Row("Entry");var plain=Row("PlainEntry",plain:true);
        var section=Variant("Assets/Resources/UI/Authored/Section.prefab","Section",root=>
        {
            var text=root.GetComponent<TMP_Text>();text.fontSize=24;text.color=GameUITheme.LightInk;text.margin=new Vector4(8,4,8,4);
        }).GetComponent<TMP_Text>();
        var empty=Variant("Assets/Resources/UI/Authored/Empty.prefab","Empty",root=>
        {var text=root.GetComponent<TMP_Text>();text.color=GameUITheme.LightInk;text.fontSize=24;}).GetComponent<TMP_Text>();
        Theme.DungeonMenuPrefab=Variant("Assets/Resources/UI/PartyMenu.prefab","PartyMenu",root=>
        {
            var menu=root.GetComponent<PartyMenu>();Shield(root);Fit(menu.Panel,.60f,.03f,.98f,.88f);
            Role(GameUISkin.PanelGraphic(menu.Panel),DungeonVisualRole.Wood);
            menu.HeroTemplate=hero;menu.EntryTemplate=entry;menu.PlainEntryTemplate=plain;
            menu.HeadingTemplate=section;menu.EmptyTemplate=empty;
            menu.HeroesRoot.gameObject.SetActive(false);
            Fit(menu.HeroText.transform,.05f,.79f,.95f,.88f);menu.HeroText.fontSize=24;menu.HeroText.color=GameUITheme.LightInk;
            Fit(menu.InventoryTab.transform,.035f,.70f,.495f,.78f);Fit(menu.SkillsTab.transform,.505f,.70f,.965f,.78f);
            ButtonRole(menu.InventoryTab,DungeonVisualRole.WoodButton);ButtonRole(menu.SkillsTab,DungeonVisualRole.WoodButton);
            Fit(menu.scrollView.transform,.035f,.41f,.965f,.685f);
            Fit(menu.DetailsControl.Scroll.transform,.055f,.14f,.945f,.38f);
            var paper=Surface(menu.Panel,"Description parchment",DungeonVisualRole.Paper,.035f,.12f,.965f,.40f);
            paper.transform.SetSiblingIndex(menu.DetailsControl.Scroll.transform.GetSiblingIndex());
            menu.Details.fontSize=24;menu.Details.color=GameUITheme.Ink;menu.Details.margin=new Vector4(8,4,8,4);
            Fit(menu.BackButton.transform,.035f,.025f,.29f,.09f);ButtonRole(menu.BackButton,DungeonVisualRole.Secondary);
            Fit(menu.Hints.transform,.31f,.018f,.965f,.105f);menu.Hints.fontSize=20;menu.Hints.color=GameUITheme.LightInk;
            menu.Hints.GetComponent<MenuControlHints>().DungeonDock=true;
            foreach(var label in menu.Panel.GetComponentsInChildren<TMP_Text>(true))
                if(label.transform.parent==menu.Panel && label!=menu.HeroText && label!=menu.Hints && label!=menu.Details && label.transform!=menu.BackButton.transform)
                { if(label.rectTransform.anchorMin.y>.8f) label.gameObject.SetActive(false); }
            var ribbon=Surface(menu.Panel,"Heading ribbon",DungeonVisualRole.Heading,.035f,.88f,.88f,.995f);ribbon.transform.SetSiblingIndex(1);
            var title=GameUISkin.Label(menu.Panel,"Party",new Vector2(.1f,.905f),new Vector2(.83f,.97f),30);title.font=Theme.HeadingFont;title.alignment=TextAlignmentOptions.Center;
            title.enableAutoSizing=true;title.fontSizeMin=24;title.fontSizeMax=30;
            CloseButton(menu.Panel,menu);
        });
        Theme.DungeonPickerPrefab=Variant("Assets/Resources/UI/Authored/PartyPicker.prefab","PartyPicker",root=>
        {
            var picker=root.GetComponent<PartyMenuPicker>();Shield(root);Dock(picker.Title.transform.parent,root.transform);
            picker.RowTemplate=plain;ButtonRole(picker.BackButton,DungeonVisualRole.Secondary);
            var title=picker.Title;Fit(title.transform,.06f,.85f,.87f,.97f);title.color=GameUITheme.LightInk;
            CloseButton(title.transform.parent,picker);
        });
        Theme.DungeonInventoryPrefab=BuildInventory();
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/DungeonScene.unity");
            var manager=Object.FindFirstObjectByType<MenuManager>();
            var party=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PartyMenu>(true)).Single();
            Replace(party.gameObject,Theme.DungeonMenuPrefab);
            foreach(var picker in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PartyMenuPicker>(true)).ToArray())Replace(picker.gameObject,Theme.DungeonPickerPrefab);
            var action=manager.ActionDialog;
            // Save a separate variant; town's action menu keeps its original geometry.
            var actionSource=PrefabUtility.GetCorrespondingObjectFromSource(action.gameObject);
            string source=AssetDatabase.GetAssetPath(actionSource);
            if(source.StartsWith(Folder) || !File.Exists(source))
            {
                source=Folder+"/ItemActionsSource.prefab";
                if(!File.Exists(source))PrefabUtility.SaveAsPrefabAsset(action.gameObject,source);
            }
            Theme.DungeonActionsPrefab=Variant(source,"ItemActions",root=>
            {
                var dialog=root.GetComponent<ActionDialog>();Dock(dialog.Panel.transform,root.transform);
                root.GetComponent<Canvas>().sortingOrder=120;
                var scaler=root.GetComponent<CanvasScaler>();scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                var inner=dialog.Buttons[0].transform.parent.parent;Fit(inner,0,0,1,1);
                dialog.ItemNameText.transform.SetParent(dialog.Panel.transform,false);
                Fit(dialog.ItemNameText.transform,.07f,.80f,.88f,.92f);dialog.ItemNameText.fontSize=32;dialog.ItemNameText.color=GameUITheme.LightInk;
                dialog.ItemNameText.alignment=TextAlignmentOptions.Center;dialog.ItemNameText.enableAutoSizing=true;dialog.ItemNameText.fontSizeMin=24;dialog.ItemNameText.fontSizeMax=32;
                var buttons=dialog.Buttons[0].transform.parent;Fit(buttons,.08f,.20f,.92f,.76f);
                var layout=buttons.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=18;
                for(int i=0;i<dialog.Buttons.Count;i++)
                {
                    var button=dialog.Buttons[i];ButtonRole(button,i==0?DungeonVisualRole.Primary:DungeonVisualRole.Secondary);
                    var height=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();height.preferredHeight=70;
                    var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=28;label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=28;
                }
                var hint=GameUISkin.Label(dialog.Panel.transform,"Choose an action\nCancel returns to your inventory",new Vector2(.08f,.08f),new Vector2(.92f,.18f),22);hint.color=GameUITheme.LightInk;
                var shield=GameUISkin.Rect("Input shield",root.transform,Vector2.zero,Vector2.one);shield.gameObject.AddComponent<Image>().color=Color.clear;shield.SetAsFirstSibling();
                CloseButton(dialog.Panel.transform,dialog);
            });
            manager.ActionDialog=Replace(action.gameObject,Theme.DungeonActionsPrefab).GetComponent<ActionDialog>();
            manager.InventoryMenu=Replace(manager.InventoryMenu.gameObject,Theme.DungeonInventoryPrefab).GetComponent<InventoryMenu>();
            Object.FindFirstObjectByType<Game>().InventoryMenu=manager.InventoryMenu;
            manager.InventoryMenu.ActionDialog=manager.ActionDialog;
            foreach(var slider in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DungeonPartyCard>(true)).SelectMany(c=>c.GetComponentsInChildren<Slider>(true)))
            {
                Role(GameUISkin.PanelGraphic(slider.transform),DungeonVisualRole.Track);
                var display=slider.GetComponentInParent<CharacterStatsDisplay>(true);
                Role(slider.fillRect.GetComponent<Image>(),slider==display.HpDisplay.StatValueSlider?DungeonVisualRole.HP:slider==display.SpDisplay.StatValueSlider?DungeonVisualRole.SP:DungeonVisualRole.Hunger);
            }
            foreach(var hud in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DungeonHud>(true)))
            {
                foreach(var button in hud.PartyRoot.parent.GetComponentsInChildren<Button>(true))ButtonRole(button,DungeonVisualRole.WoodButton);
                var header=(TMP_Text)new SerializedObject(hud).FindProperty("header").objectReferenceValue;
                header.transform.SetParent(hud.PartyRoot.parent,false);
                Fit(header.transform,.61f,.90f,.72f,.94f);header.fontSize=28;header.alignment=TextAlignmentOptions.Center;header.color=GameUITheme.LightInk;
                var backing=hud.PartyRoot.parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="Floor label parchment");
                if(backing!=null)backing.gameObject.SetActive(false);
                var serialized=new SerializedObject(hud);
                var target=(TMP_Text)serialized.FindProperty("target").objectReferenceValue;
                var targetPanel=(GameObject)serialized.FindProperty("targetBackdrop").objectReferenceValue;
                Fit(targetPanel.transform,.32f,.835f,.60f,.895f);target.fontSize=24;target.enableAutoSizing=true;target.fontSizeMin=20;target.fontSizeMax=24;
                Fit(target.transform,.04f,.08f,.96f,.92f);target.alignment=TextAlignmentOptions.Center;
            }
            Fit(manager.TargetDialog.SelectTargetPrompt.transform,.32f,.90f,.60f,.98f);
            var targetScaler=manager.TargetDialog.GetComponent<CanvasScaler>()??manager.TargetDialog.gameObject.AddComponent<CanvasScaler>();
            targetScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;targetScaler.referenceResolution=new Vector2(1920,1080);targetScaler.matchWidthOrHeight=.5f;
            var targetPrompt=manager.TargetDialog.SelectTargetPrompt.GetComponentInChildren<TMP_Text>(true);
            targetPrompt.fontSize=28;targetPrompt.enableAutoSizing=true;targetPrompt.fontSizeMin=24;targetPrompt.fontSizeMax=28;
            Fit(targetPrompt.transform,.04f,.10f,.96f,.90f);targetPrompt.alignment=TextAlignmentOptions.Center;
            var resources=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ResourceHUD>(true)).Single();
            var resourcePanel=resources.GoldText.transform.parent;
            Fit(resourcePanel,.73f,.89f,.98f,.935f);
            Fit(resources.GoldText.transform,.10f,.05f,.49f,.95f);Fit(resources.BagText.transform,.60f,.05f,.98f,.95f);
            resources.GoldText.fontSize=22;resources.BagText.fontSize=22;
            resources.GoldText.enableAutoSizing=resources.BagText.enableAutoSizing=true;
            resources.GoldText.fontSizeMin=resources.BagText.fontSizeMin=18;
            resources.GoldText.fontSizeMax=resources.BagText.fontSizeMax=22;
            Fit(resources.CoinIcon.transform,.015f,.15f,.09f,.85f);Fit(resources.BagIcon.transform,.51f,.15f,.59f,.85f);
            foreach(var display in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CharacterStatsDisplay>(true)))
            {
                var portrait=display.PortraitImage;if(portrait==null)continue;portrait.raycastTarget=true;
                if(portrait.GetComponent<Canvas>()==null)portrait.gameObject.AddComponent<Canvas>();
                if(portrait.GetComponent<GraphicRaycaster>()==null)portrait.gameObject.AddComponent<GraphicRaycaster>();
                if(portrait.GetComponent<DungeonPortraitControl>()==null)portrait.gameObject.AddComponent<DungeonPortraitControl>();
            }
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var label in root.GetComponentsInChildren<DungeonHud>(true).SelectMany(h=>h.PartyRoot.parent.GetComponentsInChildren<TMP_Text>(true)))
                {
                    var style=label.GetComponent<DungeonTextStyle>()??label.gameObject.AddComponent<DungeonTextStyle>();
                    style.OnWood=label.color==GameUITheme.LightInk;
                }
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                    if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        BuildHistory();EditorUtility.SetDirty(Theme);AssetDatabase.SaveAssets();
        Debug.Log("Dungeon dock and visual roles authored; shared travel menus retained.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(originalSetup); }
    }
    static GameObject Replace(GameObject old,GameObject prefab)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.transform.parent);root.SetActive(old.activeSelf);
        root.transform.SetSiblingIndex(old.transform.GetSiblingIndex());Object.DestroyImmediate(old);return root;
    }
    static GameObject BuildInventory()
    {
        var row=Variant("Assets/Prefabs/Menu/InventoryMenuItem.prefab","InventoryItem",root=>
        {
            var item=root.GetComponent<InventoryMenuItem>();ButtonRole(item,DungeonVisualRole.Secondary);
            item.ItemText.fontSize=24;item.ItemText.enableAutoSizing=true;item.ItemText.fontSizeMin=20;item.ItemText.fontSizeMax=24;
            var size=root.GetComponent<LayoutElement>()??root.AddComponent<LayoutElement>();size.preferredHeight=58;
        }).GetComponent<InventoryMenuItem>();
        return Variant("Assets/Prefabs/Menu/InventoryMenu.prefab","InventoryPicker",root=>
        {
            var menu=root.GetComponent<InventoryMenu>();menu.DungeonDock=true;menu.InventoryMenuItemPrefab=row;
            foreach(Transform child in root.transform)child.gameObject.SetActive(false);
            root.GetComponent<Canvas>().sortingOrder=125;
            var scaler=root.GetComponent<CanvasScaler>();scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            GameUISkin.Rect("Input shield",root.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=Color.clear;
            var panel=Surface(root.transform,"Inventory picker panel",DungeonVisualRole.Wood,0,0,1,1).rectTransform;Dock(panel,root.transform);
            Surface(panel,"Heading ribbon",DungeonVisualRole.Heading,.04f,.89f,.89f,.98f);
            var title=GameUISkin.Label(panel,"Choose an item",new Vector2(.08f,.89f),new Vector2(.85f,.98f),30);title.font=Theme.HeadingFont;title.alignment=TextAlignmentOptions.Center;
            menu.scrollView.transform.SetParent(panel,false);menu.scrollView.gameObject.SetActive(true);Fit(menu.scrollView.transform,.04f,.44f,.96f,.86f);
            Role(GameUISkin.PanelGraphic(menu.scrollView.transform),DungeonVisualRole.Paper);
            Fit(menu.scrollView.content,0,1,1,1);menu.scrollView.content.pivot=new Vector2(.5f,1);
            var list=menu.MenuItemContainer.GetComponent<VerticalLayoutGroup>();list.padding=new RectOffset(12,12,12,12);list.childControlHeight=true;
            menu.EmptyMessage.transform.SetParent(panel,false);Fit(menu.EmptyMessage.transform,.06f,.62f,.94f,.74f);
            menu.EmptyMessage.GetComponentInChildren<TMP_Text>().color=GameUITheme.LightInk;
            menu.StatText.transform.SetParent(panel,false);menu.StatText.gameObject.SetActive(true);Fit(menu.StatText.transform,.06f,.33f,.94f,.43f);
            menu.StatText.fontSize=22;menu.StatText.color=GameUITheme.LightInk;menu.StatText.enableAutoSizing=true;menu.StatText.fontSizeMin=18;menu.StatText.fontSizeMax=22;
            var paper=Surface(panel,"Description parchment",DungeonVisualRole.Paper,.04f,.12f,.96f,.32f);
            var viewport=GameUISkin.Rect("Description viewport",paper.transform,new Vector2(.04f,.06f),new Vector2(.96f,.94f));viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var text=menu.InventoryItemPreview.ItemText;text.transform.SetParent(viewport,false);text.gameObject.SetActive(true);Fit(text.transform,0,1,1,1);text.rectTransform.pivot=new Vector2(.5f,1);text.fontSize=24;text.color=GameUITheme.Ink;
            var fitter=text.GetComponent<ContentSizeFitter>()??text.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=text.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
            menu.DetailsControl=viewport.gameObject.AddComponent<TrainerPreviewScroll>();menu.DetailsControl.Scroll=scroll;menu.DetailsControl.targetGraphic=hit;
            ButtonRole(menu.DetailsControl,DungeonVisualRole.InputSurface);
            var back=GameUISkin.Button(panel,"Back",new Vector2(.04f,.025f),new Vector2(.30f,.10f),null);ButtonRole(back,DungeonVisualRole.Secondary);back.gameObject.AddComponent<DungeonDialogClose>();
            var hint=GameUISkin.Label(panel,"Select: choose item\nCancel: back   Right: details",new Vector2(.34f,.025f),new Vector2(.96f,.105f),20);hint.color=GameUITheme.LightInk;
            CloseButton(panel,menu);
        });
    }
    static void BuildHistory()
    {
        var canvas=GameUISkin.Canvas("Event history",null,130);
        try
        {
            GameUISkin.Rect("Input shield",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=Color.clear;
            var safe=GameUISkin.Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaPanel>();
            var dialog=canvas.gameObject.AddComponent<EventHistoryDialog>();
            dialog.Panel=Surface(safe,"History panel",DungeonVisualRole.Wood,.6f,.03f,.98f,.88f).rectTransform;
            Surface(dialog.Panel,"Heading ribbon",DungeonVisualRole.Heading,.04f,.88f,.89f,.98f);
            var title=GameUISkin.Label(dialog.Panel,"Event history",new Vector2(.08f,.885f),new Vector2(.85f,.975f),32);title.font=Theme.HeadingFont;title.alignment=TextAlignmentOptions.Center;
            var paper=Surface(dialog.Panel,"History parchment",DungeonVisualRole.Paper,.04f,.13f,.96f,.87f);
            var viewport=GameUISkin.Rect("Viewport",paper.transform,new Vector2(.06f,.04f),new Vector2(.94f,.96f));viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
            dialog.Entries=GameUISkin.Label(viewport,"No events yet.",new Vector2(0,1),Vector2.one,24);dialog.Entries.rectTransform.pivot=new Vector2(.5f,1);
            var scroll=dialog.scrollView=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=dialog.Entries.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
            dialog.Reader=viewport.gameObject.AddComponent<TrainerPreviewScroll>();dialog.Reader.Scroll=scroll;dialog.Reader.targetGraphic=hit;
            ButtonRole(dialog.Reader,DungeonVisualRole.InputSurface);
            dialog.Back=GameUISkin.Button(dialog.Panel,"Back",new Vector2(.04f,.03f),new Vector2(.30f,.11f),null);ButtonRole(dialog.Back,DungeonVisualRole.Secondary);
            var hint=GameUISkin.Label(dialog.Panel,"Up / Down: scroll\nCancel: back",new Vector2(.34f,.025f),new Vector2(.96f,.115f),20);hint.color=GameUITheme.LightInk;
            dialog.Reader.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=dialog.Back};
            dialog.Back.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=dialog.Reader,selectOnUp=dialog.Reader};
            CloseButton(dialog.Panel,dialog);
            Record(canvas.gameObject);
            Theme.HistoryPrefab=PrefabUtility.SaveAsPrefabAsset(canvas.gameObject,Folder+"/History.prefab").GetComponent<EventHistoryDialog>();
        }
        finally{Object.DestroyImmediate(canvas.gameObject);}
    }
}
