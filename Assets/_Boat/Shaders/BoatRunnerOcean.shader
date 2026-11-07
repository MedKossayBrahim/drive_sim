Shader "BoatRunner/Ocean"
{
    Properties
    {
        _DeepColor ("Deep Blue", Color) = (0.055, 0.20, 0.31, 1)
        _MidColor ("Atlantic Blue", Color) = (0.075, 0.36, 0.50, 1)
        _TurquoiseColor ("Turquoise Shoals", Color) = (0.10, 0.49, 0.55, 1)
        _HighlightColor ("Soft Wave Highlights", Color) = (0.39, 0.64, 0.70, 1)
        _OceanNoise ("Ocean Noise", 2D) = "gray" {}
        _WaveHeight ("Wave Height", Float) = 0.10
        _NoiseScale ("Noise Scale", Float) = 0.0035
        _HighlightStrength ("Highlight Strength", Range(0, 0.3)) = 0.10
        _FoamStrength ("Foam Strength", Range(0, 0.2)) = 0.06
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ BOATRUNNER_MOBILE_WATER
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_OceanNoise);
            SAMPLER(sampler_OceanNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _MidColor;
                float4 _TurquoiseColor;
                float4 _HighlightColor;
                float4 _OceanNoise_ST;
                float _WaveHeight;
                float _NoiseScale;
                float _HighlightStrength;
                float _FoamStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 p = positionWS.xz;
                float t = _Time.y;

                #if defined(BOATRUNNER_MOBILE_WATER)
                float waveA = sin(p.x * 0.041 + p.y * 0.019 + t * 0.20);
                float waveB = sin(p.x * -0.024 + p.y * 0.046 - t * 0.15);
                float waveC = sin(p.x * 0.017 - p.y * 0.052 + t * 0.17);
                #else
                float4 noiseUV = float4(p * _NoiseScale + float2(t * 0.0018, -t * 0.0013), 0, 0);
                float4 noise = SAMPLE_TEXTURE2D_LOD(_OceanNoise, sampler_OceanNoise, noiseUV.xy, 0);
                float waveA = sin(dot(p, float2(0.037, 0.022)) + t * 0.18 + (noise.r - 0.5) * 0.55);
                float waveB = sin(dot(p, float2(-0.026, 0.049)) - t * 0.13 + (noise.g - 0.5) * 0.50);
                float waveC = sin(dot(p, float2(0.018, -0.055)) + t * 0.21 + (noise.b - 0.5) * 0.42);
                #endif

                positionWS.y += (waveA * 0.50 + waveB * 0.32 + waveC * 0.18) * _WaveHeight;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 p = input.positionWS.xz;

                #if defined(BOATRUNNER_MOBILE_WATER)
                float broadTone = 0.5 + 0.25 * sin(p.x * 0.010 + p.y * 0.007 + t * 0.035);
                float blueMix = smoothstep(0.28, 0.72, broadTone);
                float3 color = lerp(_DeepColor.rgb, _MidColor.rgb, blueMix);
                float shoal = smoothstep(0.44, 0.76, sin(p.x * 0.006 - p.y * 0.008 + t * 0.022) * 0.5 + 0.5);
                color = lerp(color, _TurquoiseColor.rgb, shoal * 0.42);
                float crest = smoothstep(0.88, 0.98, sin(p.x * 0.041 + p.y * 0.019 + t * 0.20)) * (_HighlightStrength * 0.42);
                color = lerp(color, _HighlightColor.rgb, crest);
                return half4(color, 1);
                #else
                float2 flow = float2(t * 0.0018, -t * 0.0013);
                float4 broad = SAMPLE_TEXTURE2D(_OceanNoise, sampler_OceanNoise, p * _NoiseScale + flow);
                float2 warpedP = p + (float2(broad.g, broad.b) - 0.5) * 7.0;
                float4 detail = SAMPLE_TEXTURE2D(_OceanNoise, sampler_OceanNoise, warpedP * (_NoiseScale * 2.35) + float2(-t * 0.0023, t * 0.0017));
                float broadTone = broad.r * 0.48 + broad.g * 0.34 + broad.b * 0.18;
                float detailTone = detail.r * 0.55 + detail.g * 0.30 + detail.b * 0.15;
                float seaTone = saturate(0.5 + (broadTone - 0.5) * 2.6 + (detailTone - 0.5) * 1.3);
                float blueMix = smoothstep(0.32, 0.70, seaTone);
                float3 color = lerp(_DeepColor.rgb, _MidColor.rgb, blueMix);
                float turquoisePatch = smoothstep(0.40, 0.64, broad.b * 0.75 + detail.g * 0.25);
                color = lerp(color, _TurquoiseColor.rgb, turquoisePatch * 0.68);
                float crestField = detail.a * 0.62 + broad.a * 0.38;
                float crest = smoothstep(0.70, 0.86, crestField) * _HighlightStrength;
                float foamPatch = smoothstep(0.84, 0.95, detail.a) * _FoamStrength;
                color = lerp(color, _HighlightColor.rgb, saturate(crest + foamPatch));
                return half4(color, 1);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
