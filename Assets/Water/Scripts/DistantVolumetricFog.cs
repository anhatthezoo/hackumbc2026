using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class DistantVolumetricFog : MonoBehaviour
{
    [SerializeField] private Material _fogMaterial;
    [SerializeField] private Color _fogColor = new(0.53f, 0.73f, 0.92f, 1f);
    [SerializeField, Range(0f, 1f)] private float _density = 0.24f;
    [SerializeField, Min(10f)] private float _startDistance = 88f;
    [SerializeField, Min(10f)] private float _depthSpacing = 60f;
    [SerializeField, Range(2, 6)] private int _layerCount = 5;
    [SerializeField, Range(0.1f, 1f)] private float _bandHeight = 0.64f;
    [SerializeField, Min(0.1f)] private float _noiseScale = 4.2f;
    [SerializeField] private Vector2 _noiseSpeed = new(0.012f, 0.004f);

    private readonly List<GameObject> _layers = new();
    private readonly List<Material> _materials = new();
    private Camera _camera;
    private bool _needsRebuild = true;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _needsRebuild = true;
    }

    private void OnValidate()
    {
        _needsRebuild = true;
    }

    private void LateUpdate()
    {
        if (_needsRebuild || _layers.Count != _layerCount)
            Rebuild();

        UpdateLayerTransforms();
    }

    private void OnDisable()
    {
        ClearLayers();
    }

    private void Rebuild()
    {
        _needsRebuild = false;
        ClearLayers();

        if (_fogMaterial == null)
            return;

        if (_camera == null)
            _camera = GetComponent<Camera>();

        for (int index = 0; index < _layerCount; index++)
        {
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            layer.name = $"Distant Fog Layer {index + 1}";
            layer.hideFlags = HideFlags.HideAndDontSave;
            layer.transform.SetParent(transform, false);

            Collider collider = layer.GetComponent<Collider>();
            if (collider != null)
                DestroyObject(collider);

            Material material = new(_fogMaterial)
            {
                name = $"Distant Fog Layer {index + 1}",
                hideFlags = HideFlags.HideAndDontSave
            };

            float layerFraction = _layerCount <= 1 ? 0f : index / (float)(_layerCount - 1);
            material.SetColor("_Color", Color.Lerp(_fogColor, Color.white, layerFraction * 0.08f));
            material.SetFloat("_Density", _density * Mathf.Lerp(1f, 0.58f, layerFraction));
            material.SetFloat("_BandHeight", _bandHeight * Mathf.Lerp(1f, 1.3f, layerFraction));
            material.SetFloat("_NoiseScale", _noiseScale * Mathf.Lerp(1f, 0.72f, layerFraction));
            material.SetVector("_NoiseSpeed", _noiseSpeed * Mathf.Lerp(1f, 0.65f, layerFraction));
            material.SetFloat("_LayerPhase", index * 3.71f);

            MeshRenderer renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            _layers.Add(layer);
            _materials.Add(material);
        }

        UpdateLayerTransforms();
    }

    private void UpdateLayerTransforms()
    {
        if (_camera == null)
            return;

        float verticalHalfAngle = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        for (int index = 0; index < _layers.Count; index++)
        {
            float distance = _startDistance + _depthSpacing * index;
            float screenHeight = 2f * distance * Mathf.Tan(verticalHalfAngle);
            Transform layer = _layers[index].transform;
            layer.localPosition = new Vector3(0f, -screenHeight * 0.02f, distance);
            layer.localRotation = Quaternion.identity;
            layer.localScale = new Vector3(screenHeight * _camera.aspect * 1.08f, screenHeight * 0.7f, 1f);
        }
    }

    private void ClearLayers()
    {
        foreach (GameObject layer in _layers)
        {
            if (layer != null)
                DestroyObject(layer);
        }
        _layers.Clear();

        foreach (Material material in _materials)
        {
            if (material != null)
                DestroyObject(material);
        }
        _materials.Clear();
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
