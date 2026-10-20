Shader "Lumaquarium/Aquarium Bubble"
{
    Properties { _EnvironmentTex("Aquarium environment", 2D) = "gray" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_EnvironmentTex); SAMPLER(sampler_EnvironmentTex);
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 screen:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.screen=ComputeScreenPos(o.positionCS); o.uv=i.uv; o.color=i.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float r2=dot(p,p);
                clip(.99-r2);
                float3 normal=float3(p,sqrt(saturate(1-r2)));
                float fresnel=pow(1-normal.z,3);
                float2 uv=i.screen.xy/i.screen.w;
                half3 environment=SAMPLE_TEXTURE2D(_EnvironmentTex,sampler_EnvironmentTex,saturate(uv+normal.xy*.018)).rgb;
                float2 h=p-float2(-.29,.4);
                float highlight=exp(-dot(h,h)*145);
                float crescent=pow(saturate(dot(normal,normalize(float3(-.5,.75,.42)))),20);
                float rim=exp(-abs(sqrt(r2)-.94)*65);
                float edge=1-smoothstep(.94,.99,r2);
                half3 color=lerp(environment*.45,half3(.72,.88,.86),fresnel*.7);
                color+=half3(1,.99,.88)*(highlight*.9+crescent*.6);
                color*=1-saturate(-p.y)*rim*.48;
                float alpha=(.035+fresnel*.38+rim*.18+highlight*.62+crescent*.25)*edge*i.color.a;
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
