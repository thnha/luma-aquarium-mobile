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
// Scale cells (pipeline/scale_edges.py): RG = the scale's centre UV, B = .02 + .98 x its random value (0 outside),
// uncompressed. Koi Galaxy picks each scale's colour at its centre, so a scale is one colour and patches step
// along the rims. Black = off (scale ids then come from the detail map's B).
TEXTURE2D(_ScaleCellMap);
float4 _ScaleCellMap_TexelSize;
// Scale ids must not be filtered: a blend of two ids is a random third value (speckles at scale borders).
SAMPLER(sampler_betta_point_clamp);
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
half _Cavity;
half _AlbedoAO;
half _ScaleTilt;
half _IridescenceShift;
half _Gloss;
// Black lacquer: dark scales and dark fin rims turn deep black and non-metal (fully from 0.25 up);
// the value sets their polish, 0.25 satin -> 1 lacquer. 0 = off.
half _BlackGloss;
// Koi Galaxy: share of the dark scales that are white instead, scattered one by one (0 = none).
half _WhiteScales;
// Black lacquer detail (0 = one flat coat, 1 = per-scale polish, dull seams, extra tilt, steel-blue
// grazing sheen on some scales, glossier back than belly).
half _BlackDetail;
// Strength of that steel-blue sheen (1 = faint, 2 = clear steel blue; default 2).
half _BlackSteel;
// Depth of the grooves between fin rays (normal, cavity, AO): 1 = as baked, lower = a flatter, thinner-looking membrane.
half _FinRelief;
// Height of the body scales (normal, shading, seams, tilt): 1 = as authored, lower = thinner scales lying flat on the skin.
half _ScaleRelief;
// Scale edges: the seams between body scales in their own colour, apart from the scale relief.
// _ScaleEdge = amount (0 = off), _ScaleEdgeWidth = how far in from the seam (0..1), rim colour on dark
// scales (_ScaleEdgeDark, the colour itself) and on light scales (_ScaleEdgeLight, times the scale colour).
half _ScaleEdge;
half _ScaleEdgeWidth;
half4 _ScaleEdgeDark;
half4 _ScaleEdgeLight;
// Fins on their own: how much deeper the dark fin parts (rims, dark roots) go towards black (0 = as the
// body's black), and a brightness factor for the rest of the fin colour (1 = as painted).
half _FinBlack;
half _FinBrightness;
// Lit colour of the whole fish (forward pass only): saturation and brightness, 1 = as lit.
half _Vibrance;
half _Brightness;
// Snout and tail tip in object space (a skinned fish's object space follows its root bone), set per fish
// by BettaAppearance: the head (0) -> tail (1) coordinate for pigment depth and the caudal rim.
float4 _VariantHead;
float4 _VariantTail;
// Pigment depth: z = how much deeper the head is (and paler the tail), w = how much paler fin edges are. 0: off.
float4 _VariantDepth;
// Clear fin edges: x = where on the fin (0 root -> 1 edge) the membrane starts to clear, z = where it is
// clearest (the caudal mask peaks near 0.9), y = alpha there (1 = off). Set per fish (property block).
half4 _FinEdgeAlpha;
// Caudal fin rim: x start, y clearest, z where along the fish (0 head -> 1 tail tip) the caudal fin begins.
half4 _FinEdgeCaudal;
// Set on the material: 0 = the fish's opaque material (cuts the clear part out), 1 = its second,
// transparent material (draws only the clear part, blended).
half _FinEdgePass;
// Set by BettaPassFragment (forward pass) before the surface is built: the view direction and the
// tangent frame, so the scale sheen can follow the normal-mapped surface. Zero in other passes.
static half gBettaHasView = 0.0h;
static half3 gBettaViewWS = half3(0.0h, 0.0h, 1.0h);
static half3x3 gBettaTangentToWorld = half3x3(1.0h, 0.0h, 0.0h, 0.0h, 1.0h, 0.0h, 0.0h, 0.0h, 1.0h);
// Left by the surface for the forward pass's fin transmission (BettaLit.shader).
half _FinTranslucency;
static half3 gBettaAlbedo = half3(0.0h, 0.0h, 0.0h);
static half gBettaFin = 0.0h;
static half gBettaEdge = 0.0h;
static half gBettaFolds = 1.0h;   // cavity x AO: fin rays and folds pass less light
static half gBettaScaleGlint = 0.0h;   // Koi Galaxy: per-scale metallic glint, kept out of _Gloss
static half gBettaDark = 0.0h;   // Koi Galaxy: dark scales and fin rims (0..1), for _BlackGloss
static half2 gBettaDarkRand = half2(0.5h, 0.5h);   // their scale's randoms: polish, steel sheen pick
static half gBettaHeight = 1.0h;   // Koi Galaxy: belly 0 -> back 1 (_PatternMask B)
static half gBettaCrease = 1.0h;   // detail cavity: seam between scales 0 -> scale face 1
static half gBettaSteel = 0.0h;    // steel-blue grazing sheen on black scales
static half gBettaScaleEdge = 0.0h;   // body scale seams (0 face -> 1 seam), for _ScaleEdge
static half gBettaHead = 0.0h;   // Koi Galaxy: the whole head (0..1); no scale rims there
static half gBettaFace = 1.0h;   // body scales: across the scale, 0 at its free edge -> 1 at its base
static half gBettaFaceOn = 0.0h;   // where gBettaFace applies (body scales, not the head)
// Set by BettaPassFragment: the fragment in object space, for the pigment depth along the body.
static half gBettaHasPos = 0.0h;
static half gBettaClear = 0.0h;   // thin pigment (0..1): fins pass more light, see BettaFinTransmission
static float3 gBettaPositionOS = float3(0.0, 0.0, 0.0);

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

bool BettaHasAxis()
{
    float3 d = _VariantTail.xyz - _VariantHead.xyz;
    return gBettaHasPos > 0.5h && dot(d, d) > 1e-8;
}

// Head (0) -> tail tip (1) along the fish; 0 where the position or the axis is unknown.
half BettaBodyT()
{
    if (!BettaHasAxis())
        return 0.0h;
    float3 d = _VariantTail.xyz - _VariantHead.xyz;
    return (half)saturate(dot(gBettaPositionOS - _VariantHead.xyz, d) / dot(d, d));
}

half BettaFinEdgeAlpha(float2 uv)
{
    if (_FinEdgeAlpha.y >= 0.999h)
        return 1.0h;
    half2 m = SAMPLE_TEXTURE2D(_VariantMask, sampler_VariantMask, uv).rg;
    // The caudal fin (behind _FinEdgeCaudal.z along the fish) gets its own, wider rim: its mask peaks
    // lower than the other fins'. Passes without the position (depth) use the other fins' rim.
    half caudal = smoothstep(_FinEdgeCaudal.z - 0.05h, _FinEdgeCaudal.z + 0.05h, BettaBodyT());
    half2 band = lerp(_FinEdgeAlpha.xz, _FinEdgeCaudal.xy, caudal);
    half clear = smoothstep(band.x, band.y, m.g) * (1.0h - m.r);
    // Koi Galaxy's dark fin rim is dense black pigment, not clear membrane: it stays opaque
    // (same rim as in BettaVariantAlbedo, so the opaque and clear parts meet where the colour turns).
    if (_VariantPattern > 6.5h)
    {
        half4 g = SAMPLE_TEXTURE2D(_PatternMask, sampler_VariantMask, uv);
        half rim = smoothstep(_VariantBand - _VariantBandSoftness, _VariantBand + _VariantBandSoftness, m.g + (g.g - 0.5h) * 0.1h);
        clear *= 1.0h - rim * (1.0h - g.a) * _VariantOn;
    }
    return lerp(1.0h, _FinEdgeAlpha.y, clear);
}

// Each material keeps its own part of the fin, so the two meet without a gap or overlap.
half BettaFinEdgeClip(float2 uv)
{
    half alpha = BettaFinEdgeAlpha(uv);
    clip(_FinEdgePass > 0.5h ? 0.999h - alpha : alpha - 0.999h);
    return alpha;
}

// The scale's id (0..1): from the scale cell map, else the detail map's B; 0.5 without either.
float BettaScaleId(float2 uv)
{
    float cell = SAMPLE_TEXTURE2D_LOD(_ScaleCellMap, sampler_betta_point_clamp, uv, 0).b;
    if (cell > 0.01)
        return (cell - 0.02) / 0.98;
#if defined(_OCCLUSIONMAP)
    return SAMPLE_TEXTURE2D_LOD(_OcclusionMap, sampler_betta_point_clamp, uv, 0).b;
#else
    return 0.5;
#endif
}

// Three random values per scale from its id; 0.5 without one.
half3 BettaScaleRandom(float2 uv)
{
#if defined(_OCCLUSIONMAP)
    float id = BettaScaleId(uv);
    return (half3)frac(id * float3(12.9898, 78.233, 37.719) * 43.7585);
#else
    return half3(0.5h, 0.5h, 0.5h);
#endif
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
        // Body colour decisions are read at the scale's centre (fins keep their own texel). The four nearest
        // cell-map texels are blended like a bilinear filter, so the colour turns over one texel where two
        // scales meet instead of in the cell map's texel staircase (jagged at close range).
        half4 mc = 0.0h;
        half4 gc = 0.0h;
        float2 st = uv * _ScaleCellMap_TexelSize.zw - 0.5;
        float2 i0 = floor(st);
        float2 f = st - i0;
        [unroll] for (int k = 0; k < 4; k++)
        {
            float2 o = float2(k & 1, k >> 1);
            float2 tuv = (i0 + o + 0.5) * _ScaleCellMap_TexelSize.xy;
            half4 cell = SAMPLE_TEXTURE2D_LOD(_ScaleCellMap, sampler_betta_point_clamp, tuv, 0);
            float2 cuv = (cell.b > 0.01h && body > 0.5h) ? (float2)cell.rg : uv;
            half w = (half)(lerp(1.0 - f.x, f.x, o.x) * lerp(1.0 - f.y, f.y, o.y));
            mc += w * SAMPLE_TEXTURE2D_LOD(_VariantMask, sampler_VariantMask, cuv, 0);
            gc += w * SAMPLE_TEXTURE2D_LOD(_PatternMask, sampler_VariantMask, cuv, 0);
        }
        // Patches follow the smooth noise more than the scales: decided purely per scale, their borders
        // traced the irregular cells of the Meshy scales and broke into shards. Real koi blotches are large
        // with soft edges; the scale part still steps them a little along the scales.
        mc = lerp(mc, m, 0.6h);
        gc = lerp(gc, g, 0.6h);
        half nc = saturate(0.5h + (mc.b - 0.5h) * c + (mc.a - 0.5h) * s);
        half head = smoothstep(0.3h, 0.6h, gc.r);
        // The whole head, from the texel's own mask: R steps between its bands inside the head, so the
        // 0.3-0.6 ramp above left half-head spots behind the eye and on the gill cover, where dark scales
        // broke through as black shards. Above 0.2 is all head; the band below it fades into the body.
        half headAll = smoothstep(0.05h, 0.2h, g.r) * body;
        gBettaHead = headAll;
        half n2 = saturate(0.5h + (mc.a - 0.5h) * c - (mc.b - 0.5h) * s);
        // Every scale makes its own colour decision at its centre, so patch borders step from scale to scale
        // like a real koi mosaic. The per-scale threshold jitter stays small: a larger one flipped single
        // scales far from any patch border, which read as scattered shards instead of solid patches.
        half3 r = BettaScaleRandom(uv);
        half3 jr = (r - 0.5h) * body;
        half nj = nc + jr.x * 0.04h;
        half n2j = n2 + jr.y * 0.03h;   // smaller: red and yellow scales mix less at patch borders
        // Pearl skin is never plain white: each scale leans pink, lavender or pale blue.
        half3 tint = r.z < 0.33h ? half3(1.0h, 0.8h, 0.88h) : (r.z < 0.66h ? half3(0.86h, 0.82h, 1.0h) : half3(0.8h, 0.92h, 1.0h));
        half3 pearl = P * lerp(half3(1.0h, 1.0h, 1.0h), tint, 0.15h * body);
        // Red patches run red -> orange, with a fringe of yellow-orange scales where they meet the pearl.
        half3 patch = lerp(A, half3(1.0h, 0.5h, 0.1h), saturate(smoothstep(0.42h, 0.65h, nc) * 0.8h + jr.z * 0.1h));
        half3 fringe = lerp(pearl, half3(1.0h, 0.76h, 0.22h), 0.8h);
        half3 skin = lerp(pearl, fringe, smoothstep(0.46h, 0.5h, n2j));
        skin = lerp(skin, patch, smoothstep(0.5h, 0.56h, n2j));
        // White gill covers; red below them (the membrane under the gills) along the lower head.
        half underGill = 1.0h - smoothstep(0.25h, 0.38h, g.b);   // texel's own: a smooth edge, not scale shards
        // A koi's head is smooth white with a pink blush, not a mosaic: one even tint, no per-scale variety.
        half3 headPearl = P * lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h, 0.86h, 0.88h), 0.7h * body);
        skin = lerp(skin, lerp(headPearl, A, underGill * head), headAll);
        // Dark metallic scales gathered in solid clusters on the back and towards the tail, their borders
        // stepping scale by scale; none on the head. Near black, a few scales glint cool silver (in linear
        // colour even a small share reads bright); _VariantSpeckle.y scales the glint (0 = plain dark).
        half zone = smoothstep(_VariantSpot - 0.1h, _VariantSpot + 0.1h, nj + (gc.b - 0.5h) * 0.6h) * (1.0h - headAll);
        half darkScale = smoothstep(0.4h, 0.6h, zone + jr.y * 0.03h) * body;
        half r8 = r.x * r.x; r8 *= r8; r8 *= r8;
        half glint = saturate(_VariantSpeckle.y * 2.5h) * 0.3h * r8;
        half3 scaleColor = lerp(S, half3(0.5h, 0.56h, 0.66h), glint * (1.0h - saturate(_BlackGloss * 4.0h)));   // silver scales read grey on black
        // A few single pearl scales scattered through the dark clusters (a scale's own random picks it).
        half white = step(frac(r.x * 3.7h + r.z * 5.3h), _WhiteScales) * darkScale;
        bodyColor = lerp(lerp(skin, scaleColor, darkScale), pearl, white);
        darkScale -= white;
        // Each scale a little lighter or darker than its neighbours (subtle: more made a patchwork).
        // (Dark scales vary more: in one flat black the scales disappear.)
        bodyColor *= 1.0h + jr.z * lerp(0.08h, 0.35h, darkScale) * (1.0h - headAll);
        gBettaScaleGlint = darkScale * glint + (1.0h - darkScale) * body * 0.25h * r.x;
        // Fins: a few orange streaks, dark blue at the roots on the back, big bright red patches with a
        // yellow base on the lower fins, dark rim. Fin colour strands along the rays on a pale membrane: single strands in some places, in others
        // many strands merging into a solid patch (a third blend of the spot noises picks where).
        half n3 = saturate(0.5h + (m.b - 0.5h) * s + (m.a - 0.5h) * c);
        half strandCut = lerp(0.68h, 0.22h, smoothstep(0.4h, 0.65h, n3));
        half strands = smoothstep(strandCut - 0.04h, strandCut + 0.04h, g.g);
        half3 fin = lerp(lerp(F, half3(0.95h, 0.97h, 1.0h), 0.65h), F, strands);
        fin = lerp(fin, patch, smoothstep(0.7h, 0.8h, g.g) * smoothstep(0.5h, 0.6h, n2) * 0.8h);
        half root = 1.0h - smoothstep(0.1h, 0.35h, edge);
        fin = lerp(fin, lerp(S, F, 0.35h), root * smoothstep(0.5h, 0.7h, g.b));
        half lower = 1.0h - smoothstep(0.3h, 0.5h, g.b);
        fin = lerp(fin, half3(1.0h, 0.78h, 0.2h), root * lower * 0.8h);
        // Solid red, not blended into the pale membrane (half-blended red reads pink).
        half redPatch = smoothstep(0.42h, 0.47h, n) * smoothstep(0.35h, 0.6h, lower);
        fin = lerp(fin, A, redPatch);
        half rim = smoothstep(_VariantBand - _VariantBandSoftness, _VariantBand + _VariantBandSoftness, edge + (g.g - 0.5h) * 0.1h);
        fin = lerp(fin, S, rim);
        // Pelvic fins: red with pale streaks.
        finColor = lerp(fin, lerp(A, P, smoothstep(0.6h, 0.8h, g.g) * 0.6h), g.a);
        gBettaDark = lerp(rim * (1.0h - g.a), darkScale, body);
        gBettaDarkRand = r.yz;
        gBettaHeight = g.b;
    }
    // G on body texels marks the eye in one-piece pipeline fish (betta-splendens/pipeline): keep its
    // own colour. The shared Betta's mask has G = 0 on the body (its eyes are a separate renderer).
    half eye = saturate(edge * body);
    half3 color = lerp(finColor, bodyColor, body);
    // Pigment is thickest on the head and gills and thins out towards the tail; fin edges are new growth
    // with the least pigment. Thick pigment is darker and deeper (pow keeps white white); thin pigment
    // stays the same hue but turns clear: brighter, and its fins let more light through (gBettaClear).
    if (BettaHasAxis())
    {
        half t = BettaBodyT();
        half depth = _VariantDepth.z * (1.0h - 2.0h * t) - _VariantDepth.w * edge * (1.0h - body);
        color = pow(max(color, 1e-3h), exp2(max(depth, 0.0h)));
        half clear = saturate(-depth);
        color = min(color * (1.0h + 0.45h * clear), 1.0h);
        gBettaClear = clear * _VariantOn;
    }
    return lerp(original, lerp(color * shade, original, eye), _VariantOn);
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
    half2 xy = (scales.gb * 2.0h - 1.0h) * 0.8h * _ScaleRelief;
    half3 scaleNormal = half3(xy, sqrt(saturate(1.0h - dot(xy, xy))));
    outSurfaceData.normalTS = normalize(lerp(outSurfaceData.normalTS, scaleNormal, skin));
    outSurfaceData.albedo *= lerp(1.0h, lerp(0.79h, 1.02h, scales.r), skin * lerp(0.35h, 1.0h, _ScaleRelief));
    outSurfaceData.occlusion = lerp(outSurfaceData.occlusion, 1.0h, skin);
    outSurfaceData.metallic = lerp(outSurfaceData.metallic, silver * 0.1h, skin);
    half scaleSmoothness = 0.40h + silver * 0.08h + sheen * 0.06h + (scales.r - 0.5h) * 0.02h;
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, scaleSmoothness, skin);
    // Fin relief: shallower ray grooves read as a thin membrane instead of a pleated slab.
    half finFlat = (1.0h - body) * (1.0h - _FinRelief) * _VariantOn;
    // Thin scales: pipeline fish bake their scales into the normal map and cavity (their _ScaleMap is
    // empty), so those flatten too; some relief stays so the scales still read. Below 0.25 it flattens
    // further, down to a faint relief at 0 (values from 0.25 up are unchanged).
    half scaleFlat = body * ((1.0h - _ScaleRelief) * 0.7h + 0.22h * saturate(1.0h - _ScaleRelief * 4.0h)) * _VariantOn;
    outSurfaceData.normalTS = normalize(lerp(outSurfaceData.normalTS, half3(0.0h, 0.0h, 1.0h), finFlat + scaleFlat));
    outSurfaceData.occlusion = lerp(outSurfaceData.occlusion, 1.0h, finFlat + scaleFlat);
#if defined(_OCCLUSIONMAP)
    // Pipeline fish (fish_bake.py): R = cavity darkens and roughens the seams between scales and the
    // grooves between fin rays, so paint no longer hides them. URP applies AO (G) to ambient light only;
    // part of it also goes on albedo so folds and fin roots stay shaded under the key light.
    half4 detail = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv);
    half cavity = lerp(1.0h, saturate((detail.r - 0.3h) / 0.55h), _Cavity * (1.0h - finFlat) * (1.0h - scaleFlat));
    outSurfaceData.albedo *= cavity * lerp(1.0h, detail.g, _AlbedoAO * (1.0h - finFlat) * (1.0h - scaleFlat));
    outSurfaceData.smoothness *= lerp(0.6h, 1.0h, cavity);
    half rays = saturate((detail.r - 0.3h) / 0.55h);
    gBettaFolds = rays * rays * detail.g;
    // thin scales: shallower seams also even out the black polish and steel sheen between scales
    gBettaCrease = lerp(rays, 1.0h, scaleFlat);
    half blackOn = gBettaDark * saturate(_BlackGloss * 4.0h) * _BlackDetail * _VariantOn;
    // Body scales a little glossier than the fin membranes.
    half2 bodyEye = SAMPLE_TEXTURE2D(_VariantMask, sampler_VariantMask, uv).rg;
    half eyeMask = saturate(bodyEye.r * bodyEye.g);
    half onScales = bodyEye.r * (1.0h - eyeMask) * _VariantOn;
    // A = distance to the scale's free edge (pipeline/scale_edges.py: the bottom line of the cavity seam arcs, measured
    // walking tail-wards so the rim sits on the overlapping scale only; 0 on the seam, 1 at 16 texels
    // or more). The per-scale colour cells are cut from the same arcs, so rims and colour changes line
    // up; 1 everywhere on maps without it. Independent of the relief, so thin scales keep coloured edges.
    // Light scales get a wider rim than dark ones: a thin pink line on white broke up into dashes.
    half edgeWidth = max(_ScaleEdgeWidth, 0.01h) * lerp(1.6h, 1.0h, saturate(gBettaDark));
    gBettaScaleEdge = (1.0h - smoothstep(0.0h, edgeWidth, detail.a)) * onScales * (1.0h - gBettaHead);
    // The same distance shades each scale: lighter towards its free edge, darker at its base where it comes
    // out from under the scale in front. On dark scales that is what tells one scale from the next.
    gBettaFace = detail.a;
    gBettaFaceOn = onScales * (1.0h - gBettaHead);
    outSurfaceData.smoothness *= lerp(0.8h, 1.0h, bodyEye.r);
#if defined(_NORMALMAP)
    // B = one random value per scale (fish_bake.scale_ids): every scale tilts and polishes a little
    // differently, so highlights jump from scale to scale as the fish turns instead of sliding.
    float2 rnd = frac(BettaScaleId(uv) * float2(12.9898, 78.233) * 43.7585) - 0.5;
    half3 n = outSurfaceData.normalTS;
    n.xy += (half2)rnd * _ScaleTilt * _ScaleRelief * onScales;
    // Black scales tilt more: on a near-mirror surface that is what makes each scale catch the light on its own.
    n.xy += (half2)rnd * 0.18h * lerp(0.4h, 1.0h, _ScaleRelief) * blackOn;
    // Koi Galaxy head: smooth skin, so most of the Meshy lumps on it go flat.
    n = lerp(n, half3(0.0h, 0.0h, 1.0h), 0.6h * gBettaHead);
    outSurfaceData.normalTS = normalize(n);
    outSurfaceData.smoothness = saturate(outSurfaceData.smoothness * (1.0h + (half)rnd.y * 0.12h * onScales));
    if (gBettaHasView > 0.5h)
    {
        // Thin-film sheen: towards grazing angles of the (normal-mapped) scale surface the colour turns
        // to a blue-violet film hue at the same brightness, so a scale's rim changes colour as it turns
        // while its face keeps the fish colour. Kept out of the crevices; some metallic makes the
        // reflections take the film colour too.
        half3 nWS = normalize(mul(outSurfaceData.normalTS, gBettaTangentToWorld));
        half film = 1.0h - saturate(dot(nWS, gBettaViewWS));
        float phase = film * _IridescenceShift + _VariantSeed * 0.1;
        half3 thin = 0.5h + 0.5h * cos(6.2831853 * (phase + float3(0.55, 0.75, 0.95)));
        thin = lerp(thin, half3(0.25h, 0.45h, 1.0h), 0.4h);   // betta sheen leans blue-violet
        half luma = dot(outSurfaceData.albedo, half3(0.299h, 0.587h, 0.114h));
        half3 filmColor = thin * (luma * 1.6h + 0.05h);
        half amount = saturate(_VariantIridescence * 2.0h) * _VariantOn * (1.0h - eyeMask) * lerp(0.3h, 1.0h, bodyEye.r)
            * cavity * smoothstep(0.15h, 0.85h, film) * (1.0h - saturate(_BlackGloss * 4.0h) * gBettaDark);
        outSurfaceData.albedo = lerp(outSurfaceData.albedo, filmColor, amount);
        outSurfaceData.metallic = max(outSurfaceData.metallic, amount * 0.4h);
        // Black bettas read steel blue away from the straight-on view: stronger on some scales, none in the seams.
        gBettaSteel = blackOn * smoothstep(0.1h, 0.75h, film) * lerp(0.3h, 1.0h, step(0.55h, gBettaDarkRand.y)) * gBettaCrease;
    }
#endif
#endif
    // Fins: a little deeper at the root, lighter towards the thin edge (G = root 0 -> edge 1).
    half2 finMask = SAMPLE_TEXTURE2D(_VariantMask, sampler_VariantMask, uv).rg;
    half fin = 1.0h - finMask.r;
    outSurfaceData.albedo *= lerp(1.0h, lerp(0.82h, 1.06h, finMask.g), fin * _VariantOn);
    half finDark = saturate(gBettaDark) * fin * _VariantOn;
    half3 finLit = saturate(outSurfaceData.albedo * lerp(1.0h, _FinBrightness, fin * _VariantOn));
    outSurfaceData.albedo = lerp(finLit, outSurfaceData.albedo * (1.0h - 0.9h * _FinBlack), finDark);
    gBettaAlbedo = outSurfaceData.albedo;
    gBettaFin = fin;
    gBettaEdge = finMask.g * fin;
    // Overall gloss: below 1 softens the wet, glazed highlights (a porcelain look) into a satin skin.
    outSurfaceData.smoothness *= _Gloss;
    // Glinting scales stay polished and metallic, so single scales flash instead of the whole body.
    half glint = gBettaScaleGlint * _VariantOn;
    outSurfaceData.metallic = lerp(outSurfaceData.metallic, 0.6h, glint);
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, 0.72h, glint);
    // Black lacquer: a rough black surface spreads its reflection into a grey haze and a black metal
    // reflects tinted grey, so the dark parts go deeper, non-metal and polished: black with small sharp highlights.
    half lacquer = gBettaDark * saturate(_BlackGloss * 4.0h) * _VariantOn;
    outSurfaceData.albedo *= 1.0h - 0.6h * lacquer;
    outSurfaceData.metallic = lerp(outSurfaceData.metallic, 0.0h, lacquer);
    // Not one flat coat: every scale its own polish, dull seams between scales, glossier on the back
    // than towards the belly.
    half polish = lerp(0.5h, 0.88h, saturate((_BlackGloss - 0.25h) / 0.75h));
    half vary = (0.8h + 0.4h * gBettaDarkRand.x) * lerp(0.45h, 1.0h, gBettaCrease)
        * lerp(0.7h, 1.0h, smoothstep(0.2h, 0.8h, gBettaHeight));
    polish *= lerp(1.0h, vary, _BlackDetail);
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, saturate(polish), lacquer);
    // The steel blue sits in the reflection too: a little metallic makes the highlight take the blue.
    half steel = gBettaSteel * _BlackSteel;
    outSurfaceData.albedo += half3(0.02h, 0.055h, 0.1h) * steel;
    outSurfaceData.metallic = max(outSurfaceData.metallic, saturate(steel) * 0.5h);
    // Scale edges last, so the black lacquer does not darken a light rim on a black scale.
    half dark = saturate(gBettaDark) * (1.0h - fin);
    // Dark scales: light and glossy towards the free edge, dark and matte at the base. The gloss matters
    // as much as the colour: on black, the reflection is most of what shows.
    half faceOn = dark * gBettaFaceOn;
    outSurfaceData.albedo *= lerp(1.0h, lerp(2.2h, 0.45h, gBettaFace), faceOn);
    // Light scales the same way, gently: on white a strong shade reads as grime.
    outSurfaceData.albedo *= lerp(1.0h, lerp(1.3h, 0.92h, gBettaFace), (1.0h - dark) * gBettaFaceOn);
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, outSurfaceData.smoothness * lerp(1.15h, 0.55h, gBettaFace), faceOn);
    half3 edgeColor = lerp(outSurfaceData.albedo * _ScaleEdgeLight.rgb, _ScaleEdgeDark.rgb, dark);
    half edgeOn = gBettaScaleEdge * _ScaleEdge;
    outSurfaceData.albedo = lerp(outSurfaceData.albedo, edgeColor, edgeOn);
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, min(outSurfaceData.smoothness, 0.6h), edgeOn * dark);
}

// The stock pass files call InitializeStandardLitSurfaceData; route them through the wrapper.
#define InitializeStandardLitSurfaceData BettaVariantSurfaceData

#endif
