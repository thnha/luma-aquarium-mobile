#ifndef LUMAQUARIUM_BETTA_VARIANT_INCLUDED
#define LUMAQUARIUM_BETTA_VARIANT_INCLUDED

// Variant colouring for Betta Lit. Include after LitInput.hlsl and before the pass file:
// it wraps InitializeStandardLitSurfaceData so the stock URP pass picks up the variant albedo.
// Per-fish values come from BettaAppearance (MaterialPropertyBlock). They live outside
// UnityPerMaterial, so this shader is not SRP Batcher compatible; fine for a tank of fish.
//
// _VariantMask (baked by Editor/BettaMaskBaker.cs, or painted by an artist with the same layout):
//   R = body (1) / fin (0)
//   G = fin root (0) -> fin edge (1); on the body, eye (1) for pipeline fish
//   (their material sets a transparent _ScaleMap: the ordered atlas follows the shared Betta's UVs)
//   B, A = two independent spot noises; the seed blends them so every seed gets new spots.
// Pattern indices match BettaPattern.Kind.
// Galaxy (6): primary base, secondary dark patches, accent fine speckles (procedural, in UV space),
// _VariantEdge on the fin edges.

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
half4 _VariantEdge;
float4 _VariantSpeckle;     // x = speckles per UV unit, y = density (0..1)

// Dots scattered in UV space: x = dot, y = its bright core. Each cell holds at most one dot placed
// anywhere in it, and the 3x3 neighbourhood is searched so dots overlap cell borders: no visible grid.
// Dots cut at UV island borders are too small to show a seam; fwidth keeps edges one pixel soft.
half2 BettaGalaxySpeckles(float2 uv, float scale, half density, half seed)
{
    float2 p = uv * scale + seed * 17.0;
    float2 cell = floor(p);
    float aa = max(length(fwidth(p)) * 0.5, 1e-4);   // pixel size in cell units, outside the branchy loop
    half dotMask = 0.0h, core = 0.0h;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float2 id = cell + float2(x, y);
        float3 h = frac(sin(float3(dot(id, float2(127.1, 311.7)), dot(id, float2(269.5, 183.3)), dot(id, float2(419.2, 371.9)))) * 43758.5453);
        if (h.z > density) continue;
        half radius = lerp(0.12h, 0.5h, h.x * h.y);          // mostly small, a few large
        float d = length(p - id - h.xy);
        dotMask = max(dotMask, 1.0h - smoothstep(radius - aa, radius + aa, d));
        core = max(core, 1.0h - smoothstep(radius * 0.4h - aa, radius * 0.4h + aa, d));
    }
    return half2(dotMask, core);
}

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
    else if (_VariantPattern > 4.5h && _VariantPattern < 5.5h)                          // Dragon
    {
        bodyColor = lerp(P, A, smoothstep(0.35h, 0.8h, lum) * 0.8h);
        finColor = lerp(P, S, edge);
    }
    else if (_VariantPattern > 5.5h)                                                    // Galaxy
    {
        half3 base = lerp(P, S, spot);
        // Speckles gather in clusters (second noise), thicker on fins, thinner in the dark patches.
        half cluster = smoothstep(0.35h, 0.65h, saturate(0.5h + (m.a - 0.5h) * c - (m.b - 0.5h) * s));
        half density = _VariantSpeckle.y * lerp(0.0h, 2.0h, cluster) * lerp(1.4h, 1.0h, body) * lerp(1.0h, 0.4h, spot);
        // Fine speckles plus a sparser layer of larger flecks, like the blotchy blue of a Galaxy Plakat.
        half2 speck = max(BettaGalaxySpeckles(uv, _VariantSpeckle.x, saturate(density), _VariantSeed),
                          BettaGalaxySpeckles(uv, _VariantSpeckle.x * 0.35, saturate(density * 0.45h), _VariantSeed + 3.1h));
        half3 sparkle = lerp(A, half3(0.9h, 0.95h, 1.0h), speck.y * 0.8h);
        bodyColor = lerp(base, sparkle, speck.x);
        finColor = lerp(bodyColor, _VariantEdge.rgb,
            smoothstep(_VariantBand - _VariantBandSoftness, _VariantBand + _VariantBandSoftness, edge));
    }
    // G on body texels marks the eye in one-piece pipeline fish (betta-splendens/pipeline): keep its
    // own colour. The shared Betta's mask has G = 0 on the body (its eyes are a separate renderer).
    half eye = saturate(edge * body);
    return lerp(original, lerp(lerp(finColor, bodyColor, body) * shade, original, eye), _VariantOn);
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
