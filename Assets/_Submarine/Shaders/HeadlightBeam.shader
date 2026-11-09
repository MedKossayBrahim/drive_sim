Shader "DriveSim/HeadlightBeam"
{
    Properties
    {
        _Color ("Beam Color", Color) = (0.55, 0.82, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Length ("Length", Float) = 60
        _CorePower ("Core Falloff", Float) = 2.2
        _FadePower ("Length Falloff", Float) = 1.6
        _NoiseScale ("Noise Scale", Float) = 0.35
        _NoiseSpeed ("Noise Speed", Float) = 0.35
        _SoftDistance ("Soft Intersection", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HeadlightBeam"
            Tags { "LightMode" = "UniversalForward" }

            // Additive colour, alpha left untouched (Zero One) so the beam never
            // contaminates the camera alpha channel used by post-processing.
            Blend One One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Length;
                float _CorePower;
                float _FadePower;
                float _NoiseScale;
                float _NoiseSpeed;
                float _SoftDistance;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Each slice is a soft, camera-facing disc. Stacked along the beam
                // they read as a volume of scattered light from any viewing angle.
                float2 centered = input.uv * 2.0 - 1.0;
                float r = length(centered);
                float radial = pow(saturate(1.0 - r), _CorePower);
                radial *= 1.0 - smoothstep(0.72, 1.0, r); // feathered rim

                // Organic particulate shimmer so the shaft is not a flat gradient.
                float3 p = input.positionWS * _NoiseScale;
                float2 nuv = float2(p.x + p.y * 0.35, p.z - p.y * 0.2) + float2(0.0, _Time.y * _NoiseSpeed);
                float n = noise2(nuv) * 0.6 + noise2(nuv * 2.7 + 13.0) * 0.4;
                n = 0.7 + n * 0.6;

                // Soft intersection with opaque geometry (no hard clipping into rocks).
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-4);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float soft = saturate((sceneEye - fragEye) / max(_SoftDistance, 0.01));

                float a = radial * n * soft * input.color.a * _Intensity;
                return half4(_Color.rgb * a, a * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
