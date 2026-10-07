using UnityEngine;

// One measured anchor for both outdoor cameras (no context multiplier was needed).
public static class DioramaScale
{
    public const float HeroHeight=1.2060131f;
    public const float Fence=.6f*HeroHeight, Barrel=.55f*HeroHeight, Sign=1.1f*HeroHeight;
    public const float Bench=.5f*HeroHeight, Lamp=1.8f*HeroHeight, Door=1.6f*HeroHeight, Eaves=2.2f*HeroHeight;
    public const float Broadleaf=2.85f*HeroHeight, Pine=3.1f*HeroHeight, Pickup=.45f*HeroHeight;
    public static float ToHeight(float visibleHeight,float target)=>target/Mathf.Max(.001f,visibleHeight);
}
