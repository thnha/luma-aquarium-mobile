Shader "Lumaquarium/Aquarium Water"
{
    Properties
    {
        _Strength("Wave strength", Range(0,2)) = 1
        _EnvironmentTex("Aquarium reflection", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_EnvironmentTex); SAMPLER(sampler_EnvironmentTex);
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            float4 _Ripples[4];
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            // Unequal directions and periods avoid a repeated grid on the surface.
            float3 Waves(float2 p, float time)
            {
                float4 phase=float4(dot(p,float2(6.1,3.7))+time*.78,
                    dot(p,float2(-8.4,9.2))-time*.91,
                    dot(p,float2(14.8,-5.3))+time*1.14,
                    dot(p,float2(22.7,19.1))-time*1.43);
                float4 amplitude=float4(.008,.004,.0025,.0012)*_Strength;
                float4 derivative=cos(phase)*amplitude;
                return float3(dot(sin(phase),amplitude),dot(derivative,float4(6.1,-8.4,14.8,22.7)),
                    dot(derivative,float4(3.7,9.2,-5.3,19.1)));
            }
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.world=TransformObjectToWorld(i.positionOS.xyz);
                o.world.y+=Waves(o.world.xz,_Time.y).x;
                o.positionCS=TransformWorldToHClip(o.world);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;
                float2 slope=Waves(p,_Time.y).yz;
                float impulse=0;
                for(int n=0;n<4;n++)
                {
                    float age=_Time.y-_Ripples[n].w;
                    float2 offset=p-_Ripples[n].xy;
                    float d=length(offset);
                    float envelope=exp(-abs(d-age*.32)*18)*saturate(1-age/3)*step(0,age);
                    float ring=cos(d*48-age*7.5)*envelope*_Ripples[n].z;
                    slope+=offset/max(d,.01)*ring*.12;
                    impulse+=abs(ring);
                }
                float3 normal=normalize(float3(-slope.x,1,-slope.y));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=.0204+.9796*pow(1-abs(dot(normal,view)),5);
                float2 uv=float2(.5+p.x*.16+slope.x*.16,.88+p.y*.025+slope.y*.11);
                half3 reflected=SAMPLE_TEXTURE2D(_EnvironmentTex,sampler_EnvironmentTex,saturate(uv)).rgb;
                Light sun=GetMainLight();
                float sparkle=pow(saturate(dot(reflect(-normalize(sun.direction),normal),view)),110);
                half3 color=lerp(half3(.06,.3,.31),reflected,fresnel*.7+.3)+sun.color*sparkle*.5;
                float fade=1-smoothstep(.2,2.6,p.y);
                float alpha=(.045+fresnel*.26+sparkle*.18+impulse*.035)*fade*_Strength;
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
