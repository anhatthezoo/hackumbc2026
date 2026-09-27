using System.Collections.Generic;
using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BurningOilSlickVisual : MonoBehaviour
    {
        private const int MaximumSlicks = 32;
        private static readonly int SlickCountId = Shader.PropertyToID("_OilSlickCount");
        private static readonly int SlickDataId = Shader.PropertyToID("_OilSlickData");
        private static readonly int SlickParamsId = Shader.PropertyToID("_OilSlickParams");
        private static readonly List<BurningOilSlickVisual> ActiveSlicks = new();
        private static readonly Vector4[] SlickData = new Vector4[MaximumSlicks];
        private static readonly Vector4[] SlickParams = new Vector4[MaximumSlicks];
        private static int lastUploadedFrame = -1;

        [Header("Oil Mask")]
        [SerializeField] private Vector2 footprint = new Vector2(36f, 60f);
        [SerializeField, Range(0.02f, 0.4f)] private float edgeSoftness = 0.13f;
        [SerializeField, Range(0f, 0.45f)] private float edgeNoise = 0.24f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.94f;
        [SerializeField] private float patternSeed = 2.71f;

        [Header("Fire")]
        [SerializeField] private GameObject firePrefab;
        [SerializeField, Range(3, 24)] private int fireClusterCount = 16;
        [SerializeField, Min(0f)] private float fireHeight = 0.55f;
        [SerializeField] private Vector2 fireScaleRange = new Vector2(1.25f, 2.1f);
        [SerializeField, Min(0f)] private float flickerSpeed = 8f;

        private GameObject fireRoot;
        private Light fireLight;

        public void Configure(Vector2 area, int clusterCount)
        {
            footprint = new Vector2(
                Mathf.Max(0.1f, area.x),
                Mathf.Max(0.1f, area.y));
            fireClusterCount = Mathf.Clamp(clusterCount, 3, 24);
            UploadMasks(true);

            if (Application.isPlaying)
            {
                BuildFire();
            }
        }

        private void OnEnable()
        {
            if (!ActiveSlicks.Contains(this))
            {
                ActiveSlicks.Add(this);
            }

            UploadMasks(true);

            if (Application.isPlaying)
            {
                BuildFire();
            }
        }

        private void OnDisable()
        {
            ActiveSlicks.Remove(this);
            UploadMasks(true);
            DestroyFire();
        }

        private void OnValidate()
        {
            footprint.x = Mathf.Max(0.1f, footprint.x);
            footprint.y = Mathf.Max(0.1f, footprint.y);
            fireScaleRange.x = Mathf.Max(0.05f, fireScaleRange.x);
            fireScaleRange.y = Mathf.Max(fireScaleRange.x, fireScaleRange.y);
            UploadMasks(true);
        }

        private void Update()
        {
            UploadMasks();

            if (!Application.isPlaying)
            {
                return;
            }

            if (fireRoot == null)
            {
                BuildFire();
            }

            AnimateFire();
        }

        private static void UploadMasks(bool force = false)
        {
            if (!force && lastUploadedFrame == Time.frameCount)
            {
                return;
            }

            lastUploadedFrame = Time.frameCount;
            int count = 0;
            for (int index = ActiveSlicks.Count - 1; index >= 0; --index)
            {
                BurningOilSlickVisual slick = ActiveSlicks[index];
                if (slick == null)
                {
                    ActiveSlicks.RemoveAt(index);
                    continue;
                }

                if (!slick.isActiveAndEnabled || count >= MaximumSlicks)
                {
                    continue;
                }

                Vector3 scale = slick.transform.lossyScale;
                float radiusX = slick.footprint.x * Mathf.Abs(scale.x) * 0.5f;
                float radiusZ = slick.footprint.y * Mathf.Abs(scale.z) * 0.5f;
                Vector3 center = slick.transform.position;
                SlickData[count] = new Vector4(center.x, center.z, radiusX, radiusZ);
                SlickParams[count] = new Vector4(
                    slick.edgeSoftness,
                    slick.edgeNoise,
                    slick.patternSeed,
                    slick.opacity);
                count++;
            }

            Shader.SetGlobalInt(SlickCountId, count);
            Shader.SetGlobalVectorArray(SlickDataId, SlickData);
            Shader.SetGlobalVectorArray(SlickParamsId, SlickParams);
        }

        private void BuildFire()
        {
            DestroyFire();

            Transform staleFire = transform.Find("Oil Fire");
            if (staleFire != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(staleFire.gameObject);
                }
                else
                {
                    DestroyImmediate(staleFire.gameObject);
                }
            }

            fireRoot = new GameObject("Oil Fire");
            fireRoot.transform.SetParent(transform, false);

            if (firePrefab == null)
            {
                Debug.LogWarning($"{nameof(BurningOilSlickVisual)} on {name} has no fire prefab assigned.", this);
                return;
            }

            var random = new System.Random(Mathf.RoundToInt(patternSeed * 1000f));
            int audioClusterIndex = fireClusterCount / 2;
            for (int index = 0; index < fireClusterCount; ++index)
            {
                float normalized = fireClusterCount <= 1
                    ? 0.5f
                    : (float)index / (fireClusterCount - 1);
                float laneSpacing = footprint.y * 0.84f / Mathf.Max(1, fireClusterCount - 1);
                Vector3 position = new Vector3(
                    Mathf.Lerp(-footprint.x * 0.36f, footprint.x * 0.36f,
                        (float)random.NextDouble()),
                    fireHeight,
                    Mathf.Lerp(-footprint.y * 0.42f, footprint.y * 0.42f, normalized)
                        + Mathf.Lerp(-laneSpacing * 0.22f, laneSpacing * 0.22f,
                            (float)random.NextDouble()));
                float size = Mathf.Lerp(
                    fireScaleRange.x,
                    fireScaleRange.y,
                    (float)random.NextDouble());

                GameObject cluster = Instantiate(firePrefab, fireRoot.transform);
                cluster.name = $"Fire Cluster {index + 1}";
                cluster.transform.localPosition = position;
                cluster.transform.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.Lerp(0f, 360f, (float)random.NextDouble()),
                    0f);
                cluster.transform.localScale = Vector3.one * size;

                AudioSource[] audioSources = cluster.GetComponentsInChildren<AudioSource>(true);
                for (int audioIndex = 0; audioIndex < audioSources.Length; ++audioIndex)
                {
                    audioSources[audioIndex].enabled = index == audioClusterIndex;
                }
            }

            GameObject lightObject = new GameObject("Oil Fire Light");
            lightObject.transform.SetParent(fireRoot.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.28f, 0.035f);
            fireLight.intensity = 4.5f;
            fireLight.range = Mathf.Min(22f, Mathf.Max(footprint.x, footprint.y) * 0.45f);
            fireLight.shadows = LightShadows.None;
        }

        private void AnimateFire()
        {
            float time = Time.time * flickerSpeed;
            if (fireLight != null)
            {
                fireLight.intensity = 4.3f + Mathf.Sin(time * 1.31f) * 0.7f;
            }
        }

        private void DestroyFire()
        {
            if (fireRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(fireRoot);
                }
                else
                {
                    DestroyImmediate(fireRoot);
                }
                fireRoot = null;
            }
            fireLight = null;
        }
    }
}
