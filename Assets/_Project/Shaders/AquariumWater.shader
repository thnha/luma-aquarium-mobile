Shader "Lumaquarium/Aquarium Water"
{
    Properties { _Strength("Wave strength", Range(0,2)) = 1 }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            float4 _Ripples[4];
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                p.y += (sin(p.x * 14 + _Time.y * 1.5) * .006 + sin(p.z * 19 - _Time.y) * .004) * _Strength;
                o.world = p;
                o.positionCS = TransformWorldToHClip(p);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.world.xz;
                float t = _Time.y;
                float a = sin(p.x * 18 + sin(p.y * 14 + t) * 2 + t * .7);
                float b = sin(p.y * 24 + sin(p.x * 12 - t * .6) * 2);
                float filaments = pow(saturate(1 - abs(a + b) * .75), 12);
                float impulse = 0;
                for (int i = 0; i < 4; i++)
                {
                    float age = t - _Ripples[i].w;
                    float d = distance(p, _Ripples[i].xy);
                    impulse += sin(d * 55 - age * 8) * exp(-abs(d - age * .28) * 25) * saturate(1 - age / 3) * step(0, age);
                }
                float gleam = filaments * .5 + saturate(impulse) * .7;
                float sun = pow(saturate(1 - abs(p.x - .45 + sin(p.y * 12 + t) * .12)), 20);
                float farFade = saturate((2.6 - p.y) / 1.8);
                return half4(lerp(float3(.05,.36,.4), float3(.94,.92,.63), saturate(gleam + sun * .65)),
                    (.065 + gleam * .23) * _Strength * farFade);
            }
            ENDHLSL
        }
    }
}
