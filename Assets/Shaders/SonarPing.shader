Shader "DriveSim/SonarPing"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 0.5, 1)
        _Radius ("Radius", Range(0, 500)) = 100
        _Width ("Ring Width", Range(0, 50)) = 5
        _Intensity ("Intensity", Range(0, 5)) = 2
        _Fade ("Fade", Range(0, 5)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SonarPingPass"
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
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius;
                float _Width;
                float _Intensity;
                float _Fade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance from center (origin of the ping)
                float dist = length(input.worldPos.xz);

                // Ring
                float ring = abs(dist - _Radius);
                float ringMask = 1.0 - smoothstep(0, _Width, ring);

                // Fade with distance from center
                float fade = 1.0 - saturate(dist / (_Radius + _Fade * 50));

                // Fade with distance from ring
                float alpha = ringMask * fade * _Intensity;

                // Add some noise for organic feel
                float noise = frac(sin(dot(input.worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
                alpha *= 0.8 + noise * 0.2;

                float3 color = _Color.rgb * alpha;
                return half4(color, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
