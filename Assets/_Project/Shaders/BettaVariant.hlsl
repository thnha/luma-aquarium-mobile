#ifndef LUMAQUARIUM_BETTA_VARIANT_INCLUDED
#define LUMAQUARIUM_BETTA_VARIANT_INCLUDED

// Variant colouring for Betta Lit. Include after LitInput.hlsl and before the pass file:
// it wraps InitializeStandardLitSurfaceData so the stock URP pass picks up the variant albedo.
// Per-fish values come from BettaAppearance (MaterialPropertyBlock). They live outside
// UnityPerMaterial, so this shader is not SRP Batcher compatible; fine for a tank of fish.
//
// _VariantMask (baked by Editor/BettaMaskBaker.cs, or painted by an artist with the same layout):
//   R = body (1) / fin (0)
//   G = fin root (0) -> fin edge (1)
//   B, A = two independent spot noises; the seed blends them so every seed gets new spots.
// Pattern indices match BettaPattern.Kind.

TEXTURE2D(_VariantMask);    SAMPLER(sampler_VariantMask);
TEXTURE2D(_ScaleMap);       SAMPLER(sampler_ScaleMap);
half _VariantOn;
half4 _VariantPrimary;
half4 _VariantSecondary;
half4 _VariantAccent;
half _VariantPattern;
half _VariantSeed;
half _VariantIridescence;
half _VariantSilver;
half _VariantSpot;
half _VariantAccentSpot;
half _VariantBand;
half _VariantBandSoftness;
half _VariantDetail;
half _VariantDetailGain;

half3 BettaVariantAlbedo(half3 original, float2 uv)
{
    half4 m = SAMPLE_TEXTURE2D(_VariantMask, sampler_VariantMask, uv);
    half body = m.r;
    half edge = m.g;
    half s, c;
    sincos(_VariantSeed, s, c);
    half n = saturate(0.5h + (m.b - 0.5h) * c + (m.a - 0.5h) * s);
    half spot = smoothstep(_VariantSpot - 0.03h, _VariantSpot + 0.03h, n);
    half accentSpot = 1.0h - smoothstep(_VariantAccentSpot - 0.03h, _VariantAccentSpot + 0.03h, n);

    half3 P = _VariantPrimary.rgb;
    half3 S = _VariantSecondary.rgb;
    half3 A = _VariantAccent.rgb;
    // Body relief now comes from the ordered atlas. Leave only a trace of the
    // photographed skin's shading; fins keep their painted ray contrast.
    half lum = dot(original, half3(0.2126h, 0.7152h, 0.0722h));
    half shade = lerp(1.0h, saturate(lum * _VariantDetailGain), _VariantDetail * lerp(1.0h, 0.08h, body));

    half3 bodyColor = P;
    half3 finColor = lerp(P, lerp(P, S, 0.6h), edge);                                  // Solid
    if (_VariantPattern > 0.5h && _VariantPattern < 1.5h)                               // Bicolor
        finColor = lerp(lerp(P, S, 0.65h), S, saturate(edge * 3.0h));
    else if (_VariantPattern > 1.5h && _VariantPattern < 2.5h)                          // Butterfly
        finColor = lerp(P, S, smoothstep(_VariantBand - _VariantBandSoftness, _VariantBand + _VariantBandSoftness, edge));
    else if (_VariantPattern > 2.5h && _VariantPattern < 4.5h)                          // Marble, Koi
    {
        half3 spots = lerp(lerp(S, P, spot), A, accentSpot);
        bodyColor = spots;
        finColor = lerp(spots, S, _VariantPattern > 3.5h ? 0.35h * edge : 0.2h);
    }
    else if (_VariantPattern > 4.5h)                                                    // Dragon
    {
        bodyColor = lerp(P, A, smoothstep(0.35h, 0.8h, lum) * 0.8h);
        finColor = lerp(P, S, edge);
    }
    return lerp(original, lerp(finColor, bodyColor, body) * shade, _VariantOn);
}

inline void BettaVariantSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    InitializeStandardLitSurfaceData(uv, outSurfaceData);
    outSurfaceData.albedo = BettaVariantAlbedo(outSurfaceData.albedo, uv);
    // Silver: the painted metallic/smoothness map gives body and fins a silvery shine; 0 turns it into
    // a satin, non-metal finish (milky white instead of silver).
    half silver = lerp(1.0h, _VariantSilver, _VariantOn);
    outSurfaceData.metallic *= silver * 0.3h;
    outSurfaceData.smoothness = lerp(0.40h, clamp(outSurfaceData.smoothness, 0.36h, 0.56h), silver);
    // Iridescence: a metallic, glossy body tints its reflections with the fish colour.
    half body = SAMPLE_TEXTURE2D(_VariantMask, sampler_VariantMask, uv).r;
    half sheen = _VariantIridescence * _VariantOn;
    outSurfaceData.metallic = lerp(outSurfaceData.metallic, max(outSurfaceData.metallic, 0.25h * body), sheen);
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, max(outSurfaceData.smoothness, 0.58h), sheen);
    // The atlas is linear data: keep normal XY out of albedo, and suppress the
    // old wrinkled normal/AO only on opaque skin. Eyes and fin rays retain theirs.
    half4 scales = SAMPLE_TEXTURE2D(_ScaleMap, sampler_ScaleMap, uv);
    half skin = scales.a * body * _VariantOn;
    half2 xy = (scales.gb * 2.0h - 1.0h) * 0.8h;
    half3 scaleNormal = half3(xy, sqrt(saturate(1.0h - dot(xy, xy))));
    outSurfaceData.normalTS = normalize(lerp(outSurfaceData.normalTS, scaleNormal, skin));
    outSurfaceData.albedo *= lerp(1.0h, lerp(0.79h, 1.02h, scales.r), skin);
    outSurfaceData.occlusion = lerp(outSurfaceData.occlusion, 1.0h, skin);
    outSurfaceData.metallic = lerp(outSurfaceData.metallic, silver * 0.1h, skin);
    half scaleSmoothness = 0.40h + silver * 0.08h + sheen * 0.06h + (scales.r - 0.5h) * 0.02h;
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, scaleSmoothness, skin);
}

// The stock pass files call InitializeStandardLitSurfaceData; route them through the wrapper.
#define InitializeStandardLitSurfaceData BettaVariantSurfaceData

#endif
