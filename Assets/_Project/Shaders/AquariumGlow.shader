Shader "Lumaquarium/Aquarium Glow"
{
    // Additive light in the water: sun shafts (a soft band, brightest under the surface) or, with
    // Round on, floating motes. Both fade out at night.
    Properties
    {
        _Color("Color", Color) = (.75,.95,1,.2)
        _Round("Round mote", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _AquariumNight;
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
                if(_Round>.5) glow=pow(saturate(1-length(i.uv*2-1)),2)*i.color.a*(1-_AquariumNight*.7);
                else
                {
                    // Vertex alpha carries each shaft's phase, so neighbours shimmer out of step.
                    float across=pow(sin(i.uv.x*3.14159),2);
                    float along=smoothstep(0,.7,i.uv.y)*(.75+.25*sin(i.uv.y*9+_Time.y*.8+i.color.a*20));
                    float shimmer=.65+.35*sin(_Time.y*.45+i.color.a*31);
                    glow=across*along*shimmer*(1-_AquariumNight);
                }
                return half4(_Color.rgb*i.color.rgb*_Color.a*glow,1);
            }
            ENDHLSL
        }
    }
}
