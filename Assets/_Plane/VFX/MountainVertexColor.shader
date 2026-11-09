Shader "Sky Courier/Mountain Vertex Color"
{
    Properties { _BaseColor("Teinte", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half3 color:COLOR; half fog:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                half3 normal=TransformObjectToWorldNormal(input.normalOS);
                Light sun=GetMainLight();
                half3 lighting=max(SampleSH(normal),half3(.025,.025,.035))+sun.color*saturate(dot(normal,sun.direction));
                output.color=input.color.rgb*_BaseColor.rgb*lighting;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target { UNITY_SETUP_INSTANCE_ID(input); return half4(MixFog(input.color,input.fog),1); }
            ENDHLSL
        }
    }
}
