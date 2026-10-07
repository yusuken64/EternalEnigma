#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Editor Play Mode only: loads production scenes and prefab data, never writes PlayerPrefs.
public sealed class GameTestHarness
{
    public Game Game => Game.Instance;
    public Ally Ally => Game.PlayerController.ControlledAlly;
    public MemorySaveStore Store { get; private set; }
    public float TimeoutSeconds = 60;
    private IDisposable saveScope;
    private UnityEngine.Random.State randomState;
    private readonly List<Object> ownedAssets = new();
    private HashSet<int> existingPersistentRoots;
    private bool started;
    private Scene originalScene;

    public IEnumerator LoadCommon(GameSaveData save = null)
    {
        Begin(save ?? new TestScenario().CreateSave());
        yield return LoadScene("Common");
    }

    public IEnumerator LoadDungeon(TestScenario scenario)
    {
        Begin(scenario.CreateSave());
        yield return LoadScene("Common");
        var common = Common.Instance;
        if (scenario.IncludeStartingItems)
            common.GameSaveData.TownSaveData.InventoryItems.AddRange(ItemSaveData.Capture(common.ItemManager.StartingItems.Select(i => i.AsInventoryItem(null))));
        foreach (var item in scenario.Items) RequireItem(item);

        foreach (var allyName in new[] { scenario.AllyName }.Concat(scenario.AdditionalAllies))
        {
            var prefab = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Town/Allies" })
                .Select(g => AssetDatabase.LoadAssetAtPath<TownAlly>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null && a.Name == allyName);
            Assert.That(prefab, Is.Not.Null, $"No ally prefab named '{allyName}'.");
            var townAlly = Object.Instantiate(prefab, common.TownAllyParent);
            townAlly.Skills = scenario.Skills.ToList();
        }

        void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "DungeonScene") return;
            var generator = Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
            Seed(generator.TileWorldCreator, scenario.Seed);
            Seed(generator.ThroneTileWorldCreator, scenario.Seed);
        }
        SceneManager.sceneLoaded += Configure;
        try { yield return LoadScene("DungeonScene"); }
        finally { SceneManager.sceneLoaded -= Configure; }
        yield return WaitUntil(() => Game != null && Game.IsReady, "dungeon initialization");
        if (scenario.HP.HasValue) Ally.Vitals.HP = scenario.HP.Value;
        if (scenario.SP.HasValue) Ally.Vitals.SP = scenario.SP.Value;
        Ally.SyncDisplayedStats();
        yield return WaitForIdle();
    }

    public IEnumerator LoadTown(GameSaveData save, TownConfiguration configuration = null)
    {
        Begin(save);
        yield return LoadScene("Common");
        TownSceneLoader.Configure(configuration ?? TownSceneLoader.Default);
        void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Town")
                Seed(Object.FindFirstObjectByType<Town>().WalkableMap.TileWorldCreator,
                    save.TownSaveData.TownSeed);
        }
        SceneManager.sceneLoaded += Configure;
        try { yield return LoadScene("Town"); }
        finally { SceneManager.sceneLoaded -= Configure; }
        yield return WaitUntil(() => Object.FindFirstObjectByType<TownPlayer>()?.ControllingTownAlly != null
            && !Common.Instance.ScreenTransition.BlockScreen.activeSelf, "town initialization");
    }

    public IEnumerator LoadMainMenu(GameSaveData save)
    {
        Begin(save ?? new TestScenario().CreateSave());
        if (save == null) SaveSystem.ClearData();
        yield return LoadScene("Common");
        yield return LoadScene("MainMenu");
    }

    public IEnumerator LoadMainMenuDirect()
    {
        Begin(new TestScenario().CreateSave());
        yield return LoadScene("MainMenu");
        yield return WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "direct main menu startup");
    }

    private void Begin(GameSaveData save)
    {
        if (started) throw new InvalidOperationException("Use a fresh harness for each scenario, after Cleanup.");
        Assert.That(Object.FindFirstObjectByType<Common>(), Is.Null,
            "Run from Test Runner outside an existing game session.");
        started = true;
        originalScene = SceneManager.GetActiveScene();
        randomState = UnityEngine.Random.state;
        existingPersistentRoots = PersistentRoots().Select(o => o.GetInstanceID()).ToHashSet();
        Store = new MemorySaveStore();
        saveScope = SaveSystem.UseStore(Store);
        SaveSystem.SaveData(save); // Same JSON serialization as the real save path.
        UnityEngine.Random.InitState(save.TownSaveData.TownSeed);
    }

    private void Seed(TileWorldCreator creator, int seed)
    {
        // TWC.SetCustomRandomSeed mutates its ScriptableObject; clone to protect project assets.
        creator.twcAsset = Object.Instantiate(creator.twcAsset);
        ownedAssets.Add(creator.twcAsset);
        creator.SetCustomRandomSeed(seed);
    }

    private IEnumerator LoadScene(string name)
    {
        var operation = EditorSceneManager.LoadSceneAsyncInPlayMode($"Assets/Scenes/{name}.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return WaitUntil(() => operation.isDone, $"loading {name}");
        yield return null; // Awake and Start must finish before inspecting instances.
    }

    public InventoryItem AddItem(string name)
    {
        var item = RequireItem(name).AsInventoryItem(null);
        Game.PlayerController.Inventory.Add(item);
        return item;
    }

    private ItemDefinition RequireItem(string name)
    {
        var definition = Common.Instance.ItemManager.ItemDefinitions.FirstOrDefault(d => d.ItemName == name);
        Assert.That(definition, Is.Not.Null, $"Unknown item '{name}'. Use ItemDefinition.ItemName, not its filename.");
        return definition;
    }

    public void PlaceAlly(Vector3Int position)
    {
        Assert.That(Game.IsReady && !Game.TurnManager.IsProcessingTurn, Is.True, "Wait for the dungeon/turn first.");
        Assert.That(Game.CurrentDungeon.IsWalkable(position), Is.True, $"Tile {position} is not walkable.");
        Assert.That(Game.AllCharacters.Any(c => c != Ally && c.TilemapPosition == position), Is.False, "Tile is occupied.");
        Ally.SetPosition(position);
        Ally.currentInteractable = Game.CurrentDungeon.GetInteractable(position);
        Ally.MovedThisTurn = false;
        Game.UpdateMiniMap();
    }

    public void PlaceBesideStairs()
    {
        var stairs = Game.CurrentDungeon.Interactables.OfType<Stairs>().Single();
        foreach (var offset in new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left })
        {
            var position = stairs.Position + offset;
            if (!Game.CurrentDungeon.IsWalkable(position) ||
                Game.AllCharacters.Any(c => c != Ally && c.TilemapPosition == position)) continue;
            PlaceAlly(position);
            Ally.SetFacingByTargetPosition(stairs.Position);
            return;
        }
        Assert.Fail("No free walkable cardinal neighbor of the stairs.");
    }

    public IEnumerator ExecuteAction(GameAction action)
    {
        yield return WaitForIdle();
        Ally.SetAction(action); // Real turn execution and animation.
        yield return null;
        yield return WaitForIdle();
    }

    public IEnumerator UseItemThroughMenu(InventoryItem item)
    {
        yield return WaitForIdle();
        if (Ally.Equipment.IsEquipped(item))
        {
            MenuManager.Instance.OpenPartyMenu(PartyMenuTab.Equipment, Ally);
            var slot = ((EquipableInventoryItem)item).EquipmentSlot;
            int row = slot == EquipmentSlot.Accessory ? 2 : slot == EquipmentSlot.OffHand ? 1 : 0;
            MenuManager.Instance.PartyMenu.EntryButtons[row].onClick.Invoke();
            var picker = (PartyMenuPicker)MenuManager.Instance.CurrentDialog;
            picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "Unequip").Button.onClick.Invoke();
            yield return null;
            yield return WaitForIdle();
            yield break;
        }
        var items = Game.PlayerController.Inventory.InventoryItems.Where(i => i != null).ToList();
        int index = items.IndexOf(item);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), "Item must be equipped or in the bag.");
        MenuManager.Instance.OpenInventoryAs(Ally);
        MenuManager.Instance.PartyMenu.EntryButtons[index].onClick.Invoke();
        var dialog = MenuManager.Instance.ActionDialog;
        var useButton = dialog.UseItemText.GetComponentInParent<UnityEngine.UI.Button>();
        Assert.That(useButton, Is.Not.Null, "Equip/Use label must belong to a button.");
        useButton.onClick.Invoke(); // Exercise the scene's serialized UnityEvent, not a duplicate equip implementation.
        yield return null;
        yield return WaitForIdle();
    }

    internal IEnumerator ExecuteSkillToCompletion(SkillAction action)
    {
        Assert.That(Ally.CanCast(action.Skill, out var reason), Is.True, action.Skill.SkillName + ": " + reason);
        Assert.That(action.IsValid(Ally), Is.True, action.Skill.SkillName + ": invalid target");
        yield return ExecuteAction(action);
        for (int step = 0; Ally.PendingCast != null && step < 32; step++)
            yield return ExecuteAction(new WaitAction());
        Assert.That(Ally.PendingCast, Is.Null, "The skill must finish its charging actions.");
    }

    public IEnumerator SpawnEnemy(string prefabName, Vector3Int position)
    {
        yield return WaitForIdle();
        Assert.That(Game.CurrentDungeon.IsWalkable(position), Is.True, "Enemy tile must be walkable.");
        Assert.That(Game.AllCharacters.Any(c => c.TilemapPosition == position), Is.False, "Enemy tile is occupied.");
        var prefab = AssetDatabase.LoadAssetAtPath<Enemy>($"Assets/Prefabs/Dungeon/Enemies/{prefabName}.prefab");
        Assert.That(prefab, Is.Not.Null, $"Unknown enemy prefab '{prefabName}'.");
        var enemy = Object.Instantiate(prefab, Game.transform);
        enemy.InitialzeVitalsFromStats();
        enemy.SyncDisplayedStats();
        enemy.SetPosition(position);
        Game.Enemies.Add(enemy);
        yield return null; // Enemy.Start installs its AI policies.
    }

    public IEnumerator WaitForIdle()
    {
        yield return WaitUntil(() => Game != null && Game.IsReady && !Game.TurnManager.IsProcessingTurn
            && !Game.NewFloorMessage.gameObject.activeSelf, "turn/floor animation completion");
    }

    public IEnumerator WaitUntil(Func<bool> condition, string description)
    {
        var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup >= deadline)
                Assert.Fail($"Timed out after {TimeoutSeconds}s waiting for {description}. Check the first Console error.");
            yield return null;
        }
    }

    public IEnumerator Cleanup()
    {
        if (!started) yield break;
        try
        {
            var empty = SceneManager.CreateScene("HarnessCleanup");
            SceneManager.SetActiveScene(empty);
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            foreach (var scene in scenes)
                if (scene != empty && scene != originalScene && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var root in PersistentRoots())
                if (!existingPersistentRoots.Contains(root.GetInstanceID())) Object.Destroy(root);
            foreach (var asset in ownedAssets) if (asset != null) Object.Destroy(asset);
            yield return null;
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            saveScope?.Dispose();
            started = false;
        }
    }

    private static IEnumerable<GameObject> PersistentRoots() => Resources.FindObjectsOfTypeAll<GameObject>()
        .Where(o => o.transform.parent == null && o.scene.IsValid() && o.scene.name == "DontDestroyOnLoad").ToArray();
}

public sealed class MemorySaveStore : ISaveStore
{
    private readonly string[] slots = new string[SaveSystem.SlotCount];
    public string Json => slots[SaveSystem.ActiveSlot];
    public int Writes { get; private set; }
    public bool FailWrites;
    public string Read(int slot = 0) => slots[slot];
    public void Write(int slot, string json) { if(FailWrites) throw new System.IO.IOException("Injected save failure"); slots[slot] = json; Writes++; }
    public void Clear(int slot) => slots[slot] = null;
}

// Synthetic keyboard/mouse events must reach Play Mode even when the Test Runner has focus.
public sealed class TestInputScope : IDisposable
{
    private readonly InputSettings previous = InputSystem.settings;
    private readonly InputSettings settings;
    private readonly InputDevice[] suspendedDevices;

    public TestInputScope()
    {
        // Physical HID reports can replace Gamepad.current between synthetic test events.
        // Disable only devices that were enabled, and restore them when the fixture ends.
        suspendedDevices=InputSystem.devices.Where(device=>device.enabled).ToArray();
        foreach(var device in suspendedDevices)InputSystem.DisableDevice(device);
        settings = Object.Instantiate(previous);
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = settings;
    }

    public void Dispose()
    {
        InputSystem.settings = previous;
        foreach(var device in suspendedDevices)if(device.added)InputSystem.EnableDevice(device);
        Object.DestroyImmediate(settings);
    }
}
#endif
