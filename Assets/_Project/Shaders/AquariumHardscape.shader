Shader "Lumaquarium/Aquarium Hardscape"
{
    Properties
    {
        _BaseMap("Surface albedo",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Tiling("Tiles per meter",Float)=3
        _UVMode("Follow branch UV",Float)=0
        _Roughness("Roughness",Range(0,1))=.7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AquariumDepth.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float _Tiling, _UVMode, _Roughness;
        CBUFFER_END
        float _AquariumLightingActive, _AquariumNight;
        half4 _AquariumAmbientColor;
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; };
        Varyings Vert(Attributes i)
        {
            Varyings o;
            o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(i.normalOS); o.uv=i.uv; o.color=i.color;
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
            half4 Frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normal);
                float3 weights=pow(abs(n),4); weights/=max(dot(weights,1),.001);
                float3 p=i.world*_Tiling;
                half3 tex;
                if(_UVMode>.5) tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*float2(1,_Tiling)).rgb;
                else tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*weights.x
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
                // Screen derivatives give the photographed grains restrained micro-relief.
                float height=dot(tex,float3(.2126,.7152,.0722));
                float3 dpdx=ddx(i.world), dpdy=ddy(i.world);
                float3 r1=cross(dpdy,n),r2=cross(n,dpdx);
                float determinant=dot(dpdx,r1);
                float3 gradient=sign(determinant)*(ddx(height)*r1+ddy(height)*r2);
                n=normalize(abs(determinant)*n-gradient*.008);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float diffuse=saturate(dot(n,light.direction));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float spec=pow(saturate(dot(n,normalize(light.direction+view))),lerp(100,22,_Roughness));
                float a=sin(i.world.x*21+sin(i.world.z*17+_Time.y*.6)*2);
                float b=cos(i.world.z*23-_Time.y*.5+sin(i.world.x*14));
                // Caustics fall from the surface, so they show brightest on the sand and the tops of stones.
                float caustic=pow(saturate(1-abs(a+b)*.7),12)*lerp(.1,.34,saturate(n.y))*(1-_AquariumNight*.85);
                half3 ambient=lerp(half3(.29,.36,.29),_AquariumAmbientColor.rgb,_AquariumLightingActive);
                half3 color=tex*_BaseColor.rgb*i.color.rgb*(ambient+light.color*(.15+diffuse*.8)*light.shadowAttenuation+caustic);
                color+=light.color*spec*(1-_Roughness)*.22*light.shadowAttenuation;
                return half4(AquariumDepthFog(color,i.world),1);
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
            half4 ShadowFrag(Varyings i):SV_Target { return 0; }
            ENDHLSL
        }
    }
}
