using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class BiomeDecorationFog : MonoBehaviour
{
    public Vector3 FloorPosition;
    public bool Preview;
    private void Update()
    {
        var effect=GetComponent<BiomeDecorationEffect>();
        if(effect!=null)effect.FloorVisible=Preview || (FogOverlay.Instance!=null && FogOverlay.Instance.IsCurrentlyVisible(FloorPosition,FootPrint.Size1x1));
    }
}
