Shader "Atmosphere/Distant Volumetric Fog"
{
    Properties
    {
        [HDR] _Color ("Fog Color", Color) = (0.53, 0.73, 0.92, 1)
        _Density ("Density", Range(0, 1)) = 0.24
        _NoiseScale ("Noise Scale", Range(0.5, 12)) = 4
        _NoiseSpeed ("Noise Speed", Vector) = (0.012, 0.004, 0, 0)
        _BandHeight ("Band Height", Range(0.1, 1)) = 0.64
        _LayerPhase ("Layer Phase", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-20"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "DistantFog"

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
                float _Density;
                float _NoiseScale;
                float4 _NoiseSpeed;
                float _BandHeight;
                float _LayerPhase;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + 1);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0;
                float amplitude = 0.55;
                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += SmoothNoise(p) * amplitude;
                    p = p * 2.03 + 13.7;
                    amplitude *= 0.48;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 drift = _Time.y * _NoiseSpeed.xy;
                float2 noiseUv = input.uv * float2(_NoiseScale, _NoiseScale * 0.55)
                    + drift + float2(_LayerPhase, _LayerPhase * 0.37);
                float cloud = Fbm(noiseUv);
                float fineDetail = Fbm(noiseUv * 2.15 - drift * 0.6);
                cloud = saturate(cloud * 0.72 + fineDetail * 0.28);

                float halfBand = max(_BandHeight * 0.5, 0.01);
                float horizonDistance = abs(input.uv.y - 0.5) / halfBand;
                float verticalMask = 1.0 - smoothstep(0.18, 1.0, horizonDistance);
                float edgeFade = smoothstep(0.0, 0.08, input.uv.x)
                    * smoothstep(0.0, 0.08, 1.0 - input.uv.x);
                float densityVariation = lerp(0.42, 1.0, cloud);
                float alpha = saturate(_Density * verticalMask * edgeFade * densityVariation);

                half3 color = _Color.rgb * lerp(0.9, 1.08, cloud);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
