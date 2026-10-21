#ifndef LUMAQUARIUM_AQUARIUM_DEPTH
#define LUMAQUARIUM_AQUARIUM_DEPTH
// Water tints whatever stands further back toward the camera's background colour, so the back of
// the tank fades into the water instead of ending at a hard edge. AquariumLighting sets these;
// with no lighting component the amount stays 0 and nothing changes.
float4 _AquariumWaterColor;
float _AquariumFogAmount, _AquariumFogNear, _AquariumFogFar;

half3 AquariumDepthFog(half3 color, float3 world)
{
    // Up to the amount across the placement area, then on to the full colour over the same distance
    // again, so the sand behind the tank melts into the background.
    float t = max(0, (world.z - _AquariumFogNear) / max(.001, _AquariumFogFar - _AquariumFogNear));
    float tint = t <= 1 ? t * _AquariumFogAmount : lerp(_AquariumFogAmount, 1, saturate(t - 1));
    return lerp(color, _AquariumWaterColor.rgb, tint * step(.001, _AquariumFogAmount));
}
#endif
