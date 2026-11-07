Shader "Sky Courier/Panoramic Blend"
{
    Properties
    {
        [NoScaleOffset] _TexA ("Panorama A", 2D) = "grey" {}
        [NoScaleOffset] _TexB ("Panorama B", 2D) = "grey" {}
        _TintA ("Tint A", Color) = (.5,.5,.5,1)
        _TintB ("Tint B", Color) = (.5,.5,.5,1)
        _Exposure ("Exposure", Float) = 1
        _RotationA ("Rotation A", Float) = 0
        _RotationB ("Rotation B", Float) = 0
        _Rotation ("Shared world rotation", Float) = 0
        _Blend ("Blend", Range(0,1)) = 0
        _OriginalSunA ("Original sun A", Vector) = (0,1,0,0)
        _OriginalSunB ("Original sun B", Vector) = (0,1,0,0)
        _SunDirection ("Distant sun direction", Vector) = (0,.707,.707,0)
        [HDR] _SunColor ("Sun color", Color) = (24,20,14,1)
        _SunParams ("Mask and disk cosines", Vector) = (.992546,.997,.999877,.999936)
        _SunSamples ("Background sample angles", Vector) = (.9874,.1581,.9961947,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _TexA, _TexB;
            half4 _TexA_HDR, _TexB_HDR, _TintA, _TintB, _SunColor;
            float4 _OriginalSunA, _OriginalSunB, _SunDirection, _SunParams, _SunSamples;
            float _RotationA, _RotationB, _Rotation, _Blend, _Exposure;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;
            }
            float3 panoramaDirection(float3 direction,float angle)
            {
                float sine,cosine;sincos(radians(angle),sine,cosine);
                return normalize(float3(cosine*direction.x-sine*direction.z,direction.y,sine*direction.x+cosine*direction.z));
            }
            float2 panoramaUV(float3 direction)
            {
                float3 d=normalize(direction);
                float2 spherical=float2(atan2(d.z,d.x),acos(clamp(d.y,-1,1)))*float2(.5/UNITY_PI,1/UNITY_PI);
                float2 uv=float2(.5,1)-spherical;uv.x=frac(uv.x);return uv;
            }
            half3 readPanorama(sampler2D panorama,half4 hdr,float3 direction)
            { return DecodeHDR(tex2D(panorama,panoramaUV(direction)),hdr); }
            half3 cleanPanorama(sampler2D panorama,half4 hdr,float3 direction,float4 original)
            {
                half3 color=readPanorama(panorama,hdr,direction);
                float alignment=dot(direction,original.xyz);
                // Efface le disque bas du HDRI sans incliner les nuages ni déplacer l'horizon.
                // Les quatre lectures supplémentaires sont limitées à sa petite région angulaire.
                if(original.w>.5 && alignment>_SunParams.x)
                {
                    float3 tangent=normalize(cross(float3(0,1,0),original.xyz));
                    float3 bitangent=cross(original.xyz,tangent);
                    float3 baseDirection=original.xyz*_SunSamples.x;
                    half3 background=readPanorama(panorama,hdr,baseDirection+tangent*_SunSamples.y)
                        +readPanorama(panorama,hdr,baseDirection-tangent*_SunSamples.y)
                        +readPanorama(panorama,hdr,baseDirection+bitangent*_SunSamples.y)
                        +readPanorama(panorama,hdr,baseDirection-bitangent*_SunSamples.y);
                    float mask=smoothstep(_SunParams.x,_SunParams.y,alignment);
                    color=lerp(color,background*.25h,mask);
                }
                return color;
            }
            half4 frag(v2f i):SV_Target
            {
                float3 direction=normalize(i.direction);
                half3 a=cleanPanorama(_TexA,_TexA_HDR,panoramaDirection(direction,_RotationA+_Rotation),_OriginalSunA)*_TintA.rgb*unity_ColorSpaceDouble.rgb;
                half3 b=cleanPanorama(_TexB,_TexB_HDR,panoramaDirection(direction,_RotationB+_Rotation),_OriginalSunB)*_TintB.rgb*unity_ColorSpaceDouble.rgb;
                half3 color=lerp(a,b,_Blend);
                float alignment=dot(direction,normalize(_SunDirection.xyz));
                float disk=smoothstep(_SunParams.z,_SunParams.w,alignment);
                float halo=smoothstep(_SunSamples.z,1,alignment);
                float visible=lerp(_OriginalSunA.w,_OriginalSunB.w,_Blend);
                // Astre à l'infini : seule la direction intervient, jamais la position ou l'altitude de l'avion.
                color+=_SunColor.rgb*(disk+halo*halo*.035)*visible;
                return half4(color*_Exposure,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
