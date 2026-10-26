Shader "DriveSim/UnderwaterWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.1, 0.4, 0.6, 0.8)
        _DeepColor ("Deep Color", Color) = (0.02, 0.1, 0.25, 0.95)
        _DepthFade ("Depth Fade", Range(0, 10)) = 3
        _WaveAmplitude ("Wave Amplitude", Range(0, 2)) = 0.5
        _WaveFrequency ("Wave Frequency", Range(0, 10)) = 2
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1
        _FoamColor ("Foam Color", Color) = (0.9, 0.95, 1, 1)
        _FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.8
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.3
        _SpecularPower ("Specular Power", Range(1, 256)) = 64
        _CausticsTex ("Caustics Texture", 2D) = "white" {}
        _CausticsStrength ("Caustics Strength", Range(0, 2)) = 0.5
        _CausticsSpeed ("Caustics Speed", Range(0, 5)) = 1
        _CausticsScale ("Caustics Scale", Range(0, 10)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthFade;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float4 _FoamColor;
                float _FoamThreshold;
                float _NormalStrength;
                float _ReflectionStrength;
                float _SpecularPower;
                float _CausticsStrength;
                float _CausticsSpeed;
                float _CausticsScale;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            float3 GerstnerWave(float3 pos, float amplitude, float frequency, float speed, float2 direction)
            {
                float phase = frequency * dot(pos.xz, direction) + _Time.y * speed;
                float cosPhase = cos(phase);
                float sinPhase = sin(phase);

                float3 displacement = float3(
                    direction.x * amplitude * cosPhase,
                    amplitude * sinPhase,
                    direction.y * amplitude * cosPhase
                );

                return displacement;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // Gerstner waves
                float3 wave1 = GerstnerWave(posWS, _WaveAmplitude, _WaveFrequency, _WaveSpeed, float2(1, 0.5));
                float3 wave2 = GerstnerWave(posWS, _WaveAmplitude * 0.5, _WaveFrequency * 1.5, _WaveSpeed * 1.3, float2(-0.7, 0.3));
                float3 wave3 = GerstnerWave(posWS, _WaveAmplitude * 0.3, _WaveFrequency * 2.1, _WaveSpeed * 0.8, float2(0.4, -0.8));

                posWS += wave1 + wave2 + wave3;

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // Normal map
                float2 normalUV = input.positionWS.xz * 0.1;
                float3 normalMap = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV + _Time.y * 0.05));
                normalWS = normalize(normalWS + normalMap * _NormalStrength);

                // View direction
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                // Lighting
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float NdotL = saturate(dot(normalWS, lightDir));

                // Specular
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normalWS, halfDir)), _SpecularPower);

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 4.0);

                // Depth color
                float depth = saturate(input.positionWS.y * -0.1 + 0.5);
                float4 waterColor = lerp(_DeepColor, _ShallowColor, depth);

                // Caustics
                float2 causticsUV = input.positionWS.xz * _CausticsScale + _Time.y * _CausticsSpeed;
                float caustics = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticsUV).r;
                caustics *= _CausticsStrength * NdotL;

                // Foam at edges (simplified)
                float foam = smoothstep(_FoamThreshold, 1.0, fresnel);

                // Final color
                float3 finalColor = waterColor.rgb;
                finalColor += mainLight.color * spec * 0.5;
                finalColor += fresnel * _ReflectionStrength;
                finalColor += caustics * _ShallowColor.rgb;
                finalColor = lerp(finalColor, _FoamColor.rgb, foam * 0.5);

                float alpha = waterColor.a;

                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
