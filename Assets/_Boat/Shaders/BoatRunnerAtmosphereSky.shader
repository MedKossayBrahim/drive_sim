Shader "BoatRunner/Atmosphere Sky"
{
    Properties
    {
        _ZenithColor ("Open Sea Sky", Color) = (0.22, 0.44, 0.62, 1)
        _HorizonColor ("Horizon Haze", Color) = (0.56, 0.72, 0.75, 1)
        _HazeColor ("Soft Haze", Color) = (0.66, 0.76, 0.77, 1)
        _HorizonWidth ("Horizon Blend", Range(0.02, 0.25)) = 0.08
        _HazeStrength ("Atmospheric Haze", Range(0, 0.5)) = 0.34
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _HazeColor;
            float _HorizonWidth;
            float _HazeStrength;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float3 ray : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Unity uses a reversed depth buffer on D3D/Vulkan platforms. The far plane
                // is clip-space zero there; using z = w makes the sky pass in front of geometry.
                #if defined(UNITY_REVERSED_Z)
                o.vertex.z = 0.0;
                #else
                o.vertex.z = o.vertex.w;
                #endif
                o.ray = v.vertex.xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float elevation = normalize(i.ray).y;
                float gradient = pow(saturate((elevation + _HorizonWidth) / (1.0 + _HorizonWidth)), 0.55);
                fixed3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, gradient);
                float haze = exp(-abs(elevation) * 28.0) * _HazeStrength;
                color = lerp(color, _HazeColor.rgb, haze);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}