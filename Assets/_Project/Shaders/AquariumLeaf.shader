Shader "Lumaquarium/Aquarium Leaf"
{
    Properties
    {
        [MainTexture] _BaseMap("Leaf albedo and cutout", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Cutout", Range(0,1)) = .45
        _Sway("Current movement", Range(0,.04)) = .008
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AquariumDepth.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float _AquariumLightingActive;
            half4 _AquariumAmbientColor;
            CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        float _Cutoff;
        float _Sway;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; };
        float3 LeafPosition(float3 position, float2 uv)
        {
            float3 world=TransformObjectToWorld(position);
            float phase=world.x*7+world.z*4;
            float flex=uv.y*uv.y;
            world.x+=sin(_Time.y*.92+phase)*_Sway*flex;
            world.z+=sin(_Time.y*.63+phase+1.7)*_Sway*.65*flex;
            return world;
        }
        Varyings Vert(Attributes i)
        {
            Varyings o;
            o.world=LeafPosition(i.positionOS.xyz,i.uv);
            o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(i.normalOS);
            o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.color=i.color;
            return o;
        }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                clip(tex.a-_Cutoff);
                float3 normal=normalize(i.normal)*(front?1:-1);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float diffuse=saturate(dot(normal,light.direction));
                float transmission=pow(saturate(dot(view,-light.direction)),3)*.2;
                float gloss=pow(saturate(dot(normal,normalize(light.direction+view))),64)*.16;
                half3 pigment=tex.rgb*_BaseColor.rgb*i.color.rgb;
                half3 lighting=lerp(half3(.32,.4,.26),_AquariumAmbientColor.rgb,_AquariumLightingActive)+light.color*(diffuse*.85*light.shadowAttenuation+transmission);
                float fresnel=pow(1-saturate(dot(normal,view)),4);
                return half4(AquariumDepthFog(pigment*lighting+light.color*gloss+pigment*fresnel*.12,i.world),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            float3 _LightDirection;
            Varyings ShadowVert(Attributes i)
            {
                Varyings o=Vert(i);
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.world,normalize(o.normal),_LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #else
                    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #endif
                return o;
            }
            half4 ShadowFrag(Varyings i):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-_Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
