#ifndef ETERNAL_ENIGMA_BOUNDARY_CUTAWAY
#define ETERNAL_ENIGMA_BOUNDARY_CUTAWAY
sampler2D _FloorMask, _ForegroundWallMask;
float4 _FloorRect;
float _GroundZ, _CellSize, _CutawayHeight, _WholeCell, _ForegroundWallHeight;
float4 _DungeonWallView;
float3 _DungeonWallViewDirection;

float DungeonForegroundWallAmount(float3 world)
{
    float2 uv = (world.xy - _FloorRect.xy) / _FloorRect.zw;
    float inside = step(0, uv.x) * step(0, uv.y) * (1 - step(1, uv.x)) * (1 - step(1, uv.y));
    // A separate bilinear field keeps bevels, caps and neighboring wall pieces
    // continuous. Looking up the floor separately at each vertex tears joins.
    float wall = saturate(tex2Dlod(_ForegroundWallMask, float4(uv, 0, 0)).r * 2) * inside;
    float2 cellUV = (floor((world.xy - _FloorRect.xy) / _CellSize) + .5) * _CellSize / _FloorRect.zw;
    wall *= step(.5, tex2Dlod(_ForegroundWallMask, float4(cellUV, 0, 0)).g);
    float2 relative = (world.xy - _DungeonWallView.xy) / max(.001, _CellSize);
    // Only walls between the camera and its floor focus obstruct the character.
    // A wall beyond the character stays full height even with floor on both sides.
    float foreground = 1 - smoothstep(-.5, 0, dot(relative, _DungeonWallView.zw));
    return wall * foreground * saturate(_DungeonWallView.w) * (1 - smoothstep(1.5, 3.5, length(relative)));
}

float3 DungeonWallVertex(float3 world)
{
    if (_WholeCell < .5 && _CutawayHeight > .001)
    {
        float amount = DungeonForegroundWallAmount(world);
        float scale = lerp(1, saturate(_ForegroundWallHeight / _CutawayHeight), amount);
        world.z = _GroundZ - max(0, _GroundZ - world.z) * scale;
    }
    return world;
}

void DungeonWallClip(float3 world)
{
    float3 samplePoint = world;
    if (_WholeCell > .5)
    {
        samplePoint.xy = (floor((samplePoint.xy - _FloorRect.xy) / _CellSize) + .5) * _CellSize + _FloorRect.xy;
        samplePoint.z = _GroundZ - _CutawayHeight;
        // Whole decorations cannot remain suspended above a lowered supporting wall.
        clip(.02 - DungeonForegroundWallAmount(samplePoint));
    }
    float3 direction = _DungeonWallViewDirection;
    if (samplePoint.z < _GroundZ - .05 && direction.z > .001)
    {
        float2 end = samplePoint.xy + direction.xy * ((_GroundZ - samplePoint.z) / direction.z);
        float covered = 0;
        [unroll] for (int i = 1; i <= 12; i++)
        {
            float2 uv = (lerp(samplePoint.xy, end, i / 12.0) - _FloorRect.xy) / _FloorRect.zw;
            float inside = step(0, uv.x) * step(0, uv.y) * (1 - step(1, uv.x)) * (1 - step(1, uv.y));
            covered = max(covered, tex2D(_FloorMask, uv).r * inside);
        }
        clip(.5 - covered);
    }
}
#endif
