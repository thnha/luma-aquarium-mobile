Shader "Lumaquarium/Aquarium Nature"
{
    Properties
    {
        _BaseColor("Color", Color) = (.4,.5,.3,1)
        _Detail("Surface variation", Range(0,1)) = .3
        _Scale("Grain scale", Float) = 45
        // Set per renderer by AquariumDecorItem on plant foliage; stone and wood stay rigid.
        [Toggle] _WallPush("Fold against the glass", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "AquariumDepth.hlsl"
            float _AquariumLightingActive;
            half4 _AquariumAmbientColor;
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Detail;
            float _Scale;
            float _WallPush;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                if (_WallPush > .5) o.world = AquariumWallPush(o.world);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }
            half4 Frag(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                float3 p = i.world;
                float grain = hash(floor(p * _Scale));
                float marbling = sin(p.x * 49 + sin(p.y * 35) * 2 + p.z * 25) * .5 + .5;
                float3 base = _BaseColor.rgb * lerp(1 - _Detail, 1 + _Detail, grain * .55 + marbling * .45) * i.color.rgb;
                float3 normal = normalize(i.normal) * (front ? 1 : -1);
                Light light = GetMainLight(TransformWorldToShadowCoord(p));
                float lambert = saturate(dot(normal, light.direction));
                float ca = sin(p.x * 24 + sin(p.z * 19 + _Time.y * .65) * 2 + p.y * 7);
                float cb = cos(p.z * 25 - _Time.y * .5 + sin(p.x * 14));
                float caustic = pow(saturate(1 - abs(ca + cb) * .7), 10) * .3;
                float3 lighting = lerp(float3(.23,.38,.33),_AquariumAmbientColor.rgb,_AquariumLightingActive) + light.color * (.25 + lambert * .65) * light.shadowAttenuation;
                float3 result = base * (lighting + caustic * float3(.9,1,.68));
                // Tinted by the surface, as in Aquarium Leaf, so the backlight does not haze it.
                result += AquariumBackLight(normal, normalize(_WorldSpaceCameraPos - p), p, .3) * (base * .85 + .06);
                // Gentle distance absorption keeps the foreground legible under water.
                float fog = saturate((distance(p, _WorldSpaceCameraPos) - 3) * .05);
                return half4(AquariumDepthFog(lerp(result, float3(.1,.36,.4), fog), p),1);
            }
            ENDHLSL
        }
    }
}
