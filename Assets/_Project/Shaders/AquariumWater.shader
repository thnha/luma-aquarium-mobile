Shader "Lumaquarium/Aquarium Water"
{
    Properties
    {
        _Strength("Wave strength", Range(0,2)) = 1
        _Clarity("Surface clarity from below", Range(0,1)) = 1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_EnvironmentTex); SAMPLER(sampler_EnvironmentTex);
            TEXTURE2D(_NightEnvironmentTex); SAMPLER(sampler_NightEnvironmentTex);
            float _AquariumNight;
            float4 _AquariumWaterColor;
            // Inside of the glass (AquariumTank): x half width, y front z, z back z, w floor y.
            float4 _AquariumTankBox;
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            float _Clarity;
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
                    // Seen from under the water at this shallow angle the surface is a mirror (total internal
                    // reflection): it shows the walls, sand and plants of the tank, bent by the waves. The
                    // reflected ray is traced to the inside of the glass and that point is read back from the
                    // opaque scene; where it leaves the screen the read folds back in from the edge, so the
                    // mirror carries on with the walls and sand next to it instead of a flat colour.
                    float3 under=normalize(float3(slope.x*4,-1,slope.y*4));
                    float3 r=reflect(-view,under);
                    // At grazing angles a steep wave can tip the ray upward; it meets the surface again and is
                    // sent back down, so it never shows what lies above the walls.
                    r.y=-max(abs(r.y),.02);
                    float3 hit=float3(
                        ((r.x>0?_AquariumTankBox.x:-_AquariumTankBox.x)-i.world.x)/(abs(r.x)>1e-4?r.x:1e-4),
                        (_AquariumTankBox.w-i.world.y)/min(r.y,-1e-4),
                        ((r.z>0?_AquariumTankBox.z:_AquariumTankBox.y)-i.world.z)/(abs(r.z)>1e-4?r.z:1e-4));
                    float t=max(0,min(hit.x,min(hit.y,hit.z)));
                    float4 clip=TransformWorldToHClip(i.world+r*t);
                    float2 suv=clip.xy/max(clip.w,1e-4)*float2(.5,.5*_ProjectionParams.x)+.5;
                    suv=clip.w>0?1-abs(1-abs(suv)):float2(suv.x,.02);
                    half3 water=_AquariumWaterColor.rgb;
                    half3 mirror=lerp(SampleSceneColor(saturate(suv)),water,saturate(t*.03));
                    half3 color=lerp(water,mirror,_Clarity);
                    // Light from above breaks through the moving surface as bright glints, strongest overhead.
                    half overhead=smoothstep(.15,.5,-view.y);
                    half glint=pow(saturate(1-abs(a+b)*.6),6);
                    half3 glare=sun.color*(overhead*.15+shimmer*(.15+.35*overhead)+glint*.12+impulse*.2)*(1-_AquariumNight*.7);
                    half swell=dot(slope,float2(.9,1.3))+impulse*.25;
                    return half4(color*(1+swell)+glare,1);
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
