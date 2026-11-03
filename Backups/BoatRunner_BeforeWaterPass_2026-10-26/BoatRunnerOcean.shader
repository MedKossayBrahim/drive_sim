Shader "BoatRunner/Ocean"
{
    Properties
    {
        _DeepColor ("Deep Blue", Color) = (0.02, 0.15, 0.24, 1)
        _MidColor ("Teal", Color) = (0.035, 0.38, 0.44, 1)
        _CrestColor ("Wave Crest", Color) = (0.26, 0.72, 0.72, 1)
        _WaveHeight ("Wave Height", Float) = 0.17
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _DeepColor;
            fixed4 _MidColor;
            fixed4 _CrestColor;
            float _WaveHeight;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float waveA = sin(world.x * 0.115 + world.z * 0.045 + t * 0.72);
                float waveB = sin(world.z * 0.16 - world.x * 0.055 - t * 0.94);
                float waveC = sin((world.x + world.z) * 0.29 + t * 1.2);
                world.y += (waveA * 0.54 + waveB * 0.34 + waveC * 0.12) * _WaveHeight;
                o.vertex = UnityWorldToClipPos(world);
                o.world = world;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float broad = sin(i.world.x * 0.06 + i.world.z * 0.055 + t * 0.36) * 0.5 + 0.5;
                float ribbons = sin(i.world.x * 0.31 + i.world.z * 0.13 - t * 1.5) * 0.5 + 0.5;
                float crest = smoothstep(0.9, 0.995, ribbons) * (0.35 + broad * 0.65);
                fixed3 color = lerp(_DeepColor.rgb, _MidColor.rgb, smoothstep(0.12, 0.9, broad));
                color = lerp(color, _CrestColor.rgb, crest * 0.68);
                float glint = pow(saturate(sin(i.world.x * 0.72 - i.world.z * 0.09 + t * 1.1) * 0.5 + 0.5), 18.0);
                color += glint * 0.09;
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
