using System.Linq;
using TMPro;
using UnityEngine;

// The campaign owns the lock. This marker only presents its current state.
public sealed class OpeningDungeonGate : MonoBehaviour
{
    private GameObject barrier;
    private TextMeshPro label;
    public bool IsClosed { get; private set; }

    public static void Create(Town town)
    {
        var entrance = town.TownBuildings.Single(b => b.Definition.DialogId == "entrance");
        var root = new GameObject("Opening day entrance");
        root.transform.SetParent(entrance.transform, false);
        float size = town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        root.transform.localPosition = new Vector3(.5f, .5f, 0) * size;
        var marker = root.AddComponent<OpeningDungeonGate>();
        marker.barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.barrier.name = "Closed entrance bar";
        marker.barrier.transform.SetParent(root.transform, false);
        marker.barrier.transform.localPosition = new Vector3(0, -.6f, -.65f) * size;
        marker.barrier.transform.localScale = new Vector3(1.35f, .16f, .18f) * size;
        Destroy(marker.barrier.GetComponent<Collider>());
        var renderer = marker.barrier.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = EnvironmentKit.Load().Paving;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var tint = new MaterialPropertyBlock();
        var color = new Color(.9f, .55f, .15f);
        tint.SetColor("_Color", color); tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);

        marker.label = new GameObject("Opening status").AddComponent<TextMeshPro>();
        marker.label.transform.SetParent(root.transform, false);
        marker.label.transform.localPosition = new Vector3(0, -.8f, -.55f) * size;
        marker.label.rectTransform.sizeDelta = new Vector2(3.5f, .55f) * size;
        marker.label.fontSize = 2.1f * size;
        marker.label.alignment = TextAlignmentOptions.Center;
        marker.label.gameObject.AddComponent<BiomeSignLabel>().CameraOffset = .05f * size;
        // Replace the fallback portal caption instead of stacking two labels.
        foreach (var caption in entrance.GetComponentsInChildren<TextMeshPro>())
            if (caption != marker.label && caption.text == "Dungeon gate") caption.gameObject.SetActive(false);
        marker.Refresh();
    }

    private void Update() => Refresh();
    private void Refresh()
    {
        if (barrier == null) return;
        IsClosed = Common.InstanceOrNull?.CampaignContext?.IsPreparingForOpening == true;
        barrier.SetActive(IsClosed);
        label.text = IsClosed ? "Opens tomorrow" : "Dungeon open";
        label.color = IsClosed ? new Color(1f, .75f, .3f) : new Color(.5f, 1f, .75f);
    }
}
