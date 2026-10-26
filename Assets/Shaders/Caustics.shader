Shader "DriveSim/Caustics"
{
    Properties
    {
        _CausticsTex ("Caustics Texture", 2D) = "white" {}
        _Speed ("Speed", Range(0, 5)) = 1
        _Scale ("Scale", Range(0, 20)) = 5
        _Intensity ("Intensity", Range(0, 3)) = 1
        _Color ("Color", Color) = (0.5, 0.8, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "CausticsPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Speed;
                float _Scale;
                float _Intensity;
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv1 = input.worldPos.xz * _Scale + _Time.y * _Speed;
                float2 uv2 = input.worldPos.xz * _Scale * 1.3 - _Time.y * _Speed * 0.7;

                float c1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv1).r;
                float c2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv2).r;

                float caustics = c1 * c2 * 2.0;
                caustics = pow(caustics, 2.0);

                float3 color = _Color.rgb * caustics * _Intensity;
                return half4(color, caustics * _Intensity);
            }
            ENDHLSL
        }
    }
}
