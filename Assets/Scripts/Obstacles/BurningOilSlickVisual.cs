using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField, Range(3, 24)] private int fireClusterCount = 16;
        [SerializeField, Min(0f)] private float fireHeight = 0.55f;
        [SerializeField, Min(0f)] private float flickerSpeed = 8f;

        private readonly List<FlameCluster> flames = new();
        private GameObject fireRoot;
        private Material flameMaterial;
        private Mesh flameMesh;
        private Light fireLight;

        private sealed class FlameCluster
        {
            public Transform Root;
            public Vector3 BasePosition;
            public Vector3 BaseScale;
            public float Phase;
        }

        public void Configure(Vector2 area, int clusterCount)
        {
            footprint = new Vector2(
                Mathf.Max(0.1f, area.x),
                Mathf.Max(0.1f, area.y));
            fireClusterCount = Mathf.Clamp(clusterCount, 3, 24);
            UploadMasks(true);
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

            flameMaterial = CreateFlameMaterial();
            flameMesh = CreateFlameMesh();

            var random = new System.Random(Mathf.RoundToInt(patternSeed * 1000f));
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
                float size = Mathf.Lerp(0.65f, 1.25f, (float)random.NextDouble());

                GameObject clusterRoot = new GameObject($"Flame {index + 1}");
                clusterRoot.transform.SetParent(fireRoot.transform, false);
                clusterRoot.transform.localPosition = position;

                CreateFlameLobe(
                    clusterRoot.transform,
                    "Flame Shape",
                    Vector3.one * size,
                    flameMesh,
                    flameMaterial);

                flames.Add(new FlameCluster
                {
                    Root = clusterRoot.transform,
                    BasePosition = position,
                    BaseScale = Vector3.one,
                    Phase = (float)random.NextDouble() * Mathf.PI * 2f
                });
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
            for (int index = 0; index < flames.Count; ++index)
            {
                FlameCluster flame = flames[index];
                if (flame.Root == null)
                {
                    continue;
                }

                float flicker = 1f
                    + Mathf.Sin(time + flame.Phase) * 0.12f
                    + Mathf.Sin(time * 1.73f + flame.Phase * 2.1f) * 0.06f;
                Vector3 position = flame.BasePosition;
                position.y += Mathf.Sin(time * 0.42f + flame.Phase) * 0.18f;
                position.x += Mathf.Sin(time * 0.31f + flame.Phase) * 0.08f;
                flame.Root.localPosition = position;
                flame.Root.localScale = new Vector3(
                    2f - flicker,
                    flicker,
                    2f - flicker);
                flame.Root.localRotation = Quaternion.Euler(
                    Mathf.Sin(time * 0.55f + flame.Phase) * 5f,
                    time * 2f + flame.Phase * Mathf.Rad2Deg,
                    Mathf.Cos(time * 0.47f + flame.Phase) * 5f);
            }

            if (fireLight != null)
            {
                fireLight.intensity = 4.3f + Mathf.Sin(time * 1.31f) * 0.7f;
            }
        }

        private static GameObject CreateFlameLobe(
            Transform parent,
            string name,
            Vector3 scale,
            Mesh mesh,
            Material material)
        {
            GameObject lobe = new GameObject(name);
            lobe.name = name;
            lobe.transform.SetParent(parent, false);
            lobe.transform.localScale = scale;
            MeshFilter filter = lobe.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = lobe.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return lobe;
        }

        private static Mesh CreateFlameMesh()
        {
            const int sides = 7;
            const int ringCount = 3;
            var vertices = new Vector3[sides * ringCount + 2];
            var triangles = new int[sides * 18];

            for (int side = 0; side < sides; ++side)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                vertices[side] = new Vector3(cosine * 0.25f, 0f, sine * 0.25f);
                vertices[sides + side] = new Vector3(
                    cosine * 0.38f - 0.04f,
                    0.48f,
                    sine * 0.38f);
                vertices[sides * 2 + side] = new Vector3(
                    cosine * 0.19f + 0.07f,
                    1.02f,
                    sine * 0.19f - 0.025f);
            }

            int bottomCenter = sides * ringCount;
            int tip = bottomCenter + 1;
            vertices[bottomCenter] = Vector3.zero;
            vertices[tip] = new Vector3(-0.08f, 1.58f, 0.04f);

            int triangle = 0;
            for (int side = 0; side < sides; ++side)
            {
                int next = (side + 1) % sides;
                for (int ring = 0; ring < ringCount - 1; ++ring)
                {
                    int currentRing = ring * sides;
                    int nextRing = (ring + 1) * sides;
                    triangles[triangle++] = currentRing + side;
                    triangles[triangle++] = nextRing + side;
                    triangles[triangle++] = nextRing + next;
                    triangles[triangle++] = currentRing + side;
                    triangles[triangle++] = nextRing + next;
                    triangles[triangle++] = currentRing + next;
                }

                triangles[triangle++] = sides * 2 + side;
                triangles[triangle++] = tip;
                triangles[triangle++] = sides * 2 + next;
                triangles[triangle++] = bottomCenter;
                triangles[triangle++] = side;
                triangles[triangle++] = next;
            }

            Mesh mesh = new Mesh
            {
                name = "Runtime Low-Poly Flame",
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateFlameMaterial()
        {
            Shader shader = Shader.Find("RoyaltyBoat/Burning Oil Fire");
            Material material = new Material(shader)
            {
                name = "Runtime Burning Oil Fire",
                hideFlags = HideFlags.DontSave
            };
            material.SetColor("_BaseColor", new Color(1f, 0.78f, 0.08f));
            material.SetColor("_MiddleColor", new Color(1f, 0.28f, 0.015f));
            material.SetColor("_TipColor", new Color(0.72f, 0.035f, 0.012f));
            return material;
        }

        private void DestroyFire()
        {
            flames.Clear();

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

            DestroyRuntimeMaterial(ref flameMaterial);
            if (flameMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(flameMesh);
                }
                else
                {
                    DestroyImmediate(flameMesh);
                }
                flameMesh = null;
            }
            fireLight = null;
        }

        private static void DestroyRuntimeMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
            material = null;
        }
    }
}
