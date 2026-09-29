using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// An editor playground. The small AbilityTestLab scene loads the real dungeon and its UI.
// No scene, skill asset, preference, or player save is changed by the session.
public sealed class AbilityTestLab : MonoBehaviour
{
    public Skill[] Abilities;
    public TownAlly[] PartyPrefabs;
    public Enemy TargetPrefab;
    public static AbilityTestLab Active { get; private set; }
    public bool Ready { get; private set; }
    public bool Busy { get; private set; }
    public string Status { get; private set; } = "Loading ability playground...";
    public Ally Caster { get; private set; }
    public Ally Friend { get; private set; }
    public Enemy Target { get; private set; }
    readonly List<Skill> learned = new();
    readonly HashSet<Skill> tested = new();
    readonly List<Enemy> dummies = new();
    IDisposable saveScope;
    bool? previousFullControl;
    DungeonAnimationMode? previousAnimation;
    Vector3Int center;
    Skill selected;
    string search = "";
    Vector2 scroll;
    int tab, distance = 1, rank = 1;
    bool resetBeforeCast = true;
    public bool CastingAll { get; private set; }
    bool stopCastingAll, lastCastSucceeded;
    string batchProgress = "";
    readonly List<string> batchSkipped = new();
    string lastResult = "Select an ability, then Cast. Mana is unlimited.";
    Game Game => Game.Instance;

    sealed class LabSaveStore : ISaveStore
    {
        public string Read() => null;
        public void Write(string json) { }
        public void Clear() { }
    }

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Active != null || FindFirstObjectByType<Common>() != null)
        {
            Status = "Start this scene from Edit Mode, outside an existing game session.";
            yield break;
        }
        Active = this;
        DontDestroyOnLoad(gameObject);
        saveScope = SaveSystem.UseStore(new LabSaveStore());
        previousFullControl = DungeonPreferences.FullControlOverride;
        previousAnimation = DungeonPreferences.AnimationOverride;
        DungeonPreferences.FullControlOverride = false;
        DungeonPreferences.AnimationOverride = DungeonAnimationMode.Current;
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Common.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var common = Common.Instance;
        common.GameSaveData = new GameSaveData {
            IsSandbox = true,
            DungeonSaveData = new DungeonSaveData { StartFloor = 1, EndFloor = 5 },
            TownSaveData = new TownSaveData { TownSeed = 12345 }
        };
        foreach (var prefab in PartyPrefabs)
        {
            var ally = Instantiate(prefab, common.TownAllyParent);
            ally.Skills.Clear();
            common.InstantiatedTownAllies.Add(ally);
        }
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/DungeonScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
        float deadline = Time.realtimeSinceStartup + 90;
        while (Game == null || !Game.IsReady)
        {
            if (Time.realtimeSinceStartup > deadline) { Status = "Dungeon setup timed out. Check the Console."; yield break; }
            yield return null;
        }
        // The lab resolves one requested action at a time; AI and ordinary keyboard commands stay paused.
        Game.PlayerController.enabled = false;
        Game.TurnManager.enabled = false;
        Game.NewFloorMessage.gameObject.SetActive(false);
        Caster = Game.PlayerController.ControlledAlly;
        Friend = Game.Allies.First(a => a != Caster);
        Caster.CharacterName = "Ability tester";
        Friend.CharacterName = "Friendly target";
        foreach (var source in Abilities.OrderBy(s => s.SkillName))
        {
            var copy = Instantiate(source);
            copy.SPCost = 0; // Unlimited mana only on lab-owned skill instances.
            learned.Add(copy);
            Caster.Skills.Add(copy);
        }
        foreach (var ally in Game.Allies)
        {
            ally.BaseStats.HPMax = 10000;
            ally.BaseStats.SPMax = 10000;
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            ally.InvalidateCachedStats();
        }
        var dungeon = Game.CurrentDungeon;
        var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
            Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y))).Where(dungeon.IsWalkable).ToList();
        var open = cells.Where(p => Enumerable.Range(-1, 3).All(x => Enumerable.Range(-1, 3)
            .All(y => dungeon.IsWalkable(p + new Vector3Int(x, y))))).ToList();
        if (open.Count == 0) { Status = "No open test area found. Restart the scene."; yield break; }
        center = open.OrderByDescending(p => Enumerable.Range(1, 6).TakeWhile(n => dungeon.IsWalkable(p + Vector3Int.right * n)).Count()).First();
        foreach (var enemy in Game.Enemies.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
        Game.Enemies.Clear();
        foreach (var item in dungeon.Interactables.ToArray()) dungeon.RemoveInteractable(item);
        ResetArena();
        selected = learned.FirstOrDefault(s => s.SkillName == "Fire Bolt") ?? learned.First();
        Ready = true;
        Status = $"Ready: {learned.Count} abilities | infinite mana | AI paused";
        Debug.Log("AbilityTestLab: " + Status);
#else
        Status = "Open AbilityTestLab in the Unity Editor and press Play.";
        yield break;
#endif
    }

    void Update()
    {
        if (!Ready || Game == null || Caster == null || Caster.Vitals == null) return;
        Caster.Vitals.SP = Caster.FinalStats.SPMax;
        // Reset spawns targets during a coroutine; their Start may not have initialized AI yet.
        foreach (var dummy in dummies) if (dummy != null) dummy.Policies?.Clear();
    }

    public void ResetArena()
    {
        if (Busy || Caster == null) return;
        CombatEffectPlayer.Get()?.Clear();
        foreach (var summon in Game.Allies.Where(PartyRules.IsSummon).ToArray())
        { Game.Allies.Remove(summon); Destroy(summon.gameObject); }
        PartyRules.MarkStanding(Game, Friend);
        foreach (var actor in Game.AllCharacters.Concat(Game.DownedAllies).Distinct().ToArray())
        {
            foreach (var status in actor.StatusEffects.ToArray())
            { actor.RemoveStatusEffect(status); if (status != null) Destroy(status.gameObject); }
            actor.InvalidateCachedStats();
        }
        foreach (var dummy in dummies) if (dummy != null) { Game.Enemies.Remove(dummy); Destroy(dummy.gameObject); }
        dummies.Clear();
        foreach (var item in Game.CurrentDungeon.Interactables.ToArray()) Game.CurrentDungeon.RemoveInteractable(item);
        Caster.SetPosition(center);
        Friend.SetPosition(center + Vector3Int.left);
        Caster.Vitals.HP = Caster.FinalStats.HPMax / 2;
        Friend.Vitals.HP = Friend.FinalStats.HPMax / 2;
        Friend.Vitals.SP = Friend.FinalStats.SPMax / 4;
        Caster.Vitals.SP = Caster.FinalStats.SPMax;
        Caster.PlayIdleAnimation(); Friend.PlayIdleAnimation();
        int actualDistance = Enumerable.Range(1, distance).TakeWhile(n => Game.CurrentDungeon.IsWalkable(center + Vector3Int.right * n)).Count();
        var targetCell = center + Vector3Int.right * Mathf.Max(1, actualDistance);
        SpawnDummy(targetCell);
        if (Game.CurrentDungeon.IsWalkable(targetCell + Vector3Int.up)) SpawnDummy(targetCell + Vector3Int.up);
        Target = dummies[0];
        Caster.SetFacingByTargetPosition(Target.TilemapPosition);
        RefillInventory();
        foreach (var actor in Game.AllCharacters) actor.SyncDisplayedStats();
        Game.RefreshSight(); Game.UpdateMiniMap();
        lastResult = "Arena reset. Friendly target and caster are wounded for heal tests.";
    }

    void SpawnDummy(Vector3Int cell)
    {
        var dummy = Instantiate(TargetPrefab, Game.transform);
        dummy.InitialzeVitalsFromStats();
        dummy.BaseStats.HPMax = 1000000;
        dummy.InvalidateCachedStats();
        dummy.Vitals.HP = dummy.FinalStats.HPMax;
        dummy.CharacterName = dummies.Count == 0 ? "Primary target" : "Area target";
        dummy.SetPosition(cell);
        dummy.SyncDisplayedStats();
        Game.Enemies.Add(dummy); dummies.Add(dummy);
    }

    void RefillInventory()
    {
        var inventory = Game.PlayerController.Inventory;
        inventory.Clear(); inventory.MaxItems = 1000;
        foreach (var definition in Common.Instance.ItemManager.ItemDefinitions)
        {
            var item = definition.AsInventoryItem(null);
            if (item.HasStacks) item.StackStock = 999;
            inventory.Add(item);
        }
        Caster.Equipment.ClassFilter = null;
        foreach (var type in new[] { WeaponType.SingleSword, WeaponType.OffhandShield })
        {
            var item = inventory.InventoryItems.OfType<EquipableInventoryItem>()
                .FirstOrDefault(i => i.EquipmentItemDefinition.WeaponType == type);
            if (item != null) Caster.Equipment.Equip(item);
        }
        Caster.InvalidateCachedStats();
    }

    public IEnumerator CastAbility(string skillName)
    {
        lastCastSucceeded = false;
        var skill = learned.FirstOrDefault(s => s.SkillName == skillName);
        if (!Ready || Busy || skill == null) yield break;
        selected = skill;
        if (resetBeforeCast) ResetArena();
        var weapon = Game.PlayerController.Inventory.InventoryItems.OfType<EquipableInventoryItem>()
            .FirstOrDefault(i => i.EquipmentItemDefinition.WeaponType == (skill.UsesArrows ? WeaponType.BowAndArrow : WeaponType.SingleSword));
        if (weapon != null) Caster.Equipment.Equip(weapon);
        if (!skill.UsesArrows)
        {
            var shield = Game.PlayerController.Inventory.InventoryItems.OfType<EquipableInventoryItem>()
                .FirstOrDefault(i => i.EquipmentItemDefinition.WeaponType == WeaponType.OffhandShield);
            if (shield != null) Caster.Equipment.Equip(shield);
        }
        skill.Rank = rank; Caster.InvalidateCachedStats();
        if (skill.ActivationType == ActivationType.Passive)
        { lastResult = "Passive is learned and contributes its stats/responses. Use Preview effects to inspect its assigned VFX."; yield break; }
        if (skill.ActionEffects.OfType<ReviveAction>().Any())
        { Friend.Vitals.HP = 0; PartyRules.MarkDowned(Game, Friend); Friend.SyncDisplayedStats(); }
        if (skill.ActionEffects.OfType<DisarmTrapAction>().Any())
        { Game.CurrentDungeon.SetTrap(Caster.TilemapPosition + Vector3Int.down, 0); Game.CurrentDungeon.RevealTrapsAround(Caster.TilemapPosition, 1); }
        if (!Caster.CanCast(skill, out var reason)) { lastResult = reason; yield break; }
        GameAction action;
        if (skill.Targeting == SkillTargeting.InventoryItem)
        {
            var item = skill.GetInventoryTargets(Caster).FirstOrDefault();
            if (item == null) { lastResult = "No eligible inventory item. Reset the arena to replenish the bag."; yield break; }
            action = SkillAction.ForInventoryItem(Caster, skill, item);
        }
        else if (skill.Targeting == SkillTargeting.Missile)
            action = SkillAction.ForMissile(Caster, skill, Vector3Int.right);
        else
        {
            var candidates = skill.GetTargetCharacters(Caster);
            Character recipient = candidates.Contains(Target) ? Target : candidates.Contains(Friend) ? Friend : Caster;
            action = new SkillAction(Caster, skill, recipient);
        }
        if (!action.IsValid(Caster)) { lastResult = "No valid target in range. Set target distance to 1 and reset."; yield break; }
        yield return Resolve(action);
        tested.Add(skill);
        lastCastSucceeded = true;
        lastResult = $"Cast {skill.SkillName} (rank {rank}). Target HP: {Target?.Vitals.HP}.";
    }

    public void StopCastingAll() => stopCastingAll = true;

    public IEnumerator CastAllAbilities()
    {
        if (!Ready || Busy || CastingAll) yield break;
        CastingAll = true; stopCastingAll = false; batchSkipped.Clear();
        bool previousReset = resetBeforeCast;
        int previousDistance = distance, cast = 0, previewed = 0, completed = 0;
        var queue = learned.OrderBy(s => s.ActivationType).ThenBy(s => s.SkillName).ToArray();
        Debug.Log($"Ability Test Lab: running {queue.Length} abilities (active casts, then passive previews).");
        try
        {
            // Isolate each demonstration and put melee targets in reach.
            resetBeforeCast = true; distance = 1;
            foreach (var skill in queue)
            {
                if (stopCastingAll) break;
                selected = skill;
                bool passive = skill.ActivationType == ActivationType.Passive;
                batchProgress = $"{completed + 1}/{queue.Length}: {skill.SkillName}" + (passive ? " (passive preview)" : "");
                string failure = null;
                if (passive) ResetArena();
                yield return RunBatchStep(passive ? Preview() : CastAbility(skill.SkillName), error => failure = error);
                completed++;
                if (failure != null || !passive && !lastCastSucceeded)
                    batchSkipped.Add(skill.SkillName + ": " + (failure ?? lastResult));
                else if (passive) previewed++;
                else cast++;
                // Leave a short viewing interval; Stop also works between abilities.
                float until = Time.realtimeSinceStartup + .75f;
                while (!stopCastingAll && Time.realtimeSinceStartup < until) yield return null;
            }
        }
        finally
        {
            resetBeforeCast = previousReset; distance = previousDistance;
            CastingAll = false;
            batchProgress = $"{(stopCastingAll ? "Stopped" : "Finished")}: {cast} cast, {previewed} passive previews, {batchSkipped.Count} skipped.";
            lastResult = batchProgress;
            Debug.Log("Ability Test Lab: " + batchProgress);
            if (batchSkipped.Count > 0) Debug.Log("Ability Test Lab skipped abilities:\n" + string.Join("\n", batchSkipped));
        }
    }

    // Flatten nested routines so one failing ability cannot strand the batch or leave Busy set.
    static IEnumerator RunBatchStep(IEnumerator routine, Action<string> failed)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                var current = stack.Peek(); object next = null; bool moved = false; Exception error = null;
                try { moved = current.MoveNext(); if (moved) next = current.Current; }
                catch (Exception exception) { error = exception; }
                if (error != null) { failed(error.Message); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }

    IEnumerator Resolve(GameAction root)
    {
        Busy = true;
        try
        {
            GameMessages.BeginTurn();
            var pending = new Queue<GameAction>(); pending.Enqueue(root);
            var replay = new List<GameAction>();
            var context = new DungeonActionPlayback(true, Caster, Caster);
            int guard = 0;
            while (pending.Count > 0)
            {
                if (++guard > 1000) throw new InvalidOperationException("Ability response chain exceeded 1000 actions.");
                var action = pending.Dequeue();
                action.SetPlaybackContext(context);
                foreach (var child in Caster.ExecuteActionImmediate(action)) if (child != null) pending.Enqueue(child);
                replay.Add(action);
                foreach (var actor in Game.AllCharacters.ToArray())
                {
                    foreach (var response in actor.GetResponseTo(action)) if (response != null) pending.Enqueue(response);
                    foreach (var response in actor.GetClassResponses(action)) if (response != null) pending.Enqueue(response);
                }
            }
            // Retreat can be exercised without leaving the test floor.
            Game.PlayerController.PendingRetreat = false;
            foreach (var action in replay) yield return Caster.ExecuteActionRoutine(action);
            Caster.Vitals.SP = Caster.FinalStats.SPMax;
            foreach (var actor in Game.AllCharacters) actor.SyncDisplayedStats();
            Game.RefreshSight(); Game.UpdateMiniMap(); GameMessages.FinishAction();
        }
        finally { Busy = false; }
    }

    IEnumerator Preview()
    {
        if (selected?.VisualProfile == null || Busy) yield break;
        GameMessages.BeginTurn();
        GameMessages.ForCharacter(Caster, $"[Preview] {GameMessages.Name(Caster)} previews {selected.SkillName} effects (no cast).");
        Busy = true;
        try
        {
            var sequence = new CombatVisualSequence {
                Actor = Caster, Profile = selected.VisualProfile,
                Origin = CombatEffectPlayer.Body(Caster, Caster.TilemapPosition),
                Center = CombatEffectPlayer.Body(Target, Target.TilemapPosition), Radius = selected.AreaRadius
            };
            yield return CombatEffectPlayer.Get().Cast(sequence);
            yield return CombatEffectPlayer.Get().Deliver(sequence, sequence.Center, true, CombatEffectPlayer.TargetBounds(Target, Target.TilemapPosition));
            lastResult = "Visual preview only: " + selected.SkillName;
        }
        finally { Busy = false; }
    }

    void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.height / 850f, .85f, 1.4f);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float height = Screen.height / scale;
        var leftPanel = new Rect(12, 12, 320, height - 24);
        DrawPanel(leftPanel);
        GUILayout.BeginArea(leftPanel, GUI.skin.box);
        GUILayout.Label("ABILITY TEST LAB", new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
        GUILayout.Label(Status);
        if (Ready)
        {
            search = GUILayout.TextField(search);
            tab = GUILayout.Toolbar(tab, new[] { "All", "Active", "Passive" });
            GUILayout.Label($"{tested.Count} cast / {learned.Count(s => s.ActivationType == ActivationType.Active)} active abilities");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var skill in learned.Where(s => (tab == 0 || (tab == 1) == (s.ActivationType == ActivationType.Active)) &&
                s.SkillName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                GUILayout.BeginHorizontal();
                var rect = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28));
                if (skill.Icon != null)
                {
                    var r = skill.Icon.textureRect; var texture = skill.Icon.texture;
                    GUI.DrawTextureWithTexCoords(rect, texture, new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height));
                }
                if (GUILayout.Button((selected == skill ? "> " : "") + skill.SkillName + (tested.Contains(skill) ? "  ✓" : ""), GUILayout.Height(28)) && !CastingAll) selected = skill;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
        GUILayout.EndArea();
        if (Ready && selected != null)
        {
            var rightPanel = new Rect(Screen.width / scale - 332, 12, 320, height - 24);
            DrawPanel(rightPanel);
            GUILayout.BeginArea(rightPanel, GUI.skin.box);
            GUILayout.Label(selected.SkillName, new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold });
            GUILayout.Label(selected.Description, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label($"{selected.ActivationType} • {selected.Targeting}\nMana: unlimited (lab copy costs 0)");
            GUI.enabled = !Busy && !CastingAll;
            GUILayout.Label("Rank"); rank = GUILayout.Toolbar(rank - 1, new[] { "1", "2", "3", "4", "5" }) + 1;
            resetBeforeCast = GUILayout.Toggle(resetBeforeCast, "Reset arena before each cast");
            GUILayout.Label("Target distance (tiles)");
            distance = GUILayout.Toolbar(distance == 1 ? 0 : distance == 3 ? 1 : 2, new[] { "1", "3", "5" }) switch { 0 => 1, 1 => 3, _ => 5 };
            if (GUILayout.Button("Cast ability", GUILayout.Height(38))) StartCoroutine(CastAbility(selected.SkillName));
            if (GUILayout.Button("Cast all abilities", GUILayout.Height(32))) StartCoroutine(CastAllAbilities());
            GUI.enabled = CastingAll && !stopCastingAll;
            if (CastingAll && GUILayout.Button("Stop after current ability", GUILayout.Height(30))) StopCastingAll();
            GUI.enabled = !Busy && !CastingAll;
            if (GUILayout.Button("Preview effects only", GUILayout.Height(30))) StartCoroutine(Preview());
            if (GUILayout.Button("Reset targets, statuses and inventory", GUILayout.Height(30))) ResetArena();
            if (GUILayout.Button("Apply poison to friendly target")) StartCoroutine(Resolve(new ApplyStatusEffectAction(Friend, StatusEffectRegistry.GetByName("Dot"), Caster)));
            if (GUILayout.Button("Advance status durations")) StartCoroutine(AdvanceStatuses());
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(batchProgress)) GUILayout.Label(batchProgress, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(12);
            GUILayout.Label(Busy ? "Playing ability..." : lastResult, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("Friendly target: " + Friend.Vitals.HP + " HP\nEnemy target: " + (Target != null ? Target.Vitals.HP : 0) + " HP");
            GUILayout.Label("All passives are learned. Turn off automatic reset to test combinations. Inventory skills select an eligible item automatically; revive skills down the friendly target first. Retreat stays in the lab.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
        }
        GUI.matrix = oldMatrix;
        if (Ready && Camera.main != null)
        {
            foreach (var actor in new Character[] { Caster, Friend, Target })
            {
                if (actor == null) continue;
                var point = Camera.main.WorldToScreenPoint(actor.VisualParent.transform.position + Vector3.up * 2);
                string label = actor == Caster ? "Caster" : actor == Friend ? "Friendly" : "Target";
                if (point.z > 0) GUI.Box(new Rect(point.x - 36, Screen.height - point.y - (actor == Caster ? 50 : 24), 72, 22), label);
            }
        }
    }

    static void DrawPanel(Rect rect)
    {
        var oldColor = GUI.color;
        GUI.color = new Color(.07f, .085f, .11f, 1);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    IEnumerator AdvanceStatuses()
    {
        foreach (var actor in Game.AllCharacters.ToArray()) actor.TickStatusEffects();
        foreach (var actor in Game.AllCharacters.ToArray())
            foreach (var action in actor.GetStatusEffectSideEffects()) yield return Resolve(action);
    }

#if UNITY_EDITOR
    public IEnumerator CaptureMeleeImpact()
    {
        if (!Ready || Busy) yield break;
        StartCoroutine(CastAbility("Double Strike"));
        float deadline = Time.realtimeSinceStartup + 8;
        while (Time.realtimeSinceStartup < deadline)
        {
            var effect = CombatEffectPlayer.Get().GetComponentsInChildren<ParticleSystem>()
                .FirstOrDefault(p => p.transform.parent != null && p.transform.parent.name.StartsWith("LightSlashHit") && p.particleCount > 0);
            if (effect != null)
            {
                System.IO.Directory.CreateDirectory("Temp/AbilityTestLab");
                var details = new List<string> { "Profile scale/offset: " + selected.VisualProfile.Impact.Scale + " / " + selected.VisualProfile.Impact.Offset };
                foreach (var renderer in Target.GetComponentsInChildren<Renderer>()) details.Add("Target " + renderer.name + " bounds " + renderer.bounds);
                foreach (var renderer in CombatEffectPlayer.Get().GetComponentsInChildren<ParticleSystemRenderer>())
                    details.Add(renderer.name + " pos " + renderer.transform.position + " scale " + renderer.transform.lossyScale + " bounds " + renderer.bounds + " hidden " + renderer.forceRenderingOff + " material " + renderer.sharedMaterial?.name);
                System.IO.File.WriteAllLines("Temp/AbilityTestLab/MeleeImpact.txt", details);
                for (int frame = 0; frame < 8; frame++)
                {
                    ScreenCapture.CaptureScreenshot($"Temp/AbilityTestLab/MeleeImpact{frame}.png");
                    yield return new WaitForSecondsRealtime(.05f);
                }
                yield break;
            }
            yield return null;
        }
        Debug.LogWarning("No LightSlashHit particles detected during Double Strike.");
    }

    public IEnumerator SmokeCheck()
    {
        var names = new[] { "Fire Bolt", "Double Strike", "Venom Arrow", "Shield Bash", "Heal", "Cure", "Revive", "Disarm", "Field Kitchen" };
        foreach (var name in names)
        {
            if (!learned.Any(s => s.SkillName == name)) continue;
            yield return CastAbility(name);
            if (!tested.Any(s => s.SkillName == name)) throw new InvalidOperationException(name + ": " + lastResult);
            if (Caster.Vitals.SP != Caster.FinalStats.SPMax) throw new InvalidOperationException("Mana was not replenished.");
        }
        ResetArena();
        System.IO.Directory.CreateDirectory("Temp/AbilityTestLab");
        System.IO.File.WriteAllText("Temp/AbilityTestLab/SmokeCheck.txt", "Ready; " + learned.Count + " learned abilities.\nPassed casts: " + string.Join(", ", tested.Select(s => s.SkillName)));
        ScreenCapture.CaptureScreenshot("Temp/AbilityTestLab/GameView.png");
        Debug.Log("AbilityTestLab smoke check passed: " + string.Join(", ", tested.Select(s => s.SkillName)));
    }
#endif

    void OnDestroy()
    {
        if (Active != this) return;
        Active = null;
        DungeonPreferences.FullControlOverride = previousFullControl;
        DungeonPreferences.AnimationOverride = previousAnimation;
        saveScope?.Dispose();
        foreach (var skill in learned) if (skill != null) Destroy(skill);
    }
}
