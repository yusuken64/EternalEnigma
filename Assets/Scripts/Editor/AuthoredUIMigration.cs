using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>One-time, editor-only migration. Completed scenes are not regenerated.</summary>
public static class AuthoredUIMigration
{
    const string Folder="Assets/Resources/UI/Authored";
    const string Marker="Authored UI v1";
    static void Set(object target,string field,object value)
    {
        var info=target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(info==null)throw new MissingFieldException(target.GetType().Name,field);
        info.SetValue(target,value);
        if(target is Object obj)EditorUtility.SetDirty(obj);
    }
    static T Find<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
    static T Save<T>(T component,string name) where T:Component
    {
        var prefab=PrefabUtility.SaveAsPrefabAsset(component.gameObject,$"{Folder}/{name}.prefab");
        Object.DestroyImmediate(component.gameObject);return prefab.GetComponent<T>();
    }
    static T Instance<T>(T prefab,Transform parent) where T:Component => ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,parent)).GetComponent<T>();
    static T Load<T>(string name) where T:Component => AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab").GetComponent<T>();
    static void Fit(RectTransform rect,Vector2 min,Vector2 max)
    {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    static VerticalLayoutGroup Vertical(Transform parent,float spacing=8)
    {
        var layout=parent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=spacing;
        layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;return layout;
    }
    static AuthoredButton Row(string name,float height,float font,bool icon=false,bool hero=false,bool focus=false)
    {
        var button=GameUISkin.Button(null,name,Vector2.zero,Vector2.one,null);
        var row=button.gameObject.AddComponent<AuthoredButton>();row.Button=button;row.Label=button.GetComponentInChildren<TMP_Text>();
        row.Label.fontSize=font;row.Label.enableAutoSizing=true;row.Label.fontSizeMin=font-5;row.Label.fontSizeMax=font;
        button.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
        button.gameObject.AddComponent<PartyMenuRow>();
        if(focus)button.gameObject.AddComponent<ClassPickerFocus>();
        if(icon)
        {
            var min=hero?new Vector2(.015f,.06f):new Vector2(.015f,.1f);
            var max=hero?new Vector2(.34f,.94f):new Vector2(.13f,.9f);
            if(hero){var backing=GameUISkin.Panel(button.transform,min,max);backing.color=new Color(.09f,.17f,.17f,.88f);backing.raycastTarget=false;}
            row.Icon=GameUISkin.Rect("Icon",button.transform,min,max).gameObject.AddComponent<Image>();
            row.Icon.preserveAspect=true;row.Icon.raycastTarget=false;row.Label.rectTransform.anchorMin=new Vector2(hero ? .35f : .15f,0);
        }
        return Save(row,name);
    }
    static TMP_Text TextTemplate(string name,float height,int font)
    {
        var text=GameUISkin.Label(null,name,Vector2.zero,Vector2.one,font);text.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
        return Save<TMP_Text>(text,name);
    }
    [MenuItem("Tools/Eternal Enigma/UI/Migrate Authored Scenes")]
    public static void Run()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling)throw new InvalidOperationException("Exit Play Mode and finish compiling first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save or discard the open scene changes before migration.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Templates();
            foreach(var name in new[]{"Common","MainMenu","Town","Overworld","DungeonScene"})
            {
                var scene=EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                if(scene.GetRootGameObjects().Any(r=>r.name==Marker))continue;
                if(name=="Common")CommonScene(scene);
                else if(name=="MainMenu")MainScene(scene);
                else GameplayScene(scene,name);
                foreach(var dialog in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MessageDialog>(true)))dialog.AuthorLayout();
                new GameObject(Marker);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();Debug.Log("Authored UI migration completed.");
        }
        finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static void Templates()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/PartyEntry.prefab")!=null)return;
        var entry=Row("PartyEntry",66,24,true);var plain=Row("PartyPlainEntry",66,24);
        var hero=Row("PartyHero",100,23,true,true);var pickerRow=Row("PickerRow",64,26);
        Row("ClassChoice",68,26,true,false,true);Row("Companion",60,26);Row("TravelAction",30,26);
        var heading=TextTemplate("Section",32,22);var empty=TextTemplate("Empty",90,25);
        // Extend the existing prefab in place, retaining its GUID and authored layout.
        string path="Assets/Resources/UI/PartyMenu.prefab";
        var contents=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var menu=contents.GetComponent<PartyMenu>();menu.HeroTemplate=hero;menu.EntryTemplate=entry;menu.PlainEntryTemplate=plain;
            menu.HeadingTemplate=heading;menu.EmptyTemplate=empty;
            var layout=menu.HeroesRoot.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=8;
            layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=true;
            PrefabUtility.SaveAsPrefabAsset(contents,path);
        }finally{PrefabUtility.UnloadPrefabContents(contents);}
        var picker=PartyMenuPicker.AuthorLayout(null,"Choose an action",new List<(string,Action)>());picker.RowTemplate=pickerRow;
        picker.gameObject.SetActive(false);Save(picker,"PartyPicker");
        var root=new GameObject("Launcher authoring");PartyMenuLauncher.AuthorLayout(root.transform,()=>false,_=>{});
        var launcher=root.GetComponentInChildren<PartyMenuLauncher>();launcher.transform.SetParent(null,false);Save(launcher,"PartyShortcuts");Object.DestroyImmediate(root);
        var choiceCanvas=GameUISkin.Canvas("Confirm",null,2100);var choice=choiceCanvas.gameObject.AddComponent<CampaignChoice>();
        GameUISkin.Rect("Modal backdrop",choice.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);
        var panel=GameUISkin.Panel(choice.transform,new Vector2(.22f,.3f),new Vector2(.78f,.7f));
        choice.Prompt=GameUISkin.Label(panel.transform,"Confirm action",new Vector2(.08f,.43f),new Vector2(.92f,.91f),30);
        choice.Confirm=GameUISkin.Button(panel.transform,"Confirm",new Vector2(.08f,.12f),new Vector2(.58f,.32f),null);
        choice.Back=GameUISkin.Button(panel.transform,"Back",new Vector2(.62f,.12f),new Vector2(.92f,.32f),null);choice.gameObject.SetActive(false);Save(choice,"Confirm");
        foreach(bool dungeon in new[]{false,true})
        {
            var messages=new GameObject("Game messages").AddComponent<GameMessages>();messages.AuthorLayout(dungeon);
            Save(messages,dungeon?"DungeonEvents":"TravelEvents");
        }
        Save(InnDialog.AuthorLayout(null),"Inn");Save(HomeBedDialog.AuthorLayout(null),"Home");
        CalloutTemplate(false);CalloutTemplate(true);
        AuthorSkillIcons();
    }
    static void CommonScene(Scene scene)
    {
        var common=Find<Common>(scene);Instance(Load<CampaignChoice>("Confirm"),common.transform);
        var settings=common.GlobalSettings;DungeonOptions.AuthorLayout(settings);
        var history=settings.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Event history");
        UnityEventTools.AddPersistentListener(history.onClick,settings.ShowEventHistory);
        var transition=common.ScreenTransition;var canvas=transition.ShutterScreen.GetComponentInParent<Canvas>();
        canvas.overrideSorting=true;canvas.sortingOrder=ScreenTransition.SceneOverlayOrder;
        var label=GameUISkin.Label(transition.ShutterScreen.transform,"Destination",new Vector2(.1f,.4f),new Vector2(.9f,.6f),36);
        label.name="Destination title";label.alignment=TextAlignmentOptions.Center;label.color=GameUITheme.LightInk;
        label.gameObject.SetActive(false);Set(transition,"destinationLabel",label);
    }
    static void MainScene(Scene scene)
    {
        var menu=Find<MainMenu>(scene);AuthorMainMenu(menu);
        var developer=menu.gameObject.AddComponent<MainMenuDeveloperControls>();
        developer.Toggle=menu.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Developer");
        developer.Controls=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Where(b=>b==menu.TestDungeonButton || b.name.Contains("Autoplay")).Select(b=>b.gameObject).ToArray();
        var hero=ProtagonistHeroPicker.AuthorLayout(null,null,null);hero.transform.SetParent(menu.transform,false);
        var choices=hero.transform.Find("Heroes");hero.ChoiceButtons=new Button[8];
        for(int i=0;i<8;i++)
        {
            float x=(i%2)*.51f,top=1-(i/2)/4f;
            var button=GameUISkin.Button(choices,"Hero "+(i+1),new Vector2(x,top-.88f/4),new Vector2(x+.49f,top),null);
            var graphic=button.targetGraphic;var colors=button.colors;var sprites=button.spriteState;var transition=button.transition;Object.DestroyImmediate(button);
            var select=graphic.gameObject.GetComponentInParent<Button>(); // targetGraphic can be a child; use the authored button root below.
            var buttonObject=choices.GetChild(i).gameObject;
            var activate=buttonObject.AddComponent<SelectToActivateButton>();activate.targetGraphic=graphic;activate.colors=colors;activate.spriteState=sprites;activate.transition=transition;
            buttonObject.AddComponent<ClassPickerFocus>();hero.ChoiceButtons[i]=activate;
        }
        hero.gameObject.SetActive(false);
        var cls=ProtagonistClassPicker.AuthorLayout(null,null,null);cls.transform.SetParent(menu.transform,false);cls.ChoiceTemplate=Load<AuthoredButton>("ClassChoice");
        var classes=cls.transform.Find("Classes").gameObject.AddComponent<GridLayoutGroup>();classes.constraint=GridLayoutGroup.Constraint.FixedColumnCount;classes.constraintCount=2;
        classes.cellSize=new Vector2(500,68);classes.spacing=new Vector2(20,8);cls.gameObject.SetActive(false);
        CampaignSlotsScene(menu.transform);
    }
    static void CampaignSlotsScene(Transform parent)
    {
        var canvas=GameUISkin.Canvas("Campaign slots",parent,1000);var view=canvas.gameObject.AddComponent<CampaignSlots>();
        var panel=GameUISkin.Panel(view.transform,new Vector2(.03f,.04f),new Vector2(.97f,.96f));
        view.Title=GameUISkin.Label(panel.transform,"Campaigns",new Vector2(.04f,.90f),new Vector2(.96f,.97f),36);
        view.Slots=new CampaignSlotView[SaveSystem.SlotCount];
        for(int i=0;i<view.Slots.Length;i++)
        {
            float left=.025f+i*.325f;
            var button=((GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/CampaignSlotButton.prefab"),panel.transform))
                .GetComponent<SelectToActivateButton>();
            button.name="Slot "+(i+1);
            var rect=(RectTransform)button.transform;
            rect.anchorMin=new Vector2(left,.55f);rect.anchorMax=new Vector2(left+.30f,.88f);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            button.GetComponentInChildren<TMP_Text>().text="";
            var slot=button.gameObject.AddComponent<CampaignSlotView>();slot.Button=button;view.Slots[i]=slot;
            slot.Artwork=GameUISkin.Rect("Biome artwork",button.transform,new Vector2(.015f,.025f),new Vector2(.985f,.975f)).gameObject.AddComponent<RawImage>();slot.Artwork.raycastTarget=false;
            var shade=GameUISkin.Rect("Information shade",button.transform,new Vector2(.015f,.025f),new Vector2(.985f,.975f)).gameObject.AddComponent<Image>();shade.color=new Color(0,0,0,.58f);shade.raycastTarget=false;
            slot.Portraits=new Image[4];for(int j=0;j<4;j++){var image=GameUISkin.Rect("Portrait "+j,button.transform,new Vector2(.04f+j*.23f,.05f),new Vector2(.24f+j*.23f,.30f)).gameObject.AddComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;slot.Portraits[j]=image;}
            slot.Label=GameUISkin.Label(button.transform,"Slot "+(i+1),new Vector2(.04f,.31f),new Vector2(.96f,.96f),23);slot.Label.color=Color.white;slot.Label.enableAutoSizing=true;slot.Label.fontSizeMin=15;
        }
        var detail=GameUISkin.Label(panel.transform,"Campaign details",new Vector2(.04f,.16f),new Vector2(.96f,.52f),24);detail.enableAutoSizing=true;detail.fontSizeMin=17;Set(view,"detail",detail);
        var bar=GameUISkin.Rect("Required dungeon progress",panel.transform,new Vector2(.04f,.13f),new Vector2(.96f,.145f)).gameObject.AddComponent<Image>();bar.color=new Color(.2f,.2f,.2f);
        var progress=GameUISkin.Rect("Progress",bar.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();progress.color=new Color(.8f,.65f,.25f);Set(view,"progress",progress);
        view.BackButton=GameUISkin.Button(panel.transform,"Back",new Vector2(.04f,.03f),new Vector2(.3f,.10f),null);view.gameObject.SetActive(false);
    }
    static void GameplayScene(Scene scene,string name)
    {
        Transform owner=name=="Town"?Find<Town>(scene).transform:name=="Overworld"?Find<OverworldScene>(scene).transform:Find<Game>(scene).transform;
        Instance(Resources.Load<ResourceHUD>("UI/ResourceHUD"),owner);
        Instance(Load<PartyMenuLauncher>("PartyShortcuts"),owner);
        var theme=GameUITheme.Current;
        var partyPrefab=theme.DungeonMenuPrefab!=null?theme.DungeonMenuPrefab.GetComponent<PartyMenu>():Resources.Load<PartyMenu>("UI/PartyMenu");
        var pickerPrefab=theme.DungeonPickerPrefab!=null?theme.DungeonPickerPrefab.GetComponent<PartyMenuPicker>():Load<PartyMenuPicker>("PartyPicker");
        var party=Instance(partyPrefab,owner);party.gameObject.SetActive(false);
        Instance(pickerPrefab,owner);
        Instance(pickerPrefab,owner).name="Party target picker";
        Instance(Load<GameMessages>("DungeonEvents"),owner);
        if(name=="DungeonScene")DungeonScene(scene,owner.GetComponent<Game>());
        foreach(var shop in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ShopMenuDialog>(true)))
            if(shop.GetComponentInChildren<MenuControlHints>(true)==null)MenuControlHints.Bind(GameUISkin.Label(shop.transform,"",new Vector2(.1f,.08f),new Vector2(.8f,.12f),20));
    }
    static void DungeonScene(Scene scene,Game game)
    {
        var hud=game.gameObject.AddComponent<DungeonHud>();hud.AuthorLayout(game);
        var original=game.CharacterStatsDisplayPrefab;hud.PartySlots=new CharacterStatsDisplay[4];
        for(int i=0;i<4;i++)
        {
            var display=Object.Instantiate(original,hud.PartyRoot);display.name="Party slot "+(i+1);
            var old=display.transform.Cast<Transform>().ToArray();DungeonPartyCard.AuthorLayout(display,hud,i);
            foreach(var child in old)Object.DestroyImmediate(child.gameObject);
            hud.PartySlots[i]=display;display.gameObject.SetActive(false);
        }
        game.CharacterStatsDisplayContainer=hud.PartyRoot;
        var minimap=Find<Minimap>(scene);Fit((RectTransform)minimap.transform,new Vector2(.80f,.22f),new Vector2(.985f,.48f));
        var outline=minimap.minimapImage.GetComponent<Outline>()??minimap.minimapImage.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.7f,.53f,.25f,.9f);outline.effectDistance=new Vector2(2,-2);
        var result=Find<GameOverScreen>(scene);result.AuthorCanvas();result.AuthorLayout();
        var back=GameUISkin.Button(result.transform,"Main menu",new Vector2(.35f,.08f),new Vector2(.65f,.16f),null);back.name="Campaign main menu";UnityEventTools.AddPersistentListener(back.onClick,result.Quit_Clicked);
        Find<NewFloorMessage>(scene).AuthorLayout();
        var canvas=GameUISkin.Canvas("Combat callouts",game.transform,0);var callouts=canvas.gameObject.AddComponent<CombatCalloutView>();callouts.Root=(RectTransform)canvas.transform;
        callouts.Normal=Load<DungeonFloatingText>("Callout");callouts.Prominent=Load<DungeonFloatingText>("ProminentCallout");
    }
    static void CalloutTemplate(bool prominent)
    {
        var text=GameUISkin.Label(null,"Combat message",new Vector2(.5f,.5f),new Vector2(.5f,.5f),prominent?72:54);
        text.rectTransform.sizeDelta=new Vector2(620,110);text.rectTransform.pivot=new Vector2(.5f,.5f);text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;
        text.outlineColor=new Color32(35,25,45,255);text.outlineWidth=.2f;text.raycastTarget=false;
        var callout=text.gameObject.AddComponent<DungeonFloatingText>();Set(callout,"label",text);Set(callout,"height",prominent?230f:180f);Save(callout,prominent?"ProminentCallout":"Callout");
    }
    static void AuthorSkillIcons()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/Resources"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset.GetComponentInChildren<SkillGridItem>(true)==null && asset.GetComponentInChildren<DynamicActionButton>(true)==null)continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    TMP_Text label=null;string field=null;
                    if(component is SkillGridItem skill){label=skill.SkillText;field="SkillIcon";}
                    if(component is DynamicActionButton action){label=action.ActionText;field="SkillIcon";}
                    if(label==null || label.transform.Find("Skill Icon")!=null)continue;
                    var rect=GameUISkin.Rect("Skill Icon",label.transform,new Vector2(0,.5f),new Vector2(0,.5f));rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=new Vector2(4,0);rect.sizeDelta=new Vector2(28,28);
                    var image=rect.gameObject.AddComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;rect.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
                    var info=component.GetType().GetField(field);if(info!=null && info.FieldType==typeof(Image))info.SetValue(component,image);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }

    [MenuItem("Tools/Eternal Enigma/UI/Finalize Authored Bindings")]
    public static void FinalizeBindings()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling)throw new InvalidOperationException("Finish compiling and exit Play Mode first.");
        // Keep in-memory scene edits (including layout recalculation) when finishing this migration.
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var open=SceneManager.GetSceneAt(i);
            if(open.isDirty && open.path.StartsWith("Assets/Scenes/"))EditorSceneManager.SaveScene(open);
        }
        foreach(var name in new[]{"DungeonEvents","TravelEvents"})
        {
            var path=$"{Folder}/{name}.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<GameMessages>();Set(view,"historyButton",root.GetComponentInChildren<Button>(true));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var partyPath="Assets/Resources/UI/PartyMenu.prefab";var partyRoot=PrefabUtility.LoadPrefabContents(partyPath);
        try
        {
            var menu=partyRoot.GetComponent<PartyMenu>();var old=menu.HeroesRoot.GetComponent<HorizontalLayoutGroup>();if(old!=null)Object.DestroyImmediate(old);
            var layout=menu.HeroesRoot.GetComponent<ResponsiveGridLayout>()??menu.HeroesRoot.gameObject.AddComponent<ResponsiveGridLayout>();
            layout.Columns=4;layout.SingleRow=true;layout.HorizontalInset=.003f;layout.ColumnGap=.006f;layout.RowFill=1;
            PrefabUtility.SaveAsPrefabAsset(partyRoot,partyPath);
        }finally{PrefabUtility.UnloadPrefabContents(partyRoot);}
        string spritePath=$"{Folder}/TurnHighlight.png";
        if(!File.Exists(spritePath))
        {
            var texture=new Texture2D(2,2);texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
            File.WriteAllBytes(spritePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(spritePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(spritePath);importer.textureType=TextureImporterType.Sprite;importer.spriteBorder=Vector4.one;importer.mipmapEnabled=false;importer.SaveAndReimport();
        }
        var border=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/Resources"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset.GetComponentInChildren<BallistaDialog>(true)==null && asset.GetComponentInChildren<ShopMenuDialog>(true)==null)continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var trainer in root.GetComponentsInChildren<BallistaDialog>(true))trainer.AuthorLayout();
                foreach(var shop in root.GetComponentsInChildren<ShopMenuDialog>(true))
                    if(shop.GetComponentInChildren<MenuControlHints>(true)==null)MenuControlHints.Bind(GameUISkin.Label(shop.transform,"",new Vector2(.1f,.08f),new Vector2(.8f,.12f),20));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach(var name in new[]{"Common","MainMenu","Town","Overworld","DungeonScene"})
            {
                var scene=EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                if(name=="MainMenu")
                {
                    var menu=Find<MainMenu>(scene);
                    var dialogs=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Main menu dialogs")??new GameObject("Main menu dialogs");
                    Find<CampaignSlots>(scene).transform.SetParent(dialogs.transform,false);
                    Find<ProtagonistHeroPicker>(scene).transform.SetParent(dialogs.transform,false);
                    Find<ProtagonistClassPicker>(scene).transform.SetParent(dialogs.transform,false);
                    if(!scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameMessages>(true)).Any())Instance(Load<GameMessages>("DungeonEvents"),dialogs.transform);
                    var classes=Find<ProtagonistClassPicker>(scene).transform.Find("Classes");
                    var grid=classes.GetComponent<GridLayoutGroup>();if(grid!=null)Object.DestroyImmediate(grid);
                    if(classes.GetComponent<ResponsiveGridLayout>()==null)classes.gameObject.AddComponent<ResponsiveGridLayout>();
                }
                if(name=="DungeonScene")
                {
                    var hud=Find<DungeonHud>(scene);
                    foreach(var card in hud.PartySlots)
                    {
                        var image=card.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Turn highlight");image.sprite=border;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                    }
                    // Shared authored card; the scene owns four fixed slot placements.
                    if(!PrefabUtility.IsPartOfPrefabInstance(hud.PartySlots[0]))
                    {
                        var game=Find<Game>(scene);
                        string path=AssetDatabase.GetAssetPath(game.CharacterStatsDisplayPrefab);
                        var template=Object.Instantiate(hud.PartySlots[0]);template.name=game.CharacterStatsDisplayPrefab.name;
                        template.gameObject.SetActive(true);Fit((RectTransform)template.transform,Vector2.zero,Vector2.one);
                        var saved=PrefabUtility.SaveAsPrefabAsset(template.gameObject,path).GetComponent<CharacterStatsDisplay>();Object.DestroyImmediate(template.gameObject);
                        game.CharacterStatsDisplayPrefab=saved;
                        for(int i=0;i<hud.PartySlots.Length;i++)
                        {
                            Object.DestroyImmediate(hud.PartySlots[i].gameObject);
                            var card=Instance(saved,hud.PartyRoot);card.name="Party slot "+(i+1);
                            Fit((RectTransform)card.transform,new Vector2(0,1-(i+1)*.25f+.008f),new Vector2(1,1-i*.25f));
                            card.gameObject.SetActive(false);hud.PartySlots[i]=card;
                        }
                    }
                }
                if(name=="Town")
                {
                    var menu=Find<TownMenu>(scene);var settings=menu.ItemActionDialog;
                    if(settings==null)throw new InvalidOperationException("Town item action dialog is not authored.");
                }
                foreach(var trainer in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BallistaDialog>(true)))trainer.AuthorLayout();
                foreach(var component in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)))
                    if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();Debug.Log("Authored UI bindings finalized.");
        }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }

    [MenuItem("Tools/Eternal Enigma/UI/Inspect Authored Assets")]
    public static void Inspect()
    {
        var report=new System.Text.StringBuilder();var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach(var name in new[]{"Common","MainMenu","Town","Overworld","DungeonScene"})
            {
                var scene=EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                report.AppendLine($"{name}: {missing} missing scripts, {all.Length} objects");
                foreach(var view in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(v=>v!=null && (v is GameMessages || v is DungeonHud || v is CampaignHUD || v is PartyMenuLauncher || v is DungeonOptions || v is ProtagonistHeroPicker || v is ProtagonistClassPicker || v is CampaignSlots || v is PartyMenuPicker || v is PartyMenu)))
                {
                    report.AppendLine($"  {view.GetType().Name}: {view.name} activeSelf={view.gameObject.activeSelf}");
                    foreach(var f in view.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                        if(typeof(Object).IsAssignableFrom(f.FieldType) && (f.IsPublic || f.GetCustomAttribute<SerializeField>()!=null) && (Object)f.GetValue(view)==null)
                            report.AppendLine($"    NULL {f.Name}");
                }
                foreach(var t in all.Where(t=>t is RectTransform && (t.name.StartsWith("Hero ") || t.name=="Inventory [Q]" || t.name=="Skills [R]" || t.name=="Choose your hero" || t.name=="Party slot 1")))
                {var r=(RectTransform)t;report.AppendLine($"  Rect {t.name}: {r.anchorMin} - {r.anchorMax} offsets {r.offsetMin} / {r.offsetMax}");}
            }
        }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        Directory.CreateDirectory("Temp/AuthoredUI");File.WriteAllText("Temp/AuthoredUI/asset-inspection.txt",report.ToString());Debug.Log(report.ToString());
    }
static void AuthorMainMenu(MainMenu menu)
    {
        var roots=menu.gameObject.scene.GetRootGameObjects();
        var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.CompareTag("MainCamera"));
        if(camera!=null)camera.rect=new Rect(0,0,.76f,1);
        var column=GameUISkin.Canvas("Menu button column",menu.transform,-10);
        var background=GameUISkin.Rect("Backing",column.transform,new Vector2(.76f,0),Vector2.one).gameObject.AddComponent<Image>();
        background.color=new Color(.035f,.045f,.075f,1);background.raycastTarget=false;
        Object.DestroyImmediate(column.GetComponent<GraphicRaycaster>());
        var labels=roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true));
        var title=labels.FirstOrDefault(t=>t.text=="Eternal Enigma");
        if(title!=null)
        {
            title.fontSize*=.85f;
            title.rectTransform.anchorMin=new Vector2(.035f,.78f);title.rectTransform.anchorMax=new Vector2(.75f,.95f);
            title.rectTransform.offsetMin=title.rectTransform.offsetMax=Vector2.zero;
        }
        var container=menu.StartButton.transform.parent as RectTransform;
        if(container!=null)
        {
            container.anchorMin=new Vector2(.77f,.28f);container.anchorMax=new Vector2(.97f,.69f);
            container.offsetMin=container.offsetMax=Vector2.zero;
            var layout=container.GetComponent<VerticalLayoutGroup>();if(layout!=null){layout.spacing=8;layout.childControlWidth=true;layout.childForceExpandWidth=true;}
        }
        var developer=roots.SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Where(b=>b==menu.TestDungeonButton || b.name.Contains("Autoplay")).ToArray();
        foreach(var button in developer)button.gameObject.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var canvas=GameUISkin.Canvas("Developer controls",menu.transform,10);
        bool open=false;
        GameUISkin.Button(canvas.transform,"Developer",new Vector2(.84f,.025f),new Vector2(.97f,.085f),()=>
        {open=!open;foreach(var button in developer)button.gameObject.SetActive(open);});
#endif
    }
}
