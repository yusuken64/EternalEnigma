using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Staged production-scene screenshots for a human design review. No test runner or assertions.</summary>
[InitializeOnLoad]
public static class OpeningDesignPreview
{
    private const string Key = "EternalEnigma.OpeningDesignPreview";
    public const string Folder = "Temp/OpeningDesignPreview";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static IDisposable saves, preferences;
    private static readonly Stack<IEnumerator> sequence = new();
    private static Gate waiting;
    private static bool started;
    private static EditorWindow view;
    private sealed class Gate
    {
        public Func<bool> Ready;
        public double Deadline;
        public string Label;
    }
    private sealed class PreviewStore : ISaveStore
    {
        private readonly Dictionary<int, string> slots = new();
        public string Read(int slot) => slots.TryGetValue(slot, out var json) ? json : null;
        public void Write(int slot, string json) => slots[slot] = json;
        public void Clear(int slot) => slots.Remove(slot);
    }

    static OpeningDesignPreview()
    {
        // Reinstall after the Enter Play Mode domain reload, before production scene Awake.
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlayingOrWillChangePlaymode) Isolate();
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.ExitingEditMode) Isolate();
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + ".FastPlay", false);
            if (state == PlayModeStateChange.ExitingPlayMode) RestoreScopes();
            if (state == PlayModeStateChange.EnteredEditMode) RestoreEditor();
        };
    }

    [MenuItem("Tools/Eternal Enigma/Opening/Capture Design Preview")]
    public static void Capture() => StartCapture(false);

    [MenuItem("Tools/Eternal Enigma/Opening/Capture Save Artwork Preview")]
    public static void CaptureArtwork() => StartCapture(true);

    [MenuItem("Tools/Eternal Enigma/UI/Open Inventory Review")]
    public static void OpenInventoryReview() => StartCapture(false, PartyMenuTab.Inventory);

    [MenuItem("Tools/Eternal Enigma/UI/Open Equipment Preview")]
    public static void OpenEquipmentPreview() => StartCapture(false, PartyMenuTab.Equipment);

    [MenuItem("Tools/Eternal Enigma/UI/Open Equipment Description Review")]
    public static void OpenEquipmentDescriptionReview() => StartCapture(false, PartyMenuTab.Inventory, equipmentDescriptionReview:true);

    [MenuItem("Tools/Eternal Enigma/UI/Open Skills Review")]
    public static void OpenSkillsReview() => StartCapture(false, PartyMenuTab.Skills);

    [MenuItem("Tools/Eternal Enigma/UI/Open History Review")]
    public static void OpenHistoryReview() => StartCapture(false, PartyMenuTab.Inventory, true);

    [MenuItem("Tools/Eternal Enigma/UI/Open Stats Review")]
    public static void OpenStatsReview() => StartCapture(false, PartyMenuTab.Stats);

    [MenuItem("Tools/Eternal Enigma/UI/Open Event Feed Review")]
    public static void OpenEventFeedReview() => StartCapture(false,PartyMenuTab.Stats,feedReview:true);

    [MenuItem("Tools/Eternal Enigma/UI/Open Party Portrait Review")]
    public static void OpenPartyPortraitReview() => StartCapture(false,PartyMenuTab.Stats,feedReview:true,partyReview:true);

    [MenuItem("Tools/Eternal Enigma/UI/Open Level Up Review")]
    public static void OpenLevelUpReview() => StartCapture(false, levelUpReview:true);

    private static void StartCapture(bool artworkOnly, PartyMenuTab? reviewTab = null, bool historyReview = false, bool feedReview = false, bool partyReview = false, bool equipmentDescriptionReview = false, bool levelUpReview = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Finish Play Mode before starting the isolated design preview.");
        Directory.CreateDirectory(Folder);
        view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        SessionState.SetInt(Key + ".Size", (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view));
        SessionState.SetString(Key + ".StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetInt(Key + ".Animation", DungeonPreferences.AnimationOverride.HasValue ? (int)DungeonPreferences.AnimationOverride.Value : -1);
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetBool(Key + ".Suppression", LoadingSceneIntegration.SuppressAutomaticSceneLoading);
        SessionState.SetBool(Key + ".FastPlay", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".ArtworkOnly", artworkOnly);
        SessionState.SetInt(Key + ".PartyMenuReview", reviewTab.HasValue ? (int)reviewTab.Value : -1);
        SessionState.SetBool(Key + ".HistoryReview", historyReview);
        SessionState.SetBool(Key + ".FeedReview",feedReview);
        SessionState.SetBool(Key + ".PartyPortraitReview",partyReview);
        SessionState.SetBool(Key + ".EquipmentDescriptionReview",equipmentDescriptionReview);
        SessionState.SetBool(Key + ".LevelUpReview",levelUpReview);
        started = false; sequence.Clear(); waiting = null;
        File.WriteAllText(Folder + "/Progress.txt", "Starting isolated production scene capture.\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = true;
        // A preview must not inherit runtime state from prior tests or fast Play Mode sessions.
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Eternal Enigma/Opening/Stop Design Preview")]
    public static void Stop()
    {
        if (!SessionState.GetBool(Key, false)) return;
        sequence.Clear(); waiting = null;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else RestoreEditor();
    }

    private static void Isolate()
    {
        saves ??= SaveSystem.UseStore(new PreviewStore());
        preferences ??= GamePreferences.Isolate();
    }
    private static void RestoreScopes()
    {
        preferences?.Dispose(); preferences = null;
        saves?.Dispose(); saves = null;
        int animation = SessionState.GetInt(Key + ".Animation", -1);
        DungeonPreferences.AnimationOverride = animation < 0 ? null : (DungeonAnimationMode)animation;
        Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
    }
    private static void RestoreEditor()
    {
        RestoreScopes(); sequence.Clear(); waiting = null; started = false;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".StartScene", ""));
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = SessionState.GetBool(Key + ".Suppression", false);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + ".FastPlay", false);
        view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, SessionState.GetInt(Key + ".Size", 0));
        SessionState.EraseBool(Key);
        // The editor restores the original open scene setup when Play Mode exits.
    }
    private static Gate Await(Func<bool> ready, string label, double timeout = 120) =>
        new() { Ready = ready, Label = label, Deadline = EditorApplication.timeSinceStartup + timeout };
    private static Gate Delay(double seconds)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        return Await(() => EditorApplication.timeSinceStartup >= until, "frame settling", seconds + 15);
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (!started)
            {
                started = true; Isolate(); Application.runInBackground = true;
                DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
                view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")); view.Focus();
                sequence.Push(Frames());
            }
            if (waiting != null)
            {
                if (!waiting.Ready())
                {
                    if (EditorApplication.timeSinceStartup > waiting.Deadline) throw new TimeoutException("Preview waited too long for " + waiting.Label);
                    return;
                }
                waiting = null;
            }
            if (sequence.Count == 0) return;
            var next = sequence.Peek();
            if (!next.MoveNext()) { if (sequence.Count > 0) sequence.Pop(); return; }
            if (next.Current is IEnumerator child) sequence.Push(child);
            else if (next.Current is Gate gate) waiting = gate;
        }
        catch (Exception error)
        {
            File.AppendAllText(Folder + "/Progress.txt", "Capture interrupted: " + error + "\n");
            Debug.LogException(error); Stop();
        }
    }
    private static IEnumerator Frames()
    {
        yield return Await(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true &&
            Common.InstanceOrNull != null && !Common.InstanceOrNull.Travel.IsTransitioning, "MainMenu");
        var common = Common.Instance;
        var mainMenu = Object.FindFirstObjectByType<MainMenu>();
        if (SessionState.GetBool(Key + ".LevelUpReview", false))
        {
            yield return LevelUpReview(mainMenu);
            yield break;
        }
        int reviewTab = SessionState.GetInt(Key + ".PartyMenuReview", -1);
        if (reviewTab >= 0)
        {
            yield return PartyMenuReview(mainMenu, (PartyMenuTab)reviewTab);
            yield break; // Keep Play Mode and the isolated save store alive for the user.
        }
        var backgrounds = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
        var biomes = (EternalEnigma.Core.World.OverworldBiome[])Enum.GetValues(typeof(EternalEnigma.Core.World.OverworldBiome));
        for (int page = 0; page < biomes.Length; page += 3)
        {
            CampaignSlots.Show(mainMenu, false);
            var slots = Object.FindFirstObjectByType<CampaignSlots>();
            for (int i = 0; i < slots.Slots.Length; i++)
            {
                var biome = biomes[Mathf.Min(page + i, biomes.Length - 1)];
                var sample = mainMenu.CreateNewSave(42);
                sample.Summary = new CampaignSaveSummary { Biome = biome, Town = "Home", Region = biome.ToString(), RequiredTotal = 5 };
                sample.SavedUtc = DateTime.UtcNow.ToString("O");
                slots.Slots[i].Setup(sample, backgrounds, i, mainMenu);
            }
            slots.Slots[1].Button.Select();
            slots.Slots[0].Button.Select();
            yield return Pair("save-biomes-" + page);
            slots.BackButton.onClick.Invoke();
        }
        if (SessionState.GetBool(Key + ".ArtworkOnly", false))
        {
            File.AppendAllText(Folder + "/Progress.txt", "Complete. Save artwork preview uses isolated sample campaigns.\n");
            Stop(); yield break;
        }
        mainMenu.ChooseHero();
        var picker = Object.FindFirstObjectByType<ProtagonistHeroPicker>();
        yield return Pair("hero-recommended");
        picker.ToggleRoster(); yield return Pair("hero-all");
        picker.BackButton.onClick.Invoke(); yield return Delay(.3);
        common.GameSaveData = mainMenu.CreateNewSave(42);
        common.Travel.NewCampaign(42);
        yield return Await(() => Object.FindFirstObjectByType<Town>()?.IsReady == true && !common.Travel.IsTransitioning, "new town");
        var town = Object.FindFirstObjectByType<Town>();
        var hero = town.TownPlayer.ControllingTownAlly;
        var bed = Object.FindFirstObjectByType<HomeBed>();
        yield return Pair("home");
        if (town.HomeLesson != null)
        {
            bed.Interact(); yield return Pair("home-bed-locked");
            Object.FindFirstObjectByType<HomeBedDialog>().CloseDialog();
            yield return HomeInteraction(town, town.HomeLesson.DoorCell, "home-door-locked");
            yield return HomeInteraction(town, town.HomeLesson.BookCell, "home-book");
            yield return HomeInteraction(town, town.HomeLesson.ChestCell, "home-first-capability");
            yield return Pair("home-chest-open");
            var menus = Object.FindFirstObjectByType<TownMenuManager>();
            menus.OpenPartyMenu(PartyMenuTab.Capabilities);
            yield return Pair("home-capabilities");
            menus.PartyMenu.EntryButtons.First().onClick.Invoke();
            yield return Pair("home-key-details");
            menus.CloseAllMenus();
            yield return HomeInteraction(town, town.HomeLesson.DoorCell, "home-unlock-door");
            yield return Pair("home-door-open");
            File.AppendAllText(Folder + "/Progress.txt", $"Home review: beds={town.GetComponentsInChildren<HomeBed>().Length}, bookRead={common.CampaignContext.State.ReadHomeBook}, key={common.CampaignContext.HasHomeKey}, doorUnlocked={common.CampaignContext.State.HomeDoorUnlocked}\n");
        }
        bed.Interact(); yield return Pair("home-menu");
        Object.FindFirstObjectByType<HomeBedDialog>().CloseDialog();

        var bakery = town.TownBuildings.Single(b => b.Definition.Id == "bakery");
        StageAt(town, town.Plan.ShopRoomAt(bakery.TilemapPosition.ToGridPoint()).VendorAnchor.ToCell() - Vector3Int.up);
        bakery.Interact(town.TownPlayer, null); yield return Pair("bakery");
        var shop = Object.FindFirstObjectByType<ShopMenuDialog>();
        shop.ShopItems[0].BuyButton.onClick.Invoke(); yield return Pair("bread-receipt");
        shop.BuyModeButton.onClick.Invoke(); yield return Pair("bakery-stock");
        shop.Cancel_Clicked();

        var trainer = town.TownBuildings.Single(b => b.Definition.Id == "trainer");
        StageAt(town, town.Plan.ShopRoomAt(trainer.TilemapPosition.ToGridPoint()).VendorAnchor.ToCell() - Vector3Int.up);
        trainer.Interact(town.TownPlayer, null); yield return Pair("trainer");
        Object.FindFirstObjectByType<BallistaDialog>().Close_Clicked();
        var offer = TrainerOffers.Build(hero, town.Configuration).First(o => o.CanLearn && o.Skill.ActivationType == ActivationType.Active);
        town.Services.Learn(hero, offer.Skill, out _);
        StageAt(town, bed.Tile - Vector3Int.up);
        bed.SleepAndSave();
        yield return Await(() => !bed.IsSleeping && !common.Travel.IsTransitioning, "morning presentation");
        if (common.MessageDialog.gameObject.activeInHierarchy) common.MessageDialog.Ok_Clicked();
        yield return Delay(.5);
        common.Travel.EnterTownDungeon(town, "story-0");
        yield return Await(() => Game.Instance?.IsReady == true && !common.Travel.IsTransitioning && !Game.Instance.NewFloorMessage.gameObject.activeSelf, "dungeon entrance");
        yield return Pair("dungeon-map");
        Game.Instance.AdvanceFloor();
        yield return Await(() => Game.Instance?.IsReady == true && Game.Instance.PlayerController.Floor == 2 &&
            !common.Travel.IsTransitioning && !Game.Instance.NewFloorMessage.gameObject.activeSelf, "floor two presentation");
        yield return Pair("floor-2-map-overlay");
        var minimap = Object.FindFirstObjectByType<Minimap>();
        minimap.CycleMode(); yield return Pair("floor-2-map-full");
        minimap.CycleMode(); yield return Pair("floor-2-map-hidden");
        File.AppendAllText(Folder + "/Progress.txt", "Complete. Frames are staged layout previews, not a recorded playthrough or gameplay test.\n");
        Debug.Log("Opening design preview captured: " + Path.GetFullPath(Folder));
        Stop();
    }
    private static IEnumerator LevelUpReview(MainMenu mainMenu)
    {
        var prefab = TownSceneLoader.Default.AllyCatalog.Single(ally => ally.Name == "Avery");
        mainMenu.DebugAllyPrefabs = new List<TownAlly> { prefab };
        mainMenu.DebugAllies = new List<TownAllyData> { new() { AllyName = prefab.Name, Skills = new() } };
        mainMenu.TestDungeon_Clicked();
        yield return Await(() => Game.Instance != null && Game.Instance.IsReady &&
            !Game.Instance.TurnManager.IsProcessingTurn && !Common.Instance.ScreenTransition.BlocksInput, "level-up review dungeon");
        view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view,
            new object[] { new Vector2(1280, 720), "Level-up review" });
        view.Focus();
        yield return Delay(.3);
        var hero = Game.Instance.PlayerController.ControlledAlly;
        hero.Vitals.Level = 2;
        hero.Attributes = default;
        hero.AttributePromptPending = true;
        hero.SyncDisplayedStats();
        DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
        LevelUpChoiceDialog.Open(hero, null);
        Common.Instance.AudioManager.PlayCue(SoundCue.LevelUp);
        yield return Delay(.15);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Folder + "/level-up-review.png");
        File.AppendAllText(Folder + "/Progress.txt", "Level-up review ready. Play Mode remains open with physical input enabled and saves isolated.\n");
        Debug.Log("Level-up review is ready. Stop Play Mode when finished; existing saves are unchanged.");
        view.Focus();
    }

    private static IEnumerator PartyMenuReview(MainMenu mainMenu, PartyMenuTab tab)
    {
        var hero = TownSceneLoader.Default.AllyCatalog.Single(ally => ally.Name == "Avery");
        bool partyReview=SessionState.GetBool(Key + ".PartyPortraitReview",false);
        var heroes=new List<TownAlly>{hero};
        if(partyReview)heroes.AddRange(TownSceneLoader.Default.AllyCatalog.Where(ally=>ally!=hero).Take(3));
        mainMenu.DebugAllyPrefabs = heroes;
        mainMenu.DebugAllies = heroes.Select(ally=>new TownAllyData{AllyName=ally.Name,Skills=new()}).ToList();
        mainMenu.TestDungeon_Clicked();
        yield return Await(() => Game.Instance != null && Game.Instance.IsReady &&
            !Game.Instance.TurnManager.IsProcessingTurn && !Common.Instance.ScreenTransition.BlocksInput, "party menu review dungeon");
        var game = Game.Instance;
        var bag = game.PlayerController.Inventory;
        bag.MaxItems = Inventory.DefaultCapacity;
        bag.InventoryItems.Clear();
        foreach (var name in new[] { "Bread", "Oblivion Draught", "Arcane Wand", "Frailty Potion", "Explosion Spell",
            "Bow", "Wooden Buckler", "Wooden Arrows", "Antidote", "Mana Potion" })
            bag.InventoryItems.Add(new ItemSaveData { ItemName = name }.Restore(Common.Instance.ItemManager));
        var actor = game.PlayerController.ControlledAlly;
        if (tab == PartyMenuTab.Skills)
        {
            foreach (var skill in actor.Skills) Object.Destroy(skill);
            actor.Skills.Clear();
            foreach (var name in new[] { "Fire Bolt", "Ice Bolt", "Lightning Bolt", "Fireball", "Blizzard",
                "Thunderclap", "Inferno", "Glacier", "Tempest", "Amplify" })
                actor.Skills.Add(Common.Instance.SkillManager.GetSkillInstanceByName(name));
            actor.InvalidateCachedStats();
        }
        actor.Vitals.Hunger = 20;
        actor.SyncDisplayedStats();
        GameMessages.BeginTurn();
        GameMessages.Post("The party is ready to explore.");
        MenuManager.Instance.OpenPartyMenu(tab);
        yield return Delay(.3);
        var reviewMenu=MenuManager.Instance.PartyMenu;
        bool equipmentDescriptionReview=SessionState.GetBool(Key + ".EquipmentDescriptionReview",false);
        (tab==PartyMenuTab.Stats
            ?reviewMenu.EntryButtons.First(button=>button.GetComponent<AuthoredButton>().Label.text.StartsWith("STR "))
            :reviewMenu.EntryButtons[0]).Select();
        if(equipmentDescriptionReview)
            reviewMenu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text.StartsWith("Wooden Arrows")).Select();
        if(tab==PartyMenuTab.Equipment)
        {
            reviewMenu.EntryButtons[0].onClick.Invoke();
            yield return Delay(.2);
            var choices=(PartyMenuPicker)MenuManager.Instance.CurrentDialog;
            choices.Rows.GetComponentsInChildren<AuthoredButton>().Single(row=>row.Label.text=="Arcane Wand").Button.Select();
        }
        view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view,
            new object[] { new Vector2(1280, 720), tab + " review" });
        view.Focus();
        yield return Delay(.5);
        bool historyReview = SessionState.GetBool(Key + ".HistoryReview", false);
        bool feedReview=SessionState.GetBool(Key + ".FeedReview",false);
        if(feedReview)
        {
            reviewMenu.CloseDialog();
            GameMessages.BeginTurn();
            GameMessages.Post("Avery attacked Practice Target!");
            GameMessages.Post("Practice Target took 10 damage.");
            GameMessages.Post("Avery attacked Practice Target!");
            GameMessages.Post("Practice Target took 12 damage.");
            yield return Delay(.3);
        }
        if (historyReview)
        {
            GameMessages.ShowHistory();
            yield return Delay(.3);
        }
        Canvas.ForceUpdateCanvases();
        string captureFolder = equipmentDescriptionReview || tab==PartyMenuTab.Equipment ? "Temp/EquipmentComparison" : partyReview ? "Temp/PartyPortraitLayout" : feedReview ? "Temp/HistoryLayoutCapture" : historyReview ? "Temp/HistoryShortcut" : tab == PartyMenuTab.Stats ? "Temp/StatsDescriptions" : tab == PartyMenuTab.Skills ? "Temp/SkillDescriptions" : "Temp/InventoryDescriptions";
        Directory.CreateDirectory(captureFolder);
        ScreenCapture.CaptureScreenshot(captureFolder + (tab == PartyMenuTab.Stats && !feedReview ? "/stats-preview.png" : "/live-preview.png"));
        yield return Delay(.3);
        int animation = SessionState.GetInt(Key + ".Animation", -1);
        DungeonPreferences.AnimationOverride = animation < 0 ? null : (DungeonAnimationMode)animation;
        string selection = equipmentDescriptionReview ? "Wooden Arrows description and equip preview selected" : tab==PartyMenuTab.Equipment ? "Arcane Wand equip preview selected" : partyReview ? "four party portraits with the reviewed layout" : feedReview ? "four-line event feed" : historyReview ? "expanded event feed" : tab == PartyMenuTab.Skills ? "ten skills, Fire Bolt selected" :
            tab == PartyMenuTab.Inventory ? "ten items, Bread selected" : "Stats with STR explanation and event feed";
        File.AppendAllText(Folder + "/Progress.txt", $"{tab} review ready: {selection}. Play Mode remains open with physical input enabled and saves isolated.\n");
        Debug.Log(tab + " review is ready. Stop Play Mode when finished; existing saves are unchanged.");
        view.Focus();
    }

    private static IEnumerator HomeInteraction(Town town, Vector3Int target, string name)
    {
        if (!town.HomeLesson.TryGetInteractionStand(target, out var standing))
            throw new InvalidOperationException("No approach to " + town.HomeLesson.InteractionName(target));
        StageAt(town, standing);
        town.TownPlayer.ControllingTownAlly.SetFacing(Character.GetFacing(target - standing));
        yield return Delay(.2);
        town.HomeLesson.TryInteract(target);
        yield return Pair(name);
        if (Common.Instance.MessageDialog.gameObject.activeInHierarchy)
            Common.Instance.MessageDialog.Ok_Clicked();
        yield return Delay(.2);
    }
    private static void StageAt(Town town, Vector3Int cell)
    {
        var hero = town.TownPlayer.ControllingTownAlly;
        hero.TilemapPosition = cell; hero.transform.position = town.WalkableMap.CellToWorld(cell);
        town.RefreshRoofs(); town.TownPlayer.CameraController.SnapToFollowTarget();
    }
    private static IEnumerator Pair(string name)
    {
        foreach (var size in new[] { new Vector2(960, 600), new Vector2(1280, 720) })
        {
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { size, "Opening design preview" });
            view.Focus(); yield return Delay(.8); Canvas.ForceUpdateCanvases();
            string path = $"{Folder}/{name}-{(int)size.x}x{(int)size.y}.png";
            ScreenCapture.CaptureScreenshot(path); yield return Delay(.6);
            File.AppendAllText(Folder + "/Progress.txt", "Captured " + path + "\n");
            if (Game.Instance != null)
            {
                var map = Object.FindFirstObjectByType<Minimap>();
                var location = map.transform.Find("Map location");
                File.AppendAllText(Folder + "/Progress.txt", $"Map review: image={map.minimapImage.gameObject.activeSelf}, location={location?.gameObject.activeInHierarchy}, canvas={map.GetComponentInParent<Canvas>().enabled}, text={location?.GetComponentInChildren<TMPro.TMP_Text>(true)?.text}\n");
                if (location != null)
                    foreach (var graphic in location.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                        File.AppendAllText(Folder + "/Progress.txt", $"Map graphic {graphic.name}: enabled={graphic.enabled}, active={graphic.isActiveAndEnabled}, color={graphic.color}, rendererAlpha={graphic.canvasRenderer.GetAlpha()}, cull={graphic.canvasRenderer.cull}, position={graphic.rectTransform.position}, rect={graphic.rectTransform.rect}, scale={graphic.transform.lossyScale}\n");
            }
        }
    }
}
