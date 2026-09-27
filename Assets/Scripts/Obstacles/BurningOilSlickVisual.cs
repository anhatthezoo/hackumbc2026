using System.Collections.Generic;
using RoyaltyBoat.Audio;
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
        [SerializeField] private GameObject alternateFirePrefab;
        [SerializeField] private GameObject smokeFirePrefab;
        [SerializeField, Range(3, 24)] private int fireClusterCount = 16;
        [SerializeField, Min(0f)] private float fireHeight = 0.55f;
        [SerializeField] private Vector2 fireScaleRange = new Vector2(1.25f, 2.1f);
        [SerializeField, Min(0f)] private float flickerSpeed = 8f;

        private GameObject fireRoot;
        private readonly List<Light> fireLights = new();

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
            int patchCount = Mathf.Clamp(Mathf.CeilToInt(fireClusterCount / 3f), 3, 8);
            var patchCenters = new Vector2[patchCount];
            float patchSpacing = footprint.y * 0.78f / Mathf.Max(1, patchCount - 1);
            for (int patchIndex = 0; patchIndex < patchCount; ++patchIndex)
            {
                float normalized = patchCount <= 1
                    ? 0.5f
                    : (float)patchIndex / (patchCount - 1);
                float meander = Mathf.Sin(
                    normalized * Mathf.PI * 3.4f + patternSeed) * footprint.x * 0.13f;
                patchCenters[patchIndex] = new Vector2(
                    meander + RandomRange(random, -footprint.x * 0.2f, footprint.x * 0.2f),
                    Mathf.Lerp(-footprint.y * 0.4f, footprint.y * 0.4f, normalized)
                        + RandomRange(random, -patchSpacing * 0.18f, patchSpacing * 0.18f));
            }

            for (int index = 0; index < fireClusterCount; ++index)
            {
                Vector2 patchCenter = patchCenters[index % patchCount];
                Vector3 position = new Vector3(
                    Mathf.Clamp(
                        patchCenter.x + RandomRange(random, -footprint.x * 0.13f, footprint.x * 0.13f),
                        -footprint.x * 0.4f,
                        footprint.x * 0.4f),
                    fireHeight,
                    Mathf.Clamp(
                        patchCenter.y + RandomRange(random, -patchSpacing * 0.31f, patchSpacing * 0.31f),
                        -footprint.y * 0.43f,
                        footprint.y * 0.43f));
                float scaleSample = Mathf.Pow((float)random.NextDouble(), 1.45f);
                float size = Mathf.Lerp(
                    fireScaleRange.x,
                    fireScaleRange.y,
                    scaleSample);
                if (index % 7 == 0)
                {
                    size *= 1.12f;
                }

                double prefabRoll = random.NextDouble();
                GameObject selectedPrefab = smokeFirePrefab != null && prefabRoll > 0.82
                    ? smokeFirePrefab
                    : alternateFirePrefab != null && prefabRoll > 0.43
                        ? alternateFirePrefab
                        : firePrefab;
                GameObject cluster = Instantiate(selectedPrefab, fireRoot.transform);
                cluster.name = $"Fire Cluster {index + 1}";
                cluster.transform.localPosition = position;
                cluster.transform.localRotation = Quaternion.Euler(
                    0f,
                    RandomRange(random, 0f, 360f),
                    0f);
                cluster.transform.localScale = Vector3.one * size;

                ParticleSystem[] particleSystems = cluster.GetComponentsInChildren<ParticleSystem>(true);
                float timeOffset = RandomRange(random, 0.15f, 1.8f);
                for (int particleIndex = 0; particleIndex < particleSystems.Length; ++particleIndex)
                {
                    ParticleSystem particles = particleSystems[particleIndex];
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particles.useAutoRandomSeed = false;
                    particles.randomSeed = unchecked((uint)(
                        Mathf.RoundToInt(patternSeed * 10000f)
                        + index * 193
                        + particleIndex * 977
                        + 1));
                    particles.Simulate(timeOffset, false, true);
                    particles.Play(false);
                }

                AudioSource[] audioSources = cluster.GetComponentsInChildren<AudioSource>(true);
                for (int audioIndex = 0; audioIndex < audioSources.Length; ++audioIndex)
                {
                    audioSources[audioIndex].enabled = false;
                }
            }

            for (int lightIndex = 0; lightIndex < 3; ++lightIndex)
            {
                GameObject lightObject = new GameObject($"Oil Fire Light {lightIndex + 1}");
                lightObject.transform.SetParent(fireRoot.transform, false);
                lightObject.transform.localPosition = new Vector3(
                    Mathf.Sin(patternSeed + lightIndex * 2.1f) * footprint.x * 0.12f,
                    2.2f,
                    Mathf.Lerp(-footprint.y * 0.27f, footprint.y * 0.27f, lightIndex / 2f));
                Light fireLight = lightObject.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = Color.Lerp(
                    new Color(1f, 0.16f, 0.025f),
                    new Color(1f, 0.46f, 0.06f),
                    lightIndex / 2f);
                fireLight.intensity = 2.8f;
                fireLight.range = Mathf.Min(15f, Mathf.Max(footprint.x, footprint.y) * 0.25f);
                fireLight.shadows = LightShadows.None;
                fireLights.Add(fireLight);
            }

            GameAudio.SetFire(this, transform, true);
        }

        private void AnimateFire()
        {
            float time = Time.time * flickerSpeed;
            for (int index = 0; index < fireLights.Count; ++index)
            {
                Light fireLight = fireLights[index];
                if (fireLight != null)
                {
                    fireLight.intensity = 2.65f
                        + Mathf.Sin(time * (1.14f + index * 0.13f) + index * 2.37f) * 0.45f
                        + Mathf.Sin(time * 2.03f + index) * 0.18f;
                }
            }
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private void DestroyFire()
        {
            if (Application.isPlaying)
            {
                GameAudio.SetFire(this, transform, false);
            }

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
            fireLights.Clear();
        }
    }
}
