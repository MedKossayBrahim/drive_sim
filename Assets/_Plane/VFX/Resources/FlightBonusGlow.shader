Shader "Sky Courier/Flight Bonus Glow"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                half2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                half2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = saturate(1.0h - abs(input.uv.y * 2.0h - 1.0h));
                half4 glow = input.color;
                glow.rgb = lerp(glow.rgb, half3(1, 1, 1), smoothstep(0.65h, 1.0h, edge) * 0.6h);
                glow.a *= smoothstep(0.0h, 0.75h, edge);
                return glow;
            }
            ENDHLSL
        }
    }
}
