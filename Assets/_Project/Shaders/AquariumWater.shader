Shader "Lumaquarium/Aquarium Water"
{
    Properties
    {
        _Strength("Wave strength", Range(0,2)) = 1
        _NightEnvironmentTex("Night environment", 2D) = "gray" {}
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
            TEXTURE2D(_NightEnvironmentTex); SAMPLER(sampler_NightEnvironmentTex);
            float _AquariumNight;
            float4 _AquariumWaterColor;
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            float4 _Ripples[4];
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            // Unequal directions and periods avoid a repeated grid on the surface.
            float3 Waves(float2 p, float time)
            {
                float4 phase=float4(dot(p,float2(6.1,3.7))+time*.3,
                    dot(p,float2(-8.4,9.2))-time*.35,
                    dot(p,float2(14.8,-5.3))+time*.44,
                    dot(p,float2(22.7,19.1))-time*.55);
                float4 amplitude=float4(.006,.003,.0015,.0006)*_Strength;
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
                reflected=lerp(reflected,SAMPLE_TEXTURE2D(_NightEnvironmentTex,sampler_NightEnvironmentTex,saturate(uv)).rgb,_AquariumNight);
                Light sun=GetMainLight();
                float sparkle=pow(saturate(dot(reflect(-normalize(sun.direction),normal),view)),110);
                // A few faint bands of light that drift slowly with the waves, in the sun's or the lamp's
                // colour, dimmer at night.
                float a=sin(p.x*7+sin(p.y*6+_Time.y*.2)*2+slope.x*20);
                float b=cos(p.y*8-_Time.y*.16+sin(p.x*5)+slope.y*20);
                float shimmer=pow(saturate(1-abs(a+b)*.8),16)*(1-_AquariumNight*.7);
                if(_WorldSpaceCameraPos.y<i.world.y)
                {
                    // Seen from under the water the surface is never a window onto what lies above the tank.
                    // It takes the deep-water colour behind the tank, so the water seems to run on up to the
                    // top of the screen, with only a soft, slow swell and the rings of a tap moving across it.
                    half swell=dot(slope,float2(.9,1.3))+impulse*.25;
                    return half4(_AquariumWaterColor.rgb*(1+swell)+sun.color*shimmer*.04,1);
                }
                half3 body=lerp(half3(.2,.55,.6),half3(.03,.09,.17),_AquariumNight);
                half3 color=lerp(body,reflected,fresnel*.7+.3)+sun.color*(sparkle*.5+shimmer*.12);
                float fade=1-smoothstep(.2,2.6,p.y);
                float alpha=(.07+fresnel*.26+sparkle*.18+shimmer*.05+impulse*.035)*fade*_Strength;
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
