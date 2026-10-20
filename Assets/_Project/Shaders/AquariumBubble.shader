Shader "Lumaquarium/Aquarium Bubble"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                float rim = exp(-abs(r - .77) * 27);
                float shine = exp(-length(p - float2(-.28,.32)) * 16);
                return half4(.65,.91,1,(rim * .4 + shine) * i.color.a * step(r,1));
            }
            ENDHLSL
        }
    }
}
