// Copy of URP 17 Lit whose ForwardLit pass flips the normal on back faces,
// so single-plane fins light correctly from both sides with Render Face = Both.
// ForwardLit and GBuffer also apply per-fish variant colours (BettaVariant.hlsl, set by BettaAppearance).
Shader "Lumaquarium/Betta Lit"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

        // Variant colouring (per fish via MaterialPropertyBlock, see BettaAppearance)
        [NoScaleOffset] _VariantMask("Variant Mask", 2D) = "black" {}
        [NoScaleOffset] _ScaleMap("Ordered Body Scales", 2D) = "gray" {}
        [NoScaleOffset] _PatternMask("Pattern Mask (Galaxy: R head, G fin streaks, B height, A pelvic)", 2D) = "black" {}
        // Scale cells: RG = scale centre UV, B = 1 inside a scale (pipeline/scale_edges.py); black = off
        [NoScaleOffset] _ScaleCellMap("Scale Cells", 2D) = "black" {}
        _VariantOn("Variant On", Float) = 0.0
        _VariantPrimary("Variant Primary", Color) = (1,1,1,1)
        _VariantSecondary("Variant Secondary", Color) = (1,1,1,1)
        _VariantAccent("Variant Accent", Color) = (1,1,1,1)
        _VariantPattern("Variant Pattern", Float) = 0.0
        _VariantSeed("Variant Seed", Float) = 0.0
        _VariantIridescence("Variant Iridescence", Range(0.0, 1.0)) = 0.0
        _VariantSilver("Variant Silver Sheen", Range(0.0, 1.0)) = 1.0
        _VariantSpot("Variant Spot Threshold", Range(0.0, 1.0)) = 0.58
        _VariantAccentSpot("Variant Accent Spot Threshold", Range(0.0, 1.0)) = 0.3
        _VariantBand("Variant Butterfly Band", Range(0.0, 1.0)) = 0.55
        _VariantBandSoftness("Variant Butterfly Band Softness", Range(0.01, 0.5)) = 0.04
        _VariantDetail("Variant Texture Detail", Range(0.0, 1.0)) = 0.6
        _VariantDetailGain("Variant Texture Detail Gain", Float) = 2.5
        _VariantEdge("Variant Fin Edge (Galaxy)", Color) = (1,0.82,0.2,1)
        _VariantSpeckle("Variant Speckles (Galaxy: per UV, density)", Vector) = (220,0.35,0,0)
        // Pipeline fish: the occlusion map is fish_bake.py's detail map (R cavity between scales, G AO)
        _Cavity("Cavity Strength", Range(0.0, 1.0)) = 0.7
        _AlbedoAO("AO on Direct Light", Range(0.0, 1.0)) = 0.5
        // Detail map B = random value per scale: small normal tilt and gloss change per scale
        _ScaleTilt("Scale Tilt", Range(0.0, 0.6)) = 0.08
        _IridescenceShift("Iridescence Hue Shift", Range(0.0, 4.0)) = 1.6
        // Scales every smoothness: below 1 turns glazed highlights into satin skin
        _Gloss("Gloss", Range(0.0, 1.5)) = 1.0
        // Koi Galaxy: dark scales and fin rims turn deep black; polish 0.25 satin -> 1 lacquer (0 = off)
        _BlackGloss("Black Gloss", Range(0.0, 1.0)) = 0.0
        // Koi Galaxy: share of the dark scales that are white instead (0 = none)
        _WhiteScales("White Scales", Range(0.0, 0.5)) = 0.0
        // Koi Galaxy: irregular red patches on the forehead and top of the head (0 = none)
        _HeadPatch("Head Patch", Range(0.0, 1.0)) = 0.0
        // Koi Galaxy black lacquer detail: per-scale polish, dull seams, steel-blue sheen (0 = flat coat)
        _BlackDetail("Black Detail", Range(0.0, 1.0)) = 1.0
        // Strength of the steel-blue sheen on black scales (1 = faint, 2 = clear steel blue)
        _BlackSteel("Black Steel Sheen", Range(0.0, 4.0)) = 2.0
        // Snout and tail tip in object space (set per fish), and pigment depth: z deeper head / paler tail, w paler fin edges
        _VariantHead("Variant Head", Vector) = (0,0,0,0)
        _VariantTail("Variant Tail", Vector) = (0,0,0,0)
        _VariantDepth("Variant Pigment Depth", Vector) = (0,0,0,0)
        // Clear fin edges: x start, z clearest (fin root 0 -> edge 1), y alpha there (1 = off)
        _FinEdgeAlpha("Fin Edge Alpha", Vector) = (0.6,1,1,0)
        // Caudal fin rim: x start, y clearest, z where it begins along the fish (head 0 -> tail tip 1)
        _FinEdgeCaudal("Fin Edge Caudal", Vector) = (0.6,0.9,2,0)
        // 1 on the fish's second, transparent material that draws only the clear fin edges
        [HideInInspector] _FinEdgePass("Fin Edge Pass", Float) = 0.0
        // Fins are thin membranes: light from behind shines through, more towards the edge
        _FinTranslucency("Fin Translucency", Range(0.0, 2.0)) = 0.8
        // Depth of the grooves between fin rays (1 = as baked, lower = flatter, thinner-looking membrane)
        _FinRelief("Fin Relief", Range(0.0, 1.0)) = 1.0
        // Height of the body scales (1 = as authored, lower = thinner scales lying flat on the skin)
        _ScaleRelief("Scale Relief", Range(0.0, 1.0)) = 1.0
        // Seams between body scales in their own colour: amount (0 = off), width in from the seam,
        // rim colour on dark scales, and a factor on the light scales' own colour
        _ScaleEdge("Scale Edge", Range(0.0, 1.0)) = 0.0
        _ScaleEdgeWidth("Scale Edge Width", Range(0.0, 1.0)) = 0.4
        _ScaleEdgeDark("Scale Edge On Dark", Color) = (0.35,0.3,0.25,1)
        _ScaleEdgeLight("Scale Edge On Light", Color) = (0.55,0.55,0.6,1)
        // Fins: dark parts deeper towards black (0 = as the body's black), brightness of the other fin colours (1 = as painted)
        _FinBlack("Fin Black", Range(0.0, 1.0)) = 0.0
        _FinBrightness("Fin Brightness", Range(0.5, 2.0)) = 1.0
        // Lit colour of the whole fish: saturation and brightness (1 = as lit). Underwater light makes it read dull.
        _Vibrance("Vibrance", Range(0.5, 2.0)) = 1.0
        _Brightness("Brightness", Range(0.5, 2.0)) = 1.0
        // Reflection of the surroundings (sky probe): 1 = full, 0 = none. It greys dark colours and washes out reds.
        _EnvReflection("Environment Reflection", Range(0.0, 1.0)) = 1.0
        // Keep the base map's own colours instead of the pattern (pipeline fish with build.baseTexture)
        _BaseTexture("Base Texture", Range(0.0, 1.0)) = 0.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertex
            #pragma fragment BettaPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"


            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "BettaVariant.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            // Light through a fin from its far side: the main light (strongest looking into it) plus
            // ambient from behind, tinted by the fin colour and stronger where the membrane thins out.
            half3 BettaFinTransmission(float3 positionWS, half3 normalWS)
            {
                // black pigment (dark fin rims) lets no light through
                half fin = gBettaFin * _FinTranslucency * (1.0h - saturate(gBettaDark));
                if (fin <= 0.001h)
                    return half3(0.0h, 0.0h, 0.0h);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half behind = saturate(-dot(normalWS, light.direction));
                half into = saturate(dot(viewWS, -light.direction));
                into *= into; into *= into;
                half3 direct = light.color * (light.distanceAttenuation * light.shadowAttenuation * behind * (0.35h + 0.65h * into));
                half3 ambient = SampleSH(-normalWS) * 0.6h;
                half thin = lerp(0.35h, 1.0h, gBettaEdge) * lerp(0.25h, 1.0h, gBettaFolds);
                thin *= 1.0h + 1.5h * gBettaClear;   // thin pigment is clearer
                // light filtered by the membrane comes out deeper in colour than the lit surface
                half3 tint = gBettaAlbedo * sqrt(gBettaAlbedo) * 1.3h;
                return tint * (direct + ambient) * (thin * fin);
            }

            void BettaPassFragment(
                Varyings input
                , FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                if (!IS_FRONT_VFACE(face, true, false))
                {
                    input.normalWS = -input.normalWS;
                #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                    input.tangentWS = -input.tangentWS; // xyz and sign, keeps the normal map un-mirrored
                #endif
                }
            #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                // view and tangent frame for the scale sheen in BettaVariant.hlsl
                half3 sheenN = normalize(input.normalWS);
                half3 sheenB = input.tangentWS.w * cross(sheenN, input.tangentWS.xyz);
                gBettaTangentToWorld = half3x3(input.tangentWS.xyz, sheenB, sheenN);
                gBettaViewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                gBettaHasView = 1.0h;
            #endif
                gBettaPositionOS = TransformWorldToObject(input.positionWS);
                gBettaHasPos = 1.0h;
                half edgeAlpha = BettaFinEdgeClip(input.uv);
                // LitPassFragment (URP LitForwardPass.hlsl), inlined so the surface is at hand for the
                // environment reflection below.
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
            #endif
                InitializeBakedGIData(input, inputData);
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                if (_EnvReflection < 0.999h)
                {
                    // Take back part of the sky reflection UniversalFragmentPBR added (same terms as its
                    // GlobalIllumination): on a Meshy colour fish it lays a grey film over blacks and reds.
                    BRDFData brdfData;
                    InitializeBRDFData(surfaceData, brdfData);
                    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
                    half fresnelTerm = Pow4(1.0h - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)));
                    half3 env = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, brdfData.perceptualRoughness,
                        surfaceData.occlusion, inputData.normalizedScreenSpaceUV) * EnvironmentBRDFSpecular(brdfData, fresnelTerm);
                    color.rgb = max(color.rgb - env * (1.0h - _EnvReflection), 0.0h);
                }
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
                outColor = color;
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
                outColor.rgb += BettaFinTransmission(input.positionWS, normalize(input.normalWS));
                half luma = dot(outColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                outColor.rgb = max(lerp(luma.xxx, outColor.rgb, _Vibrance), 0.0h) * _Brightness;
                if (_FinEdgePass > 0.5h)
                    outColor.a = edgeAlpha;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex BettaShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // The fish's shadow is drawn this far behind it (away from the light), so the pleated fins and
            // neighbouring folds stop shadowing each other: at mobile shadow texels (~1 cm) those small hard
            // shadows flicker as the fins wave. Shadows on the sand and from decor onto the fish are kept.
            #define BETTA_SHADOW_PUSH_BACK 0.05

            Varyings BettaShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                #if defined(_ALPHATEST_ON)
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                positionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS) - lightDirectionWS * BETTA_SHADOW_PUSH_BACK;
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(positionWS));
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitGBufferPassVertex
            #pragma fragment LitGBufferPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "BettaVariant.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutputFormat.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment BettaDepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #include "BettaVariant.hlsl"

            // Clear fin edges write no depth (the uv is only there with alpha test on).
            half BettaDepthOnlyFragment(Varyings input) : SV_TARGET
            {
            #if defined(_ALPHATEST_ON)
                BettaFinEdgeClip(input.uv);
            #endif
                return DepthOnlyFragment(input);
            }
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment BettaDepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #include "BettaVariant.hlsl"

            // Back faces of the fins write the flipped normal too, so SSAO sees the side facing the camera.
            void BettaDepthNormalsFragment(
                Varyings input
                , FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                if (!IS_FRONT_VFACE(face, true, false))
                {
                    input.normalWS = -input.normalWS;
                #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                    input.tangentWS = -input.tangentWS;
                #endif
                }
            #if defined(_ALPHATEST_ON) || defined(_PARALLAXMAP) || defined(_NORMALMAP) || defined(_DETAIL)
                BettaFinEdgeClip(input.uv);   // clear fin edges stay out of SSAO
            #endif
                DepthNormalsFragment(input, outNormalWS
            #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
            #endif
                );
            }
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "XRMotionVectors"
            Tags { "LightMode" = "XRMotionVectors" }
            ColorMask RGBA

            // Stencil write for obj motion pixels
            Stencil
            {
                WriteMask 1
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #define APPLICATION_SPACE_WARP_MOTION 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
