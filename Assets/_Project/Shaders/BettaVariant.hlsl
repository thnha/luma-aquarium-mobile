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
// Galaxy (6): primary red fins, body in alternating primary / _VariantEdge patches, secondary dark patches on the back,
// accent blue-white head, streaks along the fin rays and body flecks; _VariantEdge rims the lower fins.
// Koi Galaxy (7): primary pearl body, accent red-orange patches (body, head, lower fins), secondary dark
// metallic scales and fin rims, _VariantEdge pale fin colour with white streaks; red pelvic fins.
// Head, streaks, height and pelvic fins come from the pipeline's _PatternMask (black when the fish has none).

TEXTURE2D(_VariantMask);    SAMPLER(sampler_VariantMask);
TEXTURE2D(_ScaleMap);       SAMPLER(sampler_ScaleMap);
TEXTURE2D(_PatternMask);
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
    else if (_VariantPattern > 5.5h && _VariantPattern < 6.5h)                          // Galaxy
    {
        // _PatternMask (pipeline fish): R head, G streak noise along the fin rays, B height (belly 0, back 1).
        half3 g = SAMPLE_TEXTURE2D(_PatternMask, sampler_VariantMask, uv).rgb;
        // Red (primary) and yellow (edge colour) patches alternate on the body: large patches from the
        // second noise, broken up by smaller blobs. Fins stay primary.
        half n2 = saturate(0.5h + (m.a - 0.5h) * c - (m.b - 0.5h) * s);
        half blobs = BettaGalaxySpeckles(uv, _VariantSpeckle.x * 0.12, 0.45h, _VariantSeed + 7.3h).x;
        half yellow = max(smoothstep(0.46h, 0.54h, n2), blobs * 0.9h) * body;
        half3 base = lerp(P, _VariantEdge.rgb, yellow);
        // Dark patches gather on the back.
        half dark = smoothstep(_VariantSpot - 0.06h, _VariantSpot + 0.06h, n + (g.b - 0.5h) * 0.6h) * lerp(0.3h, 1.0h, body);
        base = lerp(base, S, dark);
        // Blue and white marbled head.
        half3 headColor = lerp(A, half3(0.92h, 0.95h, 1.0h), smoothstep(0.45h, 0.6h, m.a));
        base = lerp(base, headColor, smoothstep(0.3h, 0.6h, g.r));
        // Streaks along the fin rays, denser towards the fin edge and on the upper fins, with bright cores.
        half amount = saturate(_VariantSpeckle.y * lerp(0.6h, 1.1h, g.b) * smoothstep(0.05h, 0.45h, edge));
        half cut = 1.0h - amount;
        half streak = smoothstep(cut - 0.04h, cut + 0.04h, g.g) * (1.0h - body);
        half3 streakColor = lerp(A, half3(0.9h, 0.95h, 1.0h), smoothstep(cut + 0.14h, cut + 0.32h, g.g));
        // Sparse blue flecks on the body, in clusters.
        half cluster = smoothstep(0.35h, 0.65h, n2);
        half2 speck = BettaGalaxySpeckles(uv, _VariantSpeckle.x, saturate(_VariantSpeckle.y * cluster * (1.0h - dark)), _VariantSeed);
        half fleck = speck.x * body * (1.0h - smoothstep(0.3h, 0.6h, g.r));
        half3 sparkle = lerp(A, half3(0.9h, 0.95h, 1.0h), speck.y * 0.8h);
        bodyColor = lerp(base, sparkle, fleck);
        // The edge colour rims the lower fins (anal, pelvic); upper fins keep their red edge.
        half rim = smoothstep(_VariantBand - _VariantBandSoftness, _VariantBand + _VariantBandSoftness, edge) * (1.0h - g.b * 0.8h);
        finColor = lerp(lerp(base, streakColor, streak), _VariantEdge.rgb, rim);
    }
    else if (_VariantPattern > 6.5h)                                                    // Koi Galaxy
    {
        half4 g = SAMPLE_TEXTURE2D(_PatternMask, sampler_VariantMask, uv);
        half3 F = _VariantEdge.rgb;
        half head = smoothstep(0.3h, 0.6h, g.r);
        half n2 = saturate(0.5h + (m.a - 0.5h) * c - (m.b - 0.5h) * s);
        // Pearl body with red to orange patches; the head is mostly pearl with orange cheeks.
        half3 patch = lerp(A, half3(1.0h, 0.62h, 0.12h), smoothstep(0.45h, 0.62h, n) * 0.7h);
        half3 skin = lerp(P, patch, smoothstep(0.5h, 0.58h, n2));
        skin = lerp(skin, lerp(patch, P, smoothstep(0.35h, 0.5h, m.a)), head);
        // Dark metallic scales packed on the back and towards the tail: a solid dark patch with blue
        // glinting scales inside, single dark scales scattered around its border.
        half zone = smoothstep(_VariantSpot - 0.1h, _VariantSpot + 0.1h, n + (g.b - 0.5h) * 0.6h) * (1.0h - head * 0.7h);
        half solid = smoothstep(0.35h, 0.75h, zone);
        half2 scales = BettaGalaxySpeckles(uv, _VariantSpeckle.x * 0.5, saturate(zone * 2.5h), _VariantSeed + 1.7h);
        half3 scaleColor = lerp(S, lerp(S, F, 0.55h), solid);
        // Pale blue glitter on the light skin.
        half2 glitter = BettaGalaxySpeckles(uv, _VariantSpeckle.x, saturate(_VariantSpeckle.y * (1.0h - zone)), _VariantSeed);
        skin = lerp(lerp(skin, F, glitter.x * 0.7h), S, solid);
        bodyColor = lerp(skin, scaleColor, scales.x);
        // Pale blue fins with white streaks along the rays and a few orange ones; dark blue at the
        // roots on the back, big red patches with a yellow base on the lower fins; dark rim.
        half3 fin = lerp(F, half3(0.95h, 0.97h, 1.0h), smoothstep(0.45h, 0.7h, g.g) * 0.8h);
        fin = lerp(fin, patch, smoothstep(0.7h, 0.8h, g.g) * smoothstep(0.5h, 0.6h, n2) * 0.8h);
        half root = 1.0h - smoothstep(0.1h, 0.35h, edge);
        fin = lerp(fin, lerp(S, F, 0.35h), root * smoothstep(0.5h, 0.7h, g.b));
        half lower = 1.0h - smoothstep(0.3h, 0.5h, g.b);
        fin = lerp(fin, half3(1.0h, 0.78h, 0.2h), root * lower * 0.8h);
        fin = lerp(fin, A, smoothstep(0.42h, 0.49h, n) * lower * (1.0h - edge * 0.4h));
        half rim = smoothstep(_VariantBand - _VariantBandSoftness, _VariantBand + _VariantBandSoftness, edge + (g.g - 0.5h) * 0.1h);
        fin = lerp(fin, S, rim);
        // Pelvic fins: red with pale streaks.
        finColor = lerp(fin, lerp(A, P, smoothstep(0.6h, 0.8h, g.g) * 0.6h), g.a);
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
