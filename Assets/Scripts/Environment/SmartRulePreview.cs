using TWC;
using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(TileWorldCreator))]
public sealed class SmartRulePreview : MonoBehaviour
{
    public TileWorldCreatorAsset Template;
    public bool IsReady { get; private set; }
    public GameObject WorldRoot { get; private set; }
    private TileWorldCreatorAsset generated;
    public TileWorldCreator Creator => GetComponent<TileWorldCreator>();
    private void OnEnable() { Creator.OnBlueprintLayersComplete += BlueprintDone; Creator.OnBuildLayersComplete += Built; }
    private void OnDisable() { Creator.OnBlueprintLayersComplete -= BlueprintDone; Creator.OnBuildLayersComplete -= Built; }
    public void Generate()
    {
        IsReady = false;
        if (generated != null) Release(generated);
        generated = Instantiate(Template); generated.hideFlags = HideFlags.DontSave;
        Creator.twcAsset = generated; WorldRoot = Creator.worldObject; Creator.ExecuteAllBlueprintLayers();
    }
    private void BlueprintDone(TileWorldCreator creator) { CoreLayoutCache.ClearResultFlags(creator.twcAsset); creator.ExecuteAllBuildLayers(true); }
    private void Built(TileWorldCreator creator) { IsReady = true; }
    private static void Release(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    private void OnDestroy() { if (generated != null) Release(generated); }
}
