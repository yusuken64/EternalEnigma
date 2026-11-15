using System;
using System.Collections;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Editor playground using the real overworld, capabilities menu, targeting, and gate models.</summary>
public sealed class LockTestLab : MonoBehaviour
{
    public static LockTestLab Active { get; private set; }
    public LockTestWorld Gallery { get; private set; }
    public OverworldScene World { get; private set; }
    public bool Ready { get; private set; }
    public int SelectedIndex { get; private set; }
    public string Status { get; private set; } = "Loading all locks and keys...";
    private IDisposable saveScope;
    private Vector2 scroll;
    private int group;
    private int orientation;
    private bool CanBrowse => Ready && World != null && !World.IsMoving && !World.GetComponent<OverworldMenuManager>().Opened &&
        !Common.Instance.Travel.IsTransitioning && !Common.Instance.GlobalSettings.IsOpen;

    private sealed class LabSaveStore : ISaveStore
    {
        public string Read(int slot) => null;
        public void Write(int slot, string json) { }
        public void Clear(int slot) { }
    }

    private IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Active != null || FindFirstObjectByType<Common>() != null)
        { Status = "Start LockTestLab from Edit Mode, outside an existing game session."; yield break; }
        Active = this;
        DontDestroyOnLoad(gameObject);
        saveScope = SaveSystem.UseStore(new LabSaveStore());
        Gallery = new LockTestWorld();
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Common.unity", new LoadSceneParameters(LoadSceneMode.Single));
        var common = Common.Instance;
        common.GameSaveData = new GameSaveData { IsSandbox = true };
        common.CampaignContext = Gallery.CreateContext();
        void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Overworld") return;
            World = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OverworldScene>()).Single();
            World.ShowLocationMarkers = false;
            World.ShowSandboxControls = false;
        }
        SceneManager.sceneLoaded += Configure;
        try
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Overworld.unity", new LoadSceneParameters(LoadSceneMode.Single));
        }
        finally { SceneManager.sceneLoaded -= Configure; }
        float deadline = Time.realtimeSinceStartup + 120;
        while (World == null || !World.IsReady || common.Travel.IsTransitioning)
        {
            if (Time.realtimeSinceStartup > deadline) { Status = "Lock gallery setup timed out. Check the Console."; yield break; }
            yield return null;
        }
        Ready = true;
        Visit(Gallery.Stations.ToList().FindIndex(station => station.Route.SkinId == LockSkinCatalog.TownGateKeySkin));
#else
        Status = "The lock test scene is available in the Unity Editor.";
        yield break;
#endif
    }

    public bool Visit(int index, bool farSide = false)
    {
        if (!CanBrowse || index < 0 || index >= Gallery.Stations.Count) return false;
        var station = Gallery.Stations[index];
        if (!World.RelocateSandbox(farSide ? station.FarSide : station.Approach)) return false;
        SelectedIndex = index;
        Status = station.Label;
        return true;
    }

    public void ResetLocks(bool all)
    {
        if (!CanBrowse) return;
        foreach (var station in all ? Gallery.Stations : new[] { Gallery.Stations[SelectedIndex] })
        { World.Context.Opened.Remove(station.Route.Id); World.Context.Resolved.Remove(station.Route.Id); }
        Visit(SelectedIndex);
    }

    private void Update()
    {
        if (!CanBrowse || Keyboard.current == null) return;
        if (Keyboard.current.pageDownKey.wasPressedThisFrame) Visit((SelectedIndex + 1) % Gallery.Stations.Count);
        if (Keyboard.current.pageUpKey.wasPressedThisFrame) Visit((SelectedIndex + Gallery.Stations.Count - 1) % Gallery.Stations.Count);
    }

    private void OnGUI()
    {
        if (Ready && !CanBrowse) return;
        GameUISkin.UseLegacySkin();
        float top = Mathf.Min(220, Screen.height * .25f);
        GameUISkin.LegacyBeginArea(new Rect(16, top, 340, Mathf.Min(590, Screen.height - top - 24)));
        GUILayout.Label("LOCK & KEY TEST LAB");
        if (!Ready) { GUILayout.Label(Status); GUILayout.EndArea(); return; }
        var selected = Gallery.Stations[SelectedIndex];
        GUILayout.Label($"{Gallery.Stations.Count} stations | {World.Context.Keys.Count} keys | All capabilities unlocked");
        GUILayout.Label($"{SelectedIndex + 1}. {selected.Label}");
        GUILayout.Label(selected.Route.IsWarp ? "Waystone endpoint" : selected.Horizontal ? "East-west path / rotated gate" : "North-south path");
        GUILayout.Label("Expected: " + (selected.Route.KeyLabel ?? (selected.Route.ShortcutKind == ShortcutKind.FarSide ? "Interact from the far side" : selected.Route.Requirement.ToString())));
        GUILayout.Label(World.Context.Gates.NeedsOpening(selected.Route) ? "LOCKED" : "OPEN");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous")) Visit((SelectedIndex + Gallery.Stations.Count - 1) % Gallery.Stations.Count);
        if (GUILayout.Button("Next")) Visit((SelectedIndex + 1) % Gallery.Stations.Count);
        GUILayout.EndHorizontal();
        if (!selected.Route.IsWarp && GUILayout.Button("Visit the other orientation"))
        {
            string id = selected.Horizontal ? selected.Route.Id.Substring(0, selected.Route.Id.Length - "-horizontal".Length) : selected.Route.Id + "-horizontal";
            Visit(Gallery.Stations.ToList().FindIndex(station => station.Route.Id == id));
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Interact")) World.ClaimRewards();
        if (GUILayout.Button("Visit far side")) Visit(SelectedIndex, true);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset this lock")) ResetLocks(false);
        if (GUILayout.Button("Reset all locks")) ResetLocks(true);
        GUILayout.EndHorizontal();
        group = GUILayout.Toolbar(group, new[] { "All", "Capabilities", "Keys", "Special" });
        orientation = GUILayout.Toolbar(orientation, new[] { "Both directions", "North-south", "East-west" });
        // Category buttons keep keyboard focus available for production movement and interaction.
        scroll = GUILayout.BeginScrollView(scroll);
        for (int i = 0; i < Gallery.Stations.Count; i++)
        {
            var station = Gallery.Stations[i];
            bool special = station.Route.Id.StartsWith("test-");
            if (group == 1 && (station.Route.KeyId != null || special) || group == 2 && station.Route.KeyId == null || group == 3 && !special) continue;
            if (orientation == 1 && station.Horizontal || orientation == 2 && (!station.Horizontal || station.Route.IsWarp)) continue;
            if (GUILayout.Button($"{i + 1}. {station.Label}")) Visit(i);
        }
        GUILayout.EndScrollView();
        GUILayout.Label("Page Up / Page Down: visit a station\nUse the normal Capabilities menu and targeting.\nEscape cancels; History contains the lock's story.");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (Active != this) return;
        Active = null;
        saveScope?.Dispose();
    }
}
