Shader "RoyaltyBoat/Burning Oil Fire"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1.0, 0.78, 0.08, 1.0)
        _MiddleColor ("Middle Color", Color) = (1.0, 0.28, 0.015, 1.0)
        _TipColor ("Tip Color", Color) = (0.72, 0.035, 0.012, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+20"
        }

        Cull Back
        ZWrite On

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MiddleColor;
                half4 _TipColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float height : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.height = saturate(input.positionOS.y / 1.58);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = lerp(
                    _BaseColor.rgb,
                    _MiddleColor.rgb,
                    smoothstep(0.08, 0.58, input.height));
                color = lerp(
                    color,
                    _TipColor.rgb,
                    smoothstep(0.52, 1.0, input.height));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
