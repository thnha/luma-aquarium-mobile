Shader "Lumaquarium/Aquarium Glow"
{
    // Additive light in the water: sun shafts (a soft band, brightest under the surface) or, with
    // Round on, floating motes. Both fade out at night unless the top lamp is on, which tints them.
    // Round 2 is the backlight panel behind the tank: brightest along the sand, kept at night. It lights the
    // stone behind it (Src DstColor, Dst One: wall * (1 + lamp)) rather than laying colour over it, so the
    // wall keeps its texture and reads clearer, tinted by the lamp.
    Properties
    {
        _Color("Color", Color) = (.75,.95,1,.2)
        _Round("Round mote", Float) = 0
        [HideInInspector] _SrcBlend("Src blend", Float) = 1
        [HideInInspector] _DstBlend("Dst blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _AquariumNight, _AquariumLamp;
            half4 _AquariumLampColor;
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Round;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.uv=i.uv; o.color=i.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float glow;
                // Unset globals read 0, so without the lighting component the lamp leaves everything as it was.
                half3 tint=lerp(half3(1,1,1),_AquariumLampColor.rgb,_AquariumLamp);
                if(_Round>1.5)
                {
                    glow=pow(sin(i.uv.x*3.14159),.45)*(pow(1-i.uv.y,1.4)*.85+(1-i.uv.y)*.15);
                    // Alpha is .55 at full strength (AquariumLighting) and fades the light in and out.
                    float on=saturate(_Color.a/.55);
                    // Some light reaches the top of the wall too, so the whole back reads clearer. At night the
                    // stone it multiplies is far darker, so the lamp works harder to light it as brightly.
                    return half4(_Color.rgb*(glow*1.6+.25)*on*(1+_AquariumNight*2.5),1);
                }
                else if(_Round>.5) glow=pow(saturate(1-length(i.uv*2-1)),2)*i.color.a*max(1-_AquariumNight*.7,_AquariumLamp*.6);
                else
                {
                    // Vertex alpha carries each shaft's phase, so neighbours shimmer out of step.
                    float across=pow(sin(i.uv.x*3.14159),2);
                    float along=smoothstep(0,.7,i.uv.y)*(.75+.25*sin(i.uv.y*9+_Time.y*.8+i.color.a*20));
                    float shimmer=.65+.35*sin(_Time.y*.45+i.color.a*31);
                    glow=across*along*shimmer*max(1-_AquariumNight,_AquariumLamp*.85);
                }
                return half4(_Color.rgb*tint*i.color.rgb*_Color.a*glow,1);
            }
            ENDHLSL
        }
    }
}
