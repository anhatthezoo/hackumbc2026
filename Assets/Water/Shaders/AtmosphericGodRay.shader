Shader "Atmosphere/Soft God Ray"
{
    Properties
    {
        [HDR] _Color ("Ray Color", Color) = (1.4, 1.1, 0.75, 0.04)
        _Softness ("Edge Softness", Range(1.0, 6.0)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "GodRay"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Softness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float edge = saturate(1.0 - abs(input.uv.x * 2.0 - 1.0));
                float beam = pow(edge, _Softness);
                float verticalFade = smoothstep(0.0, 0.16, input.uv.y)
                    * (1.0 - smoothstep(0.72, 1.0, input.uv.y));
                float wisps = 0.82 + 0.18 * sin(input.uv.y * 24.0 + input.uv.x * 5.0);
                float alpha = _Color.a * beam * verticalFade * wisps;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
