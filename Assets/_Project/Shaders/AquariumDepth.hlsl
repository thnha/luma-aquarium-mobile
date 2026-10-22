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

// A lamp behind the tank: a rim on the edges turned away from the camera, stronger toward the back,
// and light shining through thin leaves (transmission). AquariumLighting sets the colour, already
// scaled by its strength; unset it is black and adds nothing.
float4 _AquariumBackLightColor;
// Set by AquariumTank: half width, front z, back z, floor y.
float4 _AquariumTankBox;

half3 AquariumBackLight(float3 normal, float3 view, float3 world, float transmission)
{
    float3 toLight = normalize(float3(0, .35, 1));
    // The panel glows brightest along the sand (Aquarium Glow), so only low leaves have it right behind them.
    float lamp = pow(saturate(1 - (world.y - _AquariumTankBox.w) / 2), 1.6);
    // A thin bright edge, not a wash over the whole face: that read as haze.
    float rim = pow(1 - saturate(dot(normal, view)), 6) * saturate(dot(normal, toLight) * .7 + .3) * (.35 + .65 * lamp) * 1.6;
    float through = pow(saturate(dot(view, -toLight)), 4) * transmission * lamp;
    float depth = .45 + .55 * saturate((world.z - _AquariumFogNear) / max(.001, _AquariumFogFar - _AquariumFogNear));
    return _AquariumBackLightColor.rgb * (rim + through) * depth;
}
#endif
