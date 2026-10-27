Shader "Lumaquarium/Aquarium Coral"
{
    Properties
    {
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Roughness("Roughness",Range(0,1))=.62
        _Detail("Micro relief",Range(0,1))=.3
        _Transmission("Soft tissue light",Range(0,1))=.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AquariumDepth.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float _Roughness, _Detail, _Transmission;
        CBUFFER_END
        float _AquariumLightingActive, _AquariumNight, _AquariumLamp;
        half4 _AquariumAmbientColor;
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; half4 color:COLOR; };
        Varyings Vert(Attributes i)
        {
            Varyings o;
            o.world=TransformObjectToWorld(i.positionOS.xyz);o.local=i.positionOS.xyz;
            o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;
            return o;
        }
        float Grain(float3 p)
        {
            return sin(p.x*347+sin(p.z*293))*sin(p.y*389-p.z*263)*.5
                +sin(p.x*733+p.z*587)*sin(p.y*641)*.2;
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
            half4 Frag(Varyings i, FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                float3 n=normalize(i.normal)*IS_FRONT_VFACE(facing,1,-1);
                float grain=Grain(i.local);
                float3 dx=ddx(i.world),dy=ddy(i.world),r1=cross(dy,n),r2=cross(n,dx);
                float determinant=dot(dx,r1);
                n=normalize(abs(determinant)*n-sign(determinant)*(ddx(grain)*r1+ddy(grain)*r2)*_Detail*.0007);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float diffuse=saturate((dot(n,light.direction)+.18)/1.18);
                float roughness=lerp(.88,_Roughness,i.color.a);
                float spec=pow(saturate(dot(n,normalize(light.direction+view))),lerp(85,18,roughness));
                half3 ambient=lerp(half3(.29,.36,.34),_AquariumAmbientColor.rgb,_AquariumLightingActive);
                half3 pigment=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                pigment=SRGBToLinear(pigment);
                #endif
                half3 albedo=pigment*_BaseColor.rgb*(1+grain*.11);
                float shadow=light.shadowAttenuation;
                half3 color=albedo*(ambient+light.color*(.12+.86*diffuse)*shadow);
                float tissue=pow(saturate(dot(-n,light.direction)),2)*_Transmission*i.color.a;
                color+=albedo*light.color*tissue*shadow;
                color+=light.color*spec*(1-roughness)*.23*shadow;
                float a=sin(i.world.x*21+sin(i.world.z*17+_Time.y*.6)*2);
                float b=cos(i.world.z*23-_Time.y*.5+sin(i.world.x*14));
                float caustic=pow(saturate(1-abs(a+b)*.7),12)*lerp(.025,.13,saturate(n.y))*max(1-_AquariumNight*.85,_AquariumLamp*.9);
                color+=albedo*light.color*caustic*shadow;
                color+=AquariumBackLight(n,view,i.world,0)*(albedo*.7+.3);
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
            half4 ShadowFrag(Varyings i):SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i):SV_Target{return 0;}
            ENDHLSL
        }
    }
}
