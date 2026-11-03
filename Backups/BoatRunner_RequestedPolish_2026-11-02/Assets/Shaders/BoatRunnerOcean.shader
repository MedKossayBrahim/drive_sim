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
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 100
        Cull Off
        Pass
        {
            ZWrite On
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _OceanNoise;
            fixed4 _DeepColor;
            fixed4 _MidColor;
            fixed4 _TurquoiseColor;
            fixed4 _HighlightColor;
            float _WaveHeight;
            float _NoiseScale;
            float _HighlightStrength;
            float _FoamStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 world : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 p = world.xz;
                float t = _Time.y;
                float4 noise = tex2Dlod(_OceanNoise, float4(p * _NoiseScale + float2(t * 0.0018, -t * 0.0013), 0, 0));
                float waveA = sin(dot(p, float2(0.037, 0.022)) + t * 0.18 + (noise.r - 0.5) * 0.55);
                float waveB = sin(dot(p, float2(-0.026, 0.049)) - t * 0.13 + (noise.g - 0.5) * 0.50);
                float waveC = sin(dot(p, float2(0.018, -0.055)) + t * 0.21 + (noise.b - 0.5) * 0.42);
                world.y += (waveA * 0.50 + waveB * 0.32 + waveC * 0.18) * _WaveHeight;
                o.vertex = UnityWorldToClipPos(world);
                o.world = world;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.world.xz;
                float2 flow = float2(t * 0.0018, -t * 0.0013);
                float4 broad = tex2D(_OceanNoise, p * _NoiseScale + flow);
                float2 warpedP = p + (float2(broad.g, broad.b) - 0.5) * 7.0;
                float4 detail = tex2D(_OceanNoise, warpedP * (_NoiseScale * 2.35) + float2(-t * 0.0023, t * 0.0017));

                float broadTone = broad.r * 0.48 + broad.g * 0.34 + broad.b * 0.18;
                float detailTone = detail.r * 0.55 + detail.g * 0.30 + detail.b * 0.15;
                float seaTone = saturate(0.5 + (broadTone - 0.5) * 2.6 + (detailTone - 0.5) * 1.3);
                float blueMix = smoothstep(0.32, 0.70, seaTone);
                fixed3 color = lerp(_DeepColor.rgb, _MidColor.rgb, blueMix);

                float turquoisePatch = smoothstep(0.40, 0.64, broad.b * 0.75 + detail.g * 0.25);
                color = lerp(color, _TurquoiseColor.rgb, turquoisePatch * 0.68);

                float crestField = detail.a * 0.62 + broad.a * 0.38;
                float crest = smoothstep(0.70, 0.86, crestField) * _HighlightStrength;
                float foamPatch = smoothstep(0.84, 0.95, detail.a) * _FoamStrength;
                color = lerp(color, _HighlightColor.rgb, saturate(crest + foamPatch));
                UNITY_APPLY_FOG(i.fogCoord, color);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}