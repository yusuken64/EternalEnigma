using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Standalone art validation scene. Does not load Common or touch campaign saves.</summary>
[ExecuteAlways]
public sealed class EnvironmentPlayground : MonoBehaviour
{
    public EnvironmentKit Kit;
    public Transform Gallery;
    public CampaignOverworld Overworld;
    public TileWorldCreator TownCreator;
    public TileWorldCreatorAsset TownTemplate;
    public SmartRulePreview RulePreview;
    public GameObject RuleLabels;
    public TownConfiguration TownConfiguration;
    public Camera ViewCamera;
    public TMP_Text Status;
    public int Seed = 42;
    public OverworldBiome TownBiome;
    public int View;
    private bool townBuilt, worldBuilt, townBuilding, worldBuilding, overview;
    private TileWorldCreatorAsset townAsset;
    private Transform worldMarkers, townMarkers;
    private GameObject worldRoot, townRoot;

    private void OnEnable()
    {
        if (TownCreator != null) { TownCreator.OnBlueprintLayersComplete += TownBlueprints; TownCreator.OnBuildLayersComplete += TownBuilt; }
        if (Overworld != null) Overworld.TerrainBuilt += WorldBuilt;
    }
    private void Start() { if (Application.isPlaying) ShowGallery(); }
    private void OnDisable()
    {
        if (TownCreator != null) { TownCreator.OnBlueprintLayersComplete -= TownBlueprints; TownCreator.OnBuildLayersComplete -= TownBuilt; }
        if (Overworld != null) Overworld.TerrainBuilt -= WorldBuilt;
    }

    [ContextMenu("Show Asset Gallery")]
    public void ShowGallery() { View = 0; Visibility(); float height=Mathf.Max(24, ((Kit.Models.Length-1)/5)*4); Frame(new Vector3(13,height*.5f,0),height*.5f+5); }
    [ContextMenu("Generate Overworld")]
    public void ShowOverworld()
    {
        View = 1;
        if (!worldBuilt && !worldBuilding) { worldBuilding = true; Overworld.Seed = Seed; Overworld.GenerateAndBuild(); worldRoot = Overworld.GetComponent<TileWorldCreator>().worldObject; }
        Visibility(); if (worldBuilt) FrameWorld();
    }
    [ContextMenu("Generate Town")]
    public void ShowTown()
    {
        View = 2;
        if (!townBuilt && !townBuilding)
        {
            townBuilding = true;
            if (townAsset != null) Release(townAsset);
            townAsset = Instantiate(TownTemplate); townAsset.hideFlags = HideFlags.DontSave;
            townAsset.worldName = "Playground town"; townAsset.randomSeed = Seed; townAsset.useRandomSeed = true;
            TownCreator.twcAsset = townAsset;
            townRoot = TownCreator.worldObject;
            var style = TownCreator.GetComponent<TownBiomeStyle>(); style.OverrideBiome = true; style.Biome = TownBiome;
            CoreTownLayerGenerator.Configure(townAsset, TownConfiguration);
            TownCreator.SetCustomRandomSeed(Seed); CoreLayoutCache.Clear(TownCreator);
            TownCreator.ExecuteAllBlueprintLayers();
        }
        Visibility(); Frame(new Vector3(15, 15, 0), 18);
    }
    public void NextSeed() { if (worldBuilding || townBuilding) return; Seed++; worldBuilt = townBuilt = false; Rebuild(); }
    public void ShowRules()
    {
        View = 3;
        if (!RulePreview.IsReady) RulePreview.Generate();
        Visibility(); Frame(new Vector3(32,24,0),29);
    }
    [ContextMenu("Rebuild Current Seed")]
    public void Rebuild()
    {
        if (worldBuilding || townBuilding) return;
        if (View == 1) { worldBuilt = false; ShowOverworld(); }
        else if (View == 2) { townBuilt = false; ShowTown(); }
        else if (View == 3) { RulePreview.Generate(); Visibility(); }
    }
    public void NextBiome() { if (townBuilding) return; TownBiome = (OverworldBiome)(((int)TownBiome + 1) % 8); townBuilt = false; if (View == 2) ShowTown(); else Visibility(); }
    private void TownBlueprints(TileWorldCreator creator) { CoreLayoutCache.ClearResultFlags(creator.twcAsset); creator.ExecuteAllBuildLayers(true); }
    private void TownBuilt(TileWorldCreator creator)
    {
        townRoot = creator.worldObject;
        townBuilt = true; townBuilding = false;
        if (townMarkers != null) Release(townMarkers.gameObject);
        townMarkers = new GameObject("Town service models").transform;
        townMarkers.SetParent(creator.worldObject.transform, false);
        if (CoreLayoutCache.TryGetTown(creator, out var plan))
            for (int i = 0; i < plan.BuildingSlots.Count && i < TownConfiguration.Buildings.Count; i++)
            {
                string id = TownConfiguration.Buildings[i].Id;
                string model = id == "shop" ? "Shop" : id == "trainer" ? "Trainer" : id == "statue" ? "Shrine" : "DungeonPortal";
                var p = plan.BuildingSlots[i]; Kit.Create(model, TownBiome, townMarkers, new Vector3(p.X + .5f, p.Y + .5f, 0) * creator.twcAsset.cellSize, creator.twcAsset.cellSize * .8f);
            }
        Visibility();
    }
    private void WorldBuilt(TileWorldCreator creator)
    {
        worldRoot = creator.worldObject;
        worldBuilt = true; worldBuilding = false;
        if (worldMarkers != null) Release(worldMarkers.gameObject);
        worldMarkers = new GameObject("Overworld markers").transform; worldMarkers.SetParent(creator.worldObject.transform, false);
        var grid = Overworld.CurrentGrid;
        foreach (var location in grid.Locations)
        {
            if (grid.TownFootprints.Any(t => t.LocationId == location.Key)) continue;
            var p = location.Value;
            Kit.Create(location.Key.Contains("dungeon") || location.Key.Contains("story") || location.Key.Contains("repeatable") ? "DungeonPortal" : "Shrine",
                OverworldCosmetics.Biome(grid, p.X, p.Y), worldMarkers, new Vector3(p.X + .5f, p.Y + .5f, 0) * creator.twcAsset.cellSize, creator.twcAsset.cellSize * .8f);
        }
        foreach (var gate in grid.Locks) foreach (var p in gate.Cells)
            Kit.Create("Gate", OverworldCosmetics.Biome(grid, p.X, p.Y), worldMarkers, new Vector3(p.X + .5f, p.Y + .5f, 0) * creator.twcAsset.cellSize, creator.twcAsset.cellSize);
        Visibility(); if (View == 1) FrameWorld();
    }
    private void FrameWorld()
    {
        if (overview) { Frame(new Vector3(Overworld.Width, Overworld.Height, 0), Overworld.Height * 1.04f); overview = false; return; }
        var p = Overworld.CurrentGrid.PlayerStart;
        Frame(new Vector3(p.X + .5f, p.Y + 2, 0) * Overworld.Template.cellSize, 13);
    }
    public void WorldOverview()
    {
        overview = true; ShowOverworld();
        // A synchronous TWC build also frames the start town before this call returns.
        if(worldBuilt) {overview=false;Frame(new Vector3(Overworld.Width,Overworld.Height,0),Overworld.Height*1.04f);}
    }
    private void Frame(Vector3 center, float size)
    {
        ViewCamera.orthographic = true; ViewCamera.orthographicSize = size;
        ViewCamera.transform.position = center + new Vector3(0, -12, -25);
        ViewCamera.transform.LookAt(center, Vector3.up);
    }
    private void Visibility()
    {
        if (Gallery != null) Gallery.gameObject.SetActive(View == 0);
        if (worldRoot != null) worldRoot.SetActive(View == 1);
        var surface = Overworld.GetComponent<OverworldBiomeRenderer>().RenderedSurfaces;
        if (surface != null) surface.SetActive(View == 1);
        if (townRoot != null) townRoot.SetActive(View == 2);
        if (RulePreview != null && RulePreview.WorldRoot != null) RulePreview.WorldRoot.SetActive(View == 3);
        if (RuleLabels != null) RuleLabels.SetActive(View == 3);
        if (Status != null) Status.text = $"Seed {Seed}   |   Town palette: {TownBiome}   |   1 Gallery / 2 World / 3 Town / 4 Rules / B Biome / R Rebuild\nWASD pans, mouse wheel zooms. Three smart cliff tiers with dense summit noise patches. Cosmetic cap: 48 props/chunk; 120k triangles.";
    }
    private void Update()
    {
        if (!Application.isPlaying) return;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ShowGallery();
            if (keyboard.digit2Key.wasPressedThisFrame) ShowOverworld();
            if (keyboard.digit3Key.wasPressedThisFrame) ShowTown();
            if (keyboard.digit4Key.wasPressedThisFrame) ShowRules();
            if (keyboard.rKey.wasPressedThisFrame) Rebuild();
            if (keyboard.bKey.wasPressedThisFrame) NextBiome();
            var move = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0), 0);
            ViewCamera.transform.position += move * (ViewCamera.orthographicSize * Time.unscaledDeltaTime);
        }
        if (Mouse.current != null) ViewCamera.orthographicSize = Mathf.Clamp(ViewCamera.orthographicSize - Mouse.current.scroll.ReadValue().y * .025f, 3, 300);
    }
    private static void Release(UnityEngine.Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    private void OnDestroy() { if (townAsset != null) Release(townAsset); }
}

