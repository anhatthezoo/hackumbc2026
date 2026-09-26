// Adapted from GodotOceanWaves by Ethan Truong (MIT).
// See Assets/Water/LICENSE-GodotOceanWaves.txt.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class OceanWaveGenerator : MonoBehaviour
{
    private const int SpectrumCount = 4;
    private const int MaxCascades = 8;
    private const float Gravity = 9.81f;

    public enum QualityPreset
    {
        Balanced512,
        High1024
    }

    [Serializable]
    public sealed class CascadeSettings
    {
        [Min(1f)] public Vector2 tileLength = new Vector2(88f, 88f);
        [Range(0f, 2f)] public float displacementScale = 1f;
        [Range(0f, 2f)] public float normalScale = 1f;
        [Min(0.0001f)] public float windSpeed = 10f;
        [Range(-360f, 360f)] public float windDirection = 20f;
        [Min(0.0001f)] public float fetchLengthKm = 150f;
        [Range(0f, 2f)] public float swell = 0.8f;
        [Range(0f, 1f)] public float spread = 0.2f;
        [Range(0f, 1f)] public float detail = 1f;
        [Range(0f, 2f)] public float whitecap = 0.5f;
        [Range(0f, 10f)] public float foamAmount = 8f;
        public Vector2Int seed = new Vector2Int(1234, -7251);

        [NonSerialized] public float simulationTime;
    }

    [Header("Resources")]
    [SerializeField] private ComputeShader _compute;
    [SerializeField] private MeshRenderer _waterRenderer;

    [Header("Simulation")]
    [SerializeField] private QualityPreset _qualityPreset = QualityPreset.Balanced512;
    [SerializeField, Range(1f, 60f)] private float _updatesPerSecond = 30f;
    [SerializeField, Min(0.1f)] private float _waterDepth = 20f;
    [SerializeField] private CascadeSettings[] _cascades = CreateDefaultCascades();

    private RenderTexture _initialSpectrum;
    private RenderTexture _displacement;
    private RenderTexture _normalFoam;
    private RenderTexture _previousDisplacement;
    private RenderTexture _previousNormalFoam;
    private ComputeBuffer _fftBuffer;
    private MaterialPropertyBlock _propertyBlock;
    private readonly List<MeshRenderer> _waterRenderers = new List<MeshRenderer>();
    private double[] _cascadeBlendStartTimes;
    private double[] _cascadeBlendDurations;
    private bool _rendererCacheDirty = true;

    private int _clearKernel;
    private int _generateSpectrumKernel;
    private int _updateSpectrumKernel;
    private int _inverseFftRowsKernel;
    private int _transposeKernel;
    private int _unpackKernel;

    private int _nextCascade;
    private double _lastTickTime;
    private bool _needsRebuild = true;
    private bool _resourcesReady;

    private static readonly int DisplacementArrayId = Shader.PropertyToID("_DisplacementArray");
    private static readonly int NormalFoamArrayId = Shader.PropertyToID("_NormalFoamArray");
    private static readonly int PreviousDisplacementArrayId =
        Shader.PropertyToID("_PreviousDisplacementArray");
    private static readonly int PreviousNormalFoamArrayId =
        Shader.PropertyToID("_PreviousNormalFoamArray");
    private static readonly int CascadeCountId = Shader.PropertyToID("_CascadeCount");

    private static readonly int[] CascadeScaleIds =
    {
        Shader.PropertyToID("_Cascade0Scale"),
        Shader.PropertyToID("_Cascade1Scale"),
        Shader.PropertyToID("_Cascade2Scale"),
        Shader.PropertyToID("_Cascade3Scale"),
        Shader.PropertyToID("_Cascade4Scale"),
        Shader.PropertyToID("_Cascade5Scale"),
        Shader.PropertyToID("_Cascade6Scale"),
        Shader.PropertyToID("_Cascade7Scale")
    };

    private static readonly int[] CascadeBlendIds =
    {
        Shader.PropertyToID("_Cascade0Blend"),
        Shader.PropertyToID("_Cascade1Blend"),
        Shader.PropertyToID("_Cascade2Blend"),
        Shader.PropertyToID("_Cascade3Blend"),
        Shader.PropertyToID("_Cascade4Blend"),
        Shader.PropertyToID("_Cascade5Blend"),
        Shader.PropertyToID("_Cascade6Blend"),
        Shader.PropertyToID("_Cascade7Blend")
    };

    public QualityPreset Quality => _qualityPreset;
    public int SimulationResolution => MapSize;

    public void SetQuality(QualityPreset qualityPreset)
    {
        if (_qualityPreset == qualityPreset)
        {
            return;
        }

        _qualityPreset = qualityPreset;
        _needsRebuild = true;
    }

    private int MapSize => _qualityPreset == QualityPreset.High1024 ? 1024 : 512;

    private int QualityCascadeLimit => _qualityPreset == QualityPreset.High1024 ? MaxCascades : 4;

    private static CascadeSettings[] CreateDefaultCascades()
    {
        return new[]
        {
            new CascadeSettings
            {
                tileLength = new Vector2(88f, 88f),
                displacementScale = 1f,
                normalScale = 1f,
                windSpeed = 10f,
                windDirection = 20f,
                fetchLengthKm = 150f,
                swell = 0.8f,
                spread = 0.2f,
                detail = 1f,
                whitecap = 0.5f,
                foamAmount = 8f,
                seed = new Vector2Int(1234, -7251)
            },
            new CascadeSettings
            {
                tileLength = new Vector2(57f, 57f),
                displacementScale = 0.75f,
                normalScale = 1f,
                windSpeed = 5f,
                windDirection = 15f,
                fetchLengthKm = 150f,
                swell = 0.8f,
                spread = 0.4f,
                detail = 1f,
                whitecap = 0.5f,
                foamAmount = 0f,
                seed = new Vector2Int(-3821, 991)
            },
            new CascadeSettings
            {
                tileLength = new Vector2(16f, 16f),
                displacementScale = 0f,
                normalScale = 0.25f,
                windSpeed = 20f,
                windDirection = 20f,
                fetchLengthKm = 550f,
                swell = 0.8f,
                spread = 0.4f,
                detail = 1f,
                whitecap = 0.25f,
                foamAmount = 3f,
                seed = new Vector2Int(6229, 4103)
            },
            new CascadeSettings
            {
                tileLength = new Vector2(8f, 8f),
                displacementScale = 0f,
                normalScale = 0.16f,
                windSpeed = 12f,
                windDirection = 18f,
                fetchLengthKm = 80f,
                swell = 0.6f,
                spread = 0.5f,
                detail = 1f,
                whitecap = 0.25f,
                foamAmount = 1.5f,
                seed = new Vector2Int(-8147, 2551)
            },
            new CascadeSettings
            {
                tileLength = new Vector2(4f, 4f),
                displacementScale = 0f,
                normalScale = 0.08f,
                windSpeed = 8f,
                windDirection = 25f,
                fetchLengthKm = 30f,
                swell = 0.4f,
                spread = 0.65f,
                detail = 1f,
                whitecap = 0.2f,
                foamAmount = 0.5f,
                seed = new Vector2Int(3419, -6073)
            }
        };
    }

    private void Reset()
    {
        _waterRenderer = GetComponent<MeshRenderer>();
#if UNITY_EDITOR
        _compute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Water/Shaders/OceanWaves.compute");
#endif
        _cascades = CreateDefaultCascades();
        _needsRebuild = true;
    }

    private void OnEnable()
    {
        if (_waterRenderer == null)
        {
            _waterRenderer = GetComponent<MeshRenderer>();
        }

        _needsRebuild = true;
        _lastTickTime = 0.0;
        _rendererCacheDirty = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        ClearRendererProperties();
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        _updatesPerSecond = Mathf.Max(1f, _updatesPerSecond);
        _waterDepth = Mathf.Max(0.1f, _waterDepth);

        if (_cascades == null || _cascades.Length == 0)
        {
            _cascades = CreateDefaultCascades();
        }

        if (_cascades.Length > MaxCascades)
        {
            Array.Resize(ref _cascades, MaxCascades);
        }

        foreach (CascadeSettings cascade in _cascades)
        {
            if (cascade == null)
            {
                continue;
            }

            cascade.tileLength.x = Mathf.Max(1f, cascade.tileLength.x);
            cascade.tileLength.y = Mathf.Max(1f, cascade.tileLength.y);
            cascade.windSpeed = Mathf.Max(0.0001f, cascade.windSpeed);
            cascade.fetchLengthKm = Mathf.Max(0.0001f, cascade.fetchLengthKm);
        }

        _needsRebuild = true;
        _rendererCacheDirty = true;
    }

    private void OnTransformChildrenChanged()
    {
        _rendererCacheDirty = true;
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Tick(Time.realtimeSinceStartupAsDouble);
        }
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled)
        {
            return;
        }

        Tick(UnityEditor.EditorApplication.timeSinceStartup);
    }
#endif

    private void Tick(double currentTime)
    {
        if (_compute == null || _waterRenderer == null || !SystemInfo.supportsComputeShaders)
        {
            return;
        }

        if (_needsRebuild || !_resourcesReady)
        {
            RebuildResources();
            _lastTickTime = currentTime;
            ApplyInterpolationProperties(currentTime);
            return;
        }

        double interval = 1.0 / _updatesPerSecond;
        if (currentTime - _lastTickTime < interval)
        {
            ApplyInterpolationProperties(currentTime);
            return;
        }

        float deltaTime = Mathf.Min((float)(currentTime - _lastTickTime), 0.25f);
        _lastTickTime = currentTime;

        int cascadeCount = ActiveCascadeCount;
        if (cascadeCount == 0)
        {
            return;
        }

        double previousUpdateTime = _cascadeBlendStartTimes[_nextCascade];
        if (previousUpdateTime > 0.0)
        {
            _cascadeBlendDurations[_nextCascade] = Math.Max(
                currentTime - previousUpdateTime,
                1.0 / 240.0);
        }

        CopyCurrentToPrevious(_nextCascade);
        UpdateCascade(_nextCascade, deltaTime * cascadeCount);
        _cascadeBlendStartTimes[_nextCascade] = currentTime;
        _nextCascade = (_nextCascade + 1) % cascadeCount;
        ApplyInterpolationProperties(currentTime);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

    private int ActiveCascadeCount
    {
        get
        {
            return _cascades == null
                ? 0
                : Mathf.Clamp(_cascades.Length, 0, QualityCascadeLimit);
        }
    }

    private void RebuildResources()
    {
        ReleaseResources();

        ConfigureComputeQualityKeyword();

        int cascadeCount = ActiveCascadeCount;
        if (cascadeCount == 0)
        {
            ApplyRendererProperties();
            _needsRebuild = false;
            return;
        }

        _clearKernel = _compute.FindKernel("ClearOutputs");
        _generateSpectrumKernel = _compute.FindKernel("GenerateSpectrum");
        _updateSpectrumKernel = _compute.FindKernel("UpdateSpectrum");
        _inverseFftRowsKernel = _compute.FindKernel("InverseFftRows");
        _transposeKernel = _compute.FindKernel("Transpose");
        _unpackKernel = _compute.FindKernel("Unpack");

        _initialSpectrum = CreateTextureArray(
            "Ocean Initial Spectrum",
            GraphicsFormat.R32G32B32A32_SFloat,
            cascadeCount);
        _displacement = CreateTextureArray(
            "Ocean Displacement",
            GraphicsFormat.R16G16B16A16_SFloat,
            cascadeCount);
        _normalFoam = CreateTextureArray(
            "Ocean Normal Foam",
            GraphicsFormat.R16G16B16A16_SFloat,
            cascadeCount);
        _previousDisplacement = CreateTextureArray(
            "Ocean Previous Displacement",
            GraphicsFormat.R16G16B16A16_SFloat,
            cascadeCount);
        _previousNormalFoam = CreateTextureArray(
            "Ocean Previous Normal Foam",
            GraphicsFormat.R16G16B16A16_SFloat,
            cascadeCount);
        _cascadeBlendStartTimes = new double[cascadeCount];
        _cascadeBlendDurations = new double[cascadeCount];

        // Cascades update sequentially, so one FFT workspace can be reused by
        // every cascade instead of reserving a full workspace for each one.
        int complexValueCount = MapSize
            * MapSize
            * SpectrumCount
            * 2;
        _fftBuffer = new ComputeBuffer(
            complexValueCount,
            sizeof(float) * 2,
            ComputeBufferType.Structured);

        BindComputeResources();
        ClearOutputs();

        for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
        {
            CascadeSettings cascade = _cascades[cascadeIndex];
            cascade.simulationTime = 120f + Mathf.PI * cascadeIndex;
            GenerateSpectrum(cascadeIndex);
            UpdateCascade(cascadeIndex, 1f / _updatesPerSecond);
            CopyCurrentToPrevious(cascadeIndex);
            _cascadeBlendDurations[cascadeIndex] = cascadeCount / _updatesPerSecond;
        }

        _nextCascade = 0;
        _resourcesReady = true;
        _needsRebuild = false;
        ApplyRendererProperties();
    }

    private void ConfigureComputeQualityKeyword()
    {
        _compute.DisableKeyword("OCEAN_MAP_SIZE_512");
        _compute.DisableKeyword("OCEAN_MAP_SIZE_1024");
        _compute.EnableKeyword(
            _qualityPreset == QualityPreset.High1024
                ? "OCEAN_MAP_SIZE_1024"
                : "OCEAN_MAP_SIZE_512");
    }

    private RenderTexture CreateTextureArray(
        string textureName,
        GraphicsFormat format,
        int layerCount)
    {
        var descriptor = new RenderTextureDescriptor(
            MapSize,
            MapSize,
            format,
            0)
        {
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = layerCount,
            msaaSamples = 1,
            enableRandomWrite = true,
            useMipMap = false,
            autoGenerateMips = false
        };

        var texture = new RenderTexture(descriptor)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.Create();
        return texture;
    }

    private void BindComputeResources()
    {
        _compute.SetInt("_CascadeCount", ActiveCascadeCount);

        _compute.SetTexture(_clearKernel, "_DisplacementArray", _displacement);
        _compute.SetTexture(_clearKernel, "_NormalFoamArray", _normalFoam);

        _compute.SetTexture(
            _generateSpectrumKernel,
            "_InitialSpectrum",
            _initialSpectrum);

        _compute.SetTexture(
            _updateSpectrumKernel,
            "_InitialSpectrum",
            _initialSpectrum);
        _compute.SetBuffer(
            _updateSpectrumKernel,
            "_FftBuffer",
            _fftBuffer);

        _compute.SetBuffer(
            _inverseFftRowsKernel,
            "_FftBuffer",
            _fftBuffer);
        _compute.SetBuffer(
            _transposeKernel,
            "_FftBuffer",
            _fftBuffer);

        _compute.SetBuffer(
            _unpackKernel,
            "_FftBuffer",
            _fftBuffer);
        _compute.SetTexture(
            _unpackKernel,
            "_DisplacementArray",
            _displacement);
        _compute.SetTexture(
            _unpackKernel,
            "_NormalFoamArray",
            _normalFoam);
    }

    private void ClearOutputs()
    {
        _compute.Dispatch(
            _clearKernel,
            MapSize / 8,
            MapSize / 8,
            ActiveCascadeCount);
    }

    private void GenerateSpectrum(int cascadeIndex)
    {
        CascadeSettings cascade = _cascades[cascadeIndex];
        float fetchLength = cascade.fetchLengthKm * 1000f;
        float alpha = 0.076f * Mathf.Pow(
            cascade.windSpeed * cascade.windSpeed
                / (fetchLength * Gravity),
            0.22f);
        float peakFrequency = 22f * Mathf.Pow(
            Gravity * Gravity / (cascade.windSpeed * fetchLength),
            1f / 3f);

        _compute.SetInt("_CascadeIndex", cascadeIndex);
        _compute.SetInts(
            "_SpectrumSeed",
            cascade.seed.x,
            cascade.seed.y);
        _compute.SetVector(
            "_TileLength",
            new Vector4(cascade.tileLength.x, cascade.tileLength.y, 0f, 0f));
        _compute.SetFloat("_Alpha", alpha);
        _compute.SetFloat("_PeakFrequency", peakFrequency);
        _compute.SetFloat("_WindSpeed", cascade.windSpeed);
        _compute.SetFloat(
            "_WindAngle",
            cascade.windDirection * Mathf.Deg2Rad);
        _compute.SetFloat("_Depth", _waterDepth);
        _compute.SetFloat("_Swell", cascade.swell);
        _compute.SetFloat("_Detail", cascade.detail);
        _compute.SetFloat("_Spread", cascade.spread);
        _compute.Dispatch(
            _generateSpectrumKernel,
            MapSize / 8,
            MapSize / 8,
            1);
    }

    private void UpdateCascade(int cascadeIndex, float deltaTime)
    {
        // Compute shader reimports invalidate per-kernel resource bindings in the
        // Editor without necessarily rebuilding this component's GPU resources.
        BindComputeResources();

        CascadeSettings cascade = _cascades[cascadeIndex];
        cascade.simulationTime += deltaTime;

        _compute.SetInt("_CascadeIndex", cascadeIndex);
        _compute.SetVector(
            "_TileLength",
            new Vector4(cascade.tileLength.x, cascade.tileLength.y, 0f, 0f));
        _compute.SetFloat("_Depth", _waterDepth);
        _compute.SetFloat("_SimulationTime", cascade.simulationTime);
        _compute.SetFloat("_Whitecap", cascade.whitecap);
        _compute.SetFloat(
            "_FoamGrowRate",
            deltaTime * cascade.foamAmount * 7.5f);
        _compute.SetFloat(
            "_FoamDecayRate",
            deltaTime * Mathf.Max(0.5f, 10f - cascade.foamAmount) * 1.15f);

        _compute.Dispatch(
            _updateSpectrumKernel,
            MapSize / 8,
            MapSize / 8,
            1);
        _compute.Dispatch(
            _inverseFftRowsKernel,
            1,
            MapSize,
            SpectrumCount);
        _compute.Dispatch(
            _transposeKernel,
            MapSize / 16,
            MapSize / 16,
            SpectrumCount);
        _compute.Dispatch(
            _inverseFftRowsKernel,
            1,
            MapSize,
            SpectrumCount);
        _compute.Dispatch(
            _unpackKernel,
            MapSize / 8,
            MapSize / 8,
            1);
    }

    private void ApplyRendererProperties()
    {
        RefreshWaterRenderersIfNeeded();
        if (_waterRenderers.Count == 0)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        foreach (MeshRenderer renderer in _waterRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(DisplacementArrayId, _displacement);
            _propertyBlock.SetTexture(NormalFoamArrayId, _normalFoam);
            _propertyBlock.SetTexture(PreviousDisplacementArrayId, _previousDisplacement);
            _propertyBlock.SetTexture(PreviousNormalFoamArrayId, _previousNormalFoam);
            _propertyBlock.SetInt(CascadeCountId, _resourcesReady ? ActiveCascadeCount : 0);

            for (int cascadeIndex = 0; cascadeIndex < MaxCascades; ++cascadeIndex)
            {
                Vector4 scale = Vector4.zero;
                if (cascadeIndex < ActiveCascadeCount)
                {
                    CascadeSettings cascade = _cascades[cascadeIndex];
                    scale = new Vector4(
                        1f / cascade.tileLength.x,
                        1f / cascade.tileLength.y,
                        cascade.displacementScale,
                        cascade.normalScale);
                }

                _propertyBlock.SetVector(CascadeScaleIds[cascadeIndex], scale);
            }

            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void ApplyInterpolationProperties(double currentTime)
    {
        if (!_resourcesReady)
        {
            return;
        }

        RefreshWaterRenderersIfNeeded();
        _propertyBlock ??= new MaterialPropertyBlock();
        foreach (MeshRenderer renderer in _waterRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            for (int cascadeIndex = 0; cascadeIndex < MaxCascades; ++cascadeIndex)
            {
                float blend = 1f;
                if (cascadeIndex < ActiveCascadeCount
                    && _cascadeBlendStartTimes != null
                    && _cascadeBlendDurations != null)
                {
                    double duration = Math.Max(
                        _cascadeBlendDurations[cascadeIndex],
                        1.0 / 240.0);
                    blend = Mathf.Clamp01((float)(
                        (currentTime - _cascadeBlendStartTimes[cascadeIndex]) / duration));
                }

                _propertyBlock.SetFloat(CascadeBlendIds[cascadeIndex], blend);
            }

            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void RefreshWaterRenderersIfNeeded()
    {
        if (!_rendererCacheDirty)
        {
            return;
        }

        _waterRenderers.Clear();
        GetComponentsInChildren(true, _waterRenderers);
        if (_waterRenderer != null && !_waterRenderers.Contains(_waterRenderer))
        {
            _waterRenderers.Add(_waterRenderer);
        }

        _rendererCacheDirty = false;
    }

    private void CopyCurrentToPrevious(int cascadeIndex)
    {
        if (_displacement == null || _normalFoam == null
            || _previousDisplacement == null || _previousNormalFoam == null)
        {
            return;
        }

        Graphics.CopyTexture(
            _displacement, cascadeIndex, 0,
            _previousDisplacement, cascadeIndex, 0);
        Graphics.CopyTexture(
            _normalFoam, cascadeIndex, 0,
            _previousNormalFoam, cascadeIndex, 0);
    }

    private void ClearRendererProperties()
    {
        RefreshWaterRenderersIfNeeded();
        foreach (MeshRenderer renderer in _waterRenderers)
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
            }
        }
    }

    private void ReleaseResources()
    {
        _resourcesReady = false;

        if (_fftBuffer != null)
        {
            _fftBuffer.Dispose();
            _fftBuffer = null;
        }

        ReleaseTexture(ref _initialSpectrum);
        ReleaseTexture(ref _displacement);
        ReleaseTexture(ref _normalFoam);
        ReleaseTexture(ref _previousDisplacement);
        ReleaseTexture(ref _previousNormalFoam);
        _cascadeBlendStartTimes = null;
        _cascadeBlendDurations = null;
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }

        texture = null;
    }
}

