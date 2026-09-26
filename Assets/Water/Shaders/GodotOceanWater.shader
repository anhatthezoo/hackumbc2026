Shader "Ocean/Godot Ocean Water URP"
{
    Properties
    {
        [Header(Surface)]
        _WaterColor ("Water Color", Color) = (0.1, 0.15, 0.18, 1)
        _FoamColor ("Foam Color", Color) = (0.73, 0.67, 0.62, 1)
        _Roughness ("Roughness", Range(0.02, 1.0)) = 0.65
        _Reflectance ("Normal-Incidence Reflectance", Range(0.0, 0.08)) = 0.02
        _NormalStrength ("Normal Strength", Range(0.0, 2.0)) = 1.0
        _SubsurfaceStrength ("Subsurface Strength", Range(0.0, 2.0)) = 1.0
        _EnvironmentReflectionStrength ("Environment Reflection Strength", Range(0.0, 2.0)) = 1.0

        [Header(Transmission)]
        _TransmissionColor ("Transmission Color", Color) = (0.04, 0.38, 0.42, 1)
        _TransmissionStrength ("Transmission Strength", Range(0.0, 2.0)) = 0.75

        [Header(Foam)]
        _FoamStrength ("Accumulated Foam Strength", Range(0.0, 4.0)) = 2.2
        _CrestFoamStrength ("Crest Foam Strength", Range(0.0, 2.0)) = 0.75
        _FoamBrightness ("Foam Brightness", Range(0.0, 3.0)) = 1.35

        [Header(FFT Cascade Textures)]
        [NoScaleOffset] _DisplacementArray ("Displacement Array", 2DArray) = "" {}
        [NoScaleOffset] _NormalFoamArray ("Normal / Foam Array", 2DArray) = "" {}
        [NoScaleOffset] _PreviousDisplacementArray ("Previous Displacement Array", 2DArray) = "" {}
        [NoScaleOffset] _PreviousNormalFoamArray ("Previous Normal / Foam Array", 2DArray) = "" {}
        _CascadeCount ("Cascade Count", Int) = 0

        [Header(Cascade Scales)]
        _Cascade0Scale ("Cascade 0 (UV XY, Displacement, Normal)", Vector) = (0.0113636, 0.0113636, 1.0, 1.0)
        _Cascade1Scale ("Cascade 1 (UV XY, Displacement, Normal)", Vector) = (0.0175439, 0.0175439, 0.75, 1.0)
        _Cascade2Scale ("Cascade 2 (UV XY, Displacement, Normal)", Vector) = (0.0625, 0.0625, 0.0, 0.25)
        _Cascade3Scale ("Cascade 3 (UV XY, Displacement, Normal)", Vector) = (0.01, 0.01, 0.0, 0.0)
        _Cascade4Scale ("Cascade 4 (UV XY, Displacement, Normal)", Vector) = (0.01, 0.01, 0.0, 0.0)
        _Cascade5Scale ("Cascade 5 (UV XY, Displacement, Normal)", Vector) = (0.01, 0.01, 0.0, 0.0)
        _Cascade6Scale ("Cascade 6 (UV XY, Displacement, Normal)", Vector) = (0.01, 0.01, 0.0, 0.0)
        _Cascade7Scale ("Cascade 7 (UV XY, Displacement, Normal)", Vector) = (0.01, 0.01, 0.0, 0.0)

        [HideInInspector] _Cascade0Blend ("Cascade 0 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade1Blend ("Cascade 1 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade2Blend ("Cascade 2 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade3Blend ("Cascade 3 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade4Blend ("Cascade 4 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade5Blend ("Cascade 5 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade6Blend ("Cascade 6 Blend", Range(0.0, 1.0)) = 1.0
        [HideInInspector] _Cascade7Blend ("Cascade 7 Blend", Range(0.0, 1.0)) = 1.0

        [Header(Distance Fades)]
        _DisplacementFadeStart ("Displacement Fade Start", Float) = 150.0
        _DisplacementFadeRate ("Displacement Fade Rate", Range(0.0001, 0.05)) = 0.007
        _FoamFadeRate ("Foam Fade Rate", Range(0.0, 0.05)) = 0.0075
        _NormalFadeRate ("Normal Fade Rate", Range(0.0, 0.05)) = 0.0175
        _DistantNormalStrength ("Distant Normal Strength", Range(0.0, 0.1)) = 0.015
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Back
        ZWrite On

        HLSLINCLUDE
        #pragma target 4.5
        #pragma require 2darray

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #define MAX_CASCADES 8
        #define PI 3.14159265358979323846

        TEXTURE2D_ARRAY(_DisplacementArray);
        SAMPLER(sampler_DisplacementArray);
        TEXTURE2D_ARRAY(_NormalFoamArray);
        SAMPLER(sampler_NormalFoamArray);
        TEXTURE2D_ARRAY(_PreviousDisplacementArray);
        SAMPLER(sampler_PreviousDisplacementArray);
        TEXTURE2D_ARRAY(_PreviousNormalFoamArray);
        SAMPLER(sampler_PreviousNormalFoamArray);

        CBUFFER_START(UnityPerMaterial)
            half4 _WaterColor;
            half4 _FoamColor;
            float _Roughness;
            float _Reflectance;
            float _NormalStrength;
            float _SubsurfaceStrength;
            float _EnvironmentReflectionStrength;
            half4 _TransmissionColor;
            float _TransmissionStrength;
            float _FoamStrength;
            float _CrestFoamStrength;
            float _FoamBrightness;
            int _CascadeCount;
            float4 _Cascade0Scale;
            float4 _Cascade1Scale;
            float4 _Cascade2Scale;
            float4 _Cascade3Scale;
            float4 _Cascade4Scale;
            float4 _Cascade5Scale;
            float4 _Cascade6Scale;
            float4 _Cascade7Scale;
            float _Cascade0Blend;
            float _Cascade1Blend;
            float _Cascade2Blend;
            float _Cascade3Blend;
            float _Cascade4Blend;
            float _Cascade5Blend;
            float _Cascade6Blend;
            float _Cascade7Blend;
            float _DisplacementFadeStart;
            float _DisplacementFadeRate;
            float _FoamFadeRate;
            float _NormalFadeRate;
            float _DistantNormalStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 waterUV : TEXCOORD1;
            float waveHeight : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float4 GetCascadeScale(int index)
        {
            switch (index)
            {
                case 0: return _Cascade0Scale;
                case 1: return _Cascade1Scale;
                case 2: return _Cascade2Scale;
                case 3: return _Cascade3Scale;
                case 4: return _Cascade4Scale;
                case 5: return _Cascade5Scale;
                case 6: return _Cascade6Scale;
                default: return _Cascade7Scale;
            }
        }

        float GetCascadeBlend(int index)
        {
            switch (index)
            {
                case 0: return _Cascade0Blend;
                case 1: return _Cascade1Blend;
                case 2: return _Cascade2Blend;
                case 3: return _Cascade3Blend;
                case 4: return _Cascade4Blend;
                case 5: return _Cascade5Blend;
                case 6: return _Cascade6Blend;
                default: return _Cascade7Blend;
            }
        }

        float3 SampleDisplacement(float2 waterUV)
        {
            float3 displacement = 0.0;
            int cascadeCount = clamp(_CascadeCount, 0, MAX_CASCADES);

            [loop]
            for (int cascade = 0; cascade < cascadeCount; ++cascade)
            {
                float4 scale = GetCascadeScale(cascade);
                float3 previousDisplacement = SAMPLE_TEXTURE2D_ARRAY_LOD(
                    _PreviousDisplacementArray,
                    sampler_PreviousDisplacementArray,
                    waterUV * scale.xy,
                    cascade,
                    0.0).xyz;
                float3 currentDisplacement = SAMPLE_TEXTURE2D_ARRAY_LOD(
                    _DisplacementArray,
                    sampler_DisplacementArray,
                    waterUV * scale.xy,
                    cascade,
                    0.0).xyz;
                displacement += lerp(
                    previousDisplacement,
                    currentDisplacement,
                    GetCascadeBlend(cascade)) * scale.z;
            }
            return displacement;
        }

        float4 CubicWeights(float value)
        {
            float value2 = value * value;
            float value3 = value2 * value;
            return float4(
                -value3 + 3.0 * value2 - 3.0 * value + 1.0,
                3.0 * value3 - 6.0 * value2 + 4.0,
                -3.0 * value3 + 3.0 * value2 + 3.0 * value + 1.0,
                value3) / 6.0;
        }

        float HashNoise(float2 value)
        {
            value = frac(value * float2(123.34, 456.21));
            value += dot(value, value + 45.32);
            return frac(value.x * value.y);
        }

        float ValueNoise(float2 value)
        {
            float2 cell = floor(value);
            float2 fractional = frac(value);
            fractional = fractional * fractional * (3.0 - 2.0 * fractional);
            float bottom = lerp(
                HashNoise(cell),
                HashNoise(cell + float2(1.0, 0.0)),
                fractional.x);
            float top = lerp(
                HashNoise(cell + float2(0.0, 1.0)),
                HashNoise(cell + 1.0),
                fractional.x);
            return lerp(bottom, top, fractional.y);
        }

        float MicroWaveHeight(float2 waterUV)
        {
            const float2 wind = float2(0.9397, 0.3420);
            float2 p = waterUV + wind * _Time.y * 0.55;
            float2 rotated = float2(
                p.x * 0.766 - p.y * 0.643,
                p.x * 0.643 + p.y * 0.766);
            return ValueNoise(p * 0.34) * 0.48
                + ValueNoise(rotated * 0.92 + 31.4) * 0.22
                + ValueNoise(p * 2.35 - 17.8) * 0.075;
        }

        float2 MicroWaveGradient(float2 waterUV)
        {
            // A few inexpensive directional bands restore the capillary-scale
            // normal detail visible in the reference without changing the FFT
            // silhouette. Slightly different headings avoid a tiled corduroy
            // pattern while keeping the dominant wind direction readable.
            float time = _Time.y;
            const float2 d0 = float2(0.9397, 0.3420);
            const float2 d1 = float2(0.8192, 0.5736);
            const float2 d2 = float2(0.9848, 0.1736);
            const float2 d3 = float2(0.6428, 0.7660);
            float modulation = lerp(0.55, 1.0, ValueNoise(waterUV * 0.12));
            float2 gradient = d0 * cos(dot(waterUV, d0) * 1.35 + time * 1.1) * 0.13;
            gradient += d1 * cos(dot(waterUV, d1) * 2.4 + time * 1.55) * 0.09;
            gradient += d2 * cos(dot(waterUV, d2) * 4.1 + time * 2.1) * 0.055;
            gradient += d3 * cos(dot(waterUV, d3) * 6.8 + time * 2.8) * 0.03;
            const float epsilon = 0.12;
            float2 noiseGradient = float2(
                MicroWaveHeight(waterUV + float2(epsilon, 0.0))
                    - MicroWaveHeight(waterUV - float2(epsilon, 0.0)),
                MicroWaveHeight(waterUV + float2(0.0, epsilon))
                    - MicroWaveHeight(waterUV - float2(0.0, epsilon)))
                / (2.0 * epsilon);
            return gradient * modulation + noiseGradient;
        }

        // Four bilinear taps approximate bicubic B-spline filtering.
        float4 SampleNormalFoamBicubic(float2 uv, int layer, float2 dimensions)
        {
            float2 inverseDimensions = rcp(dimensions);
            float2 texelPosition = uv * dimensions + 0.5;
            float2 fractional = frac(texelPosition);
            float4 weightX = CubicWeights(fractional.x);
            float4 weightY = CubicWeights(fractional.y);
            float4 pairWeights = float4(
                weightX.x + weightX.y,
                weightX.z + weightX.w,
                weightY.x + weightY.y,
                weightY.z + weightY.w);
            float4 samplePosition =
                (float4(weightX.y, weightX.w, weightY.y, weightY.w) / pairWeights
                + float4(-1.5, 0.5, -1.5, 0.5)
                + floor(texelPosition).xxyy) * inverseDimensions.xxyy;
            float2 blend = pairWeights.xz / (pairWeights.xz + pairWeights.yw);

            float4 bottomLeft = SAMPLE_TEXTURE2D_ARRAY(
                _NormalFoamArray, sampler_NormalFoamArray, samplePosition.yw, layer);
            float4 bottomRight = SAMPLE_TEXTURE2D_ARRAY(
                _NormalFoamArray, sampler_NormalFoamArray, samplePosition.xw, layer);
            float4 topLeft = SAMPLE_TEXTURE2D_ARRAY(
                _NormalFoamArray, sampler_NormalFoamArray, samplePosition.yz, layer);
            float4 topRight = SAMPLE_TEXTURE2D_ARRAY(
                _NormalFoamArray, sampler_NormalFoamArray, samplePosition.xz, layer);

            return lerp(
                lerp(bottomLeft, bottomRight, blend.x),
                lerp(topLeft, topRight, blend.x),
                blend.y);
        }

        float4 SamplePreviousNormalFoamBicubic(float2 uv, int layer, float2 dimensions)
        {
            float2 inverseDimensions = rcp(dimensions);
            float2 texelPosition = uv * dimensions + 0.5;
            float2 fractional = frac(texelPosition);
            float4 weightX = CubicWeights(fractional.x);
            float4 weightY = CubicWeights(fractional.y);
            float4 pairWeights = float4(
                weightX.x + weightX.y,
                weightX.z + weightX.w,
                weightY.x + weightY.y,
                weightY.z + weightY.w);
            float4 samplePosition =
                (float4(weightX.y, weightX.w, weightY.y, weightY.w) / pairWeights
                + float4(-1.5, 0.5, -1.5, 0.5)
                + floor(texelPosition).xxyy) * inverseDimensions.xxyy;
            float2 blend = pairWeights.xz / (pairWeights.xz + pairWeights.yw);

            float4 bottomLeft = SAMPLE_TEXTURE2D_ARRAY(
                _PreviousNormalFoamArray, sampler_PreviousNormalFoamArray, samplePosition.yw, layer);
            float4 bottomRight = SAMPLE_TEXTURE2D_ARRAY(
                _PreviousNormalFoamArray, sampler_PreviousNormalFoamArray, samplePosition.xw, layer);
            float4 topLeft = SAMPLE_TEXTURE2D_ARRAY(
                _PreviousNormalFoamArray, sampler_PreviousNormalFoamArray, samplePosition.yz, layer);
            float4 topRight = SAMPLE_TEXTURE2D_ARRAY(
                _PreviousNormalFoamArray, sampler_PreviousNormalFoamArray, samplePosition.xz, layer);

            return lerp(
                lerp(bottomLeft, bottomRight, blend.x),
                lerp(topLeft, topRight, blend.x),
                blend.y);
        }

        float3 SampleGradientAndFoam(float2 waterUV)
        {
            uint width;
            uint height;
            uint layers;
            uint mipCount;
            _NormalFoamArray.GetDimensions(0, width, height, layers, mipCount);

            float2 dimensions = max(float2(width, height), 1.0);
            float3 gradientAndFoam = 0.0;
            int cascadeCount = min(clamp(_CascadeCount, 0, MAX_CASCADES), (int)layers);

            [loop]
            for (int cascade = 0; cascade < cascadeCount; ++cascade)
            {
                float4 scale = GetCascadeScale(cascade);
                float2 uv = waterUV * scale.xy;
                float pixelsPerMeter = dimensions.x * min(scale.x, scale.y);
                float bilinearWeight = saturate(pixelsPerMeter * 0.1);
                float4 bicubic = SampleNormalFoamBicubic(uv, cascade, dimensions);
                float4 bilinear = SAMPLE_TEXTURE2D_ARRAY(
                    _NormalFoamArray, sampler_NormalFoamArray, uv, cascade);
                float4 currentPacked = lerp(bicubic, bilinear, bilinearWeight);
                float4 previousBicubic = SamplePreviousNormalFoamBicubic(
                    uv, cascade, dimensions);
                float4 previousBilinear = SAMPLE_TEXTURE2D_ARRAY(
                    _PreviousNormalFoamArray,
                    sampler_PreviousNormalFoamArray,
                    uv,
                    cascade);
                float4 previousPacked = lerp(
                    previousBicubic, previousBilinear, bilinearWeight);
                float4 packed = lerp(
                    previousPacked,
                    currentPacked,
                    GetCascadeBlend(cascade));
                gradientAndFoam += packed.xyw * float3(scale.ww, 1.0);
            }
            return gradientAndFoam;
        }

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float2 waterUV = positionWS.xz;
            float3 displacement = SampleDisplacement(waterUV);
            float horizontalDistance = distance(positionWS.xz, _WorldSpaceCameraPos.xz);
            float displacementFade = min(
                exp(-(horizontalDistance - _DisplacementFadeStart) * _DisplacementFadeRate),
                1.0);

            positionWS += displacement * displacementFade;
            output.positionWS = positionWS;
            output.waterUV = waterUV;
            output.waveHeight = displacement.y;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        float SmithMaskingShadowing(float cosine, float roughness)
        {
            cosine = saturate(cosine);
            float sineSquared = max(1.0 - cosine * cosine, 1e-5);
            float a = cosine / (max(roughness, 1e-3) * sqrt(sineSquared));
            float aSquared = a * a;
            return a < 1.6
                ? (1.0 - 1.259 * a + 0.396 * aSquared) / (3.535 * a + 2.181 * aSquared)
                : 0.0;
        }

        float GgxDistribution(float cosine, float roughness)
        {
            float alphaSquared = roughness * roughness;
            float denominator = 1.0 + (alphaSquared - 1.0) * cosine * cosine;
            return alphaSquared / max(PI * denominator * denominator, 1e-5);
        }

        float FresnelFactor(float normalDotView)
        {
            float exponent = 5.0 * exp(-2.69 * _Roughness);
            float grazing = pow(1.0 - saturate(normalDotView), exponent)
                / (1.0 + 22.7 * pow(_Roughness, 1.5));
            return lerp(grazing, 1.0, _Reflectance);
        }

        half3 EvaluateOceanLight(
            Light light,
            float3 normalWS,
            float3 viewDirectionWS,
            half3 albedo,
            float waveHeight,
            float foamFactor,
            float fresnel)
        {
            float3 lightDirectionWS = light.direction;
            float3 halfwayDirection = SafeNormalize(lightDirectionWS + viewDirectionWS);
            float normalDotLight = max(dot(normalWS, lightDirectionWS), 2e-5);
            float normalDotView = max(dot(normalWS, viewDirectionWS), 2e-5);
            float attenuation = light.distanceAttenuation * light.shadowAttenuation;

            // Preserve the source shader's argument order. Although unusual,
            // this is part of its characteristic broad ocean highlights.
            float lightMask = SmithMaskingShadowing(_Roughness, normalDotView);
            float viewMask = SmithMaskingShadowing(_Roughness, normalDotLight);
            float distribution = GgxDistribution(saturate(dot(normalWS, halfwayDirection)), _Roughness);
            float geometry = rcp(1.0 + lightMask + viewMask);
            float specular = fresnel * distribution * geometry / (4.0 * normalDotView + 0.1);

            const half3 subsurfaceTint = half3(0.9, 1.15, 0.85);
            float backScatter = pow(saturate(dot(lightDirectionWS, -viewDirectionWS)), 4.0);
            float crestScatter = pow(0.5 - 0.5 * dot(lightDirectionWS, normalWS), 3.0);
            float subsurfaceHeight = _SubsurfaceStrength
                * max(0.0, waveHeight + 2.5)
                * backScatter
                * crestScatter;
            float subsurfaceNear = 0.5 * normalDotView * normalDotView;
            float lambert = 0.5 * normalDotLight;
            // Godot applies ALBEDO to the light function's diffuse result.
            // Apply that multiplication explicitly in this hand-written URP pass.
            half3 waterResponse = lambert
                + (subsurfaceHeight + subsurfaceNear) * subsurfaceTint / (1.0 + lightMask);
            // Godot applies ALBEDO after the custom light response. Preserve
            // that order so subsurface illumination retains the water tint.
            half3 diffuseResponse = lerp(waterResponse, _FoamColor.rgb, foamFactor);
            half3 diffuse = albedo * diffuseResponse * (1.0 - fresnel);

            return (diffuse + specular) * light.color * attenuation;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float horizontalDistance = distance(input.positionWS.xz, _WorldSpaceCameraPos.xz);
            float3 gradientAndFoam = SampleGradientAndFoam(input.waterUV);
            float foamDistanceFade = exp(-horizontalDistance * _FoamFadeRate);
            float accumulatedFoam = smoothstep(
                0.0,
                1.0,
                gradientAndFoam.z * _FoamStrength);
            float foamFactor = accumulatedFoam * foamDistanceFade;
            half3 albedo = lerp(_WaterColor.rgb, _FoamColor.rgb, foamFactor);

            float normalStrength = lerp(
                _DistantNormalStrength,
                _NormalStrength,
                exp(-horizontalDistance * _NormalFadeRate));
            float2 surfaceGradient = gradientAndFoam.xy;
            float3 normalWS = normalize(float3(
                -surfaceGradient.x * normalStrength,
                1.0,
                -surfaceGradient.y * normalStrength));
            float3 viewDirectionWS = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
            float normalDotView = saturate(dot(normalWS, viewDirectionWS));
            float fresnel = FresnelFactor(normalDotView);

            half3 color = SampleSH(normalWS) * albedo * (1.0 - fresnel);
            float3 reflectionDirectionWS = reflect(-viewDirectionWS, normalWS);
            float environmentRoughness = saturate(
                0.4 + (1.0 - fresnel) * foamFactor);
            half3 environmentReflection = GlossyEnvironmentReflection(
                reflectionDirectionWS,
                input.positionWS,
                environmentRoughness,
                1.0h);
            color += environmentReflection
                * (fresnel + _Reflectance * 0.8)
                * _EnvironmentReflectionStrength;

            // Godot's environment and custom light path keep a readable
            // blue-green body tone even when the low sun does not directly
            // illuminate a trough. Reconstruct that in-scattering explicitly
            // because URP's hand-written pass does not supply it for us.
            float heightScatter = smoothstep(-2.5, 2.5, input.waveHeight);
            float bodyScatter = _SubsurfaceStrength
                * lerp(0.18, 0.58, heightScatter)
                * (1.0 - fresnel)
                * (1.0 - foamFactor);
            color += _WaterColor.rgb * bodyScatter;

            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            color += EvaluateOceanLight(
                mainLight, normalWS, viewDirectionWS, albedo,
                input.waveHeight, foamFactor, fresnel);

            #if defined(_ADDITIONAL_LIGHTS)
            uint additionalLightCount = GetAdditionalLightsCount();
            [loop]
            for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
            {
                Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                color += EvaluateOceanLight(
                    additionalLight, normalWS, viewDirectionWS, albedo,
                    input.waveHeight, foamFactor, fresnel);
            }
            #endif

            // The source's custom light path leaves white water strongly
            // sky-lit. URP otherwise darkens foam twice (albedo and diffuse),
            // so restore that diffuse skylight without adding synthetic foam.
            half3 foamSkylight = _FoamColor.rgb
                * (0.52 + 0.28 * saturate(normalWS.y));
            color = lerp(color, max(color, foamSkylight), foamFactor * 0.82);

            color = MixFog(color, input.fogFactor);
            return half4(color, 1.0);
        }

        half4 DepthOnlyFragment(Varyings input) : SV_Target
        {
            return 0.0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    Fallback Off
}

