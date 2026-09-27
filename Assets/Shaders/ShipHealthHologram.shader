Shader "RoyaltyBoat/UI/ShipHealthHologram"
{
    Properties
    {
        _BaseColor ("Hologram Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _XRayAmount ("X-Ray Amount", Range(0, 1)) = 0
        _OutlineWidth ("Outline Width", Range(0, 0.15)) = 0.015
        _GridScale ("Grid Scale", Range(0.25, 12)) = 2.5
        _ScanDensity ("Scan Density", Range(1, 80)) = 24
        _ScanSpeed ("Scan Speed", Range(-10, 10)) = 1.5
        _FresnelPower ("Fresnel Power", Range(0.25, 8)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "HologramOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha One
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Opacity;
                float _XRayAmount;
                float _OutlineWidth;
                float _GridScale;
                float _ScanDensity;
                float _ScanSpeed;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                float3 expanded = input.positionOS.xyz + input.normalOS * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half outlineAlpha = lerp(0.22h, 0.58h, _XRayAmount) * _Opacity;
                return half4(_BaseColor.rgb * 1.2h, outlineAlpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DamagedXRay"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha One
            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex XRayVertex
            #pragma fragment XRayFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Opacity;
                float _XRayAmount;
                float _OutlineWidth;
                float _GridScale;
                float _ScanDensity;
                float _ScanSpeed;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings XRayVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 XRayFragment(Varyings input) : SV_Target
            {
                clip(_XRayAmount - 0.001h);
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0h - abs(dot(normalWS, viewDirection))), 1.7h);
                half fineLines = pow(saturate(sin(input.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed * 2.4h) * 0.5h + 0.5h), 14.0h);
                half sweep = pow(saturate(sin(input.positionWS.y * 3.2h - _Time.y * 2.1h) * 0.5h + 0.5h), 10.0h);
                half strobe = 0.72h + 0.28h * (sin(_Time.y * 7.0h) * 0.5h + 0.5h);
                half intensity = (0.16h + rim * 0.46h + fineLines * 0.5h + sweep * 0.7h) * strobe;
                return half4(_BaseColor.rgb * intensity, _XRayAmount * (0.12h + rim * 0.2h + fineLines * 0.28h + sweep * 0.35h));
            }
            ENDHLSL
        }

        Pass
        {
            Name "HologramSurface"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex HologramVertex
            #pragma fragment HologramFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Opacity;
                float _XRayAmount;
                float _OutlineWidth;
                float _GridScale;
                float _ScanDensity;
                float _ScanSpeed;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings HologramVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 HologramFragment(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(saturate(1.0h - abs(dot(normalWS, viewDirection))), _FresnelPower);

                float3 gridCoordinate = abs(frac(input.positionOS * _GridScale + 0.5) - 0.5) / fwidth(input.positionOS * _GridScale);
                half grid = 1.0h - saturate(min(gridCoordinate.x, min(gridCoordinate.y, gridCoordinate.z)));
                half fineLines = pow(saturate(sin(input.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed * 2.4h) * 0.5h + 0.5h), 14.0h);
                half sweep = pow(saturate(sin(input.positionWS.y * 3.2h - _Time.y * 2.1h) * 0.5h + 0.5h), 10.0h);
                half strobe = 0.78h + 0.22h * (sin(_Time.y * 7.0h) * 0.5h + 0.5h);

                half gridStrength = lerp(0.025h, 0.16h, _XRayAmount);
                half glow = (0.68h + fresnel * 0.65h + grid * gridStrength + fineLines * 0.16h + sweep * 0.32h) * strobe;
                half alpha = _Opacity;
                return half4(_BaseColor.rgb * glow, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
