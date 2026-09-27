using System.Collections.Generic;
using RoyaltyBoat.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class VoyageShipHealthController : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        private const int TextureSize = 512;
        private const float RefreshInterval = 0.1f;
        private static readonly Vector3 PreviewOrigin = new Vector3(12000f, -12000f, 12000f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int XRayAmountId = Shader.PropertyToID("_XRayAmount");

        private sealed class PreviewRenderer
        {
            public Renderer Source;
            public Renderer Clone;
        }

        private sealed class PreviewPart
        {
            public Block Source;
            public readonly List<PreviewRenderer> Renderers = new List<PreviewRenderer>();
            public readonly List<Mesh> BakedMeshes = new List<Mesh>();
            public float LastHealthRatio = -1f;
            public bool Destroyed;
        }

        private readonly List<PreviewPart> parts = new List<PreviewPart>();
        private readonly HashSet<Block> trackedBlocks = new HashSet<Block>();
        private MaterialPropertyBlock properties;

        private UIDocument document;
        private Image viewport;
        private Ship ship;
        private GameObject stage;
        private Transform previewRoot;
        private Camera previewCamera;
        private RenderTexture target;
        private Material hologramMaterial;
        private float nextRefreshTime;
        private bool framed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene activeScene, LoadSceneMode mode)
        {
            if (!VoyageFlow.IsVoyageActive ||
                (activeScene.name != VoyageFlow.GameplaySceneName &&
                 activeScene.name != VoyageFlow.DevMapSceneName) ||
                FindAnyObjectByType<VoyageShipHealthController>() != null)
            {
                return;
            }

            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("Voyage/ShipHealth");
            PanelSettings panelSettings = Resources.Load<PanelSettings>("MainMenu/MainMenuPanelSettings");
            Shader hologramShader = Shader.Find("RoyaltyBoat/UI/ShipHealthHologram");
            if (layout == null || panelSettings == null || hologramShader == null)
            {
                Debug.LogError("Ship health HUD assets could not be loaded.");
                return;
            }

            GameObject host = new GameObject("Ship Health HUD");
            UIDocument uiDocument = host.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = layout;
            uiDocument.sortingOrder = 89;
            host.AddComponent<VoyageShipHealthController>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            viewport = document.rootVisualElement.Q<Image>("shipHealthViewport");
            CreatePreviewStage();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            if (ship == null)
            {
                ship = FindAnyObjectByType<Ship>();
                if (ship == null)
                {
                    return;
                }
            }

            AddNewBlocks();
            RefreshParts();
            if (!framed && parts.Count > 0)
            {
                FrameShip();
            }
        }

        private void OnDestroy()
        {
            if (viewport != null)
            {
                viewport.image = null;
            }

            foreach (PreviewPart part in parts)
            {
                foreach (Mesh mesh in part.BakedMeshes)
                {
                    if (mesh != null)
                    {
                        Destroy(mesh);
                    }
                }
            }

            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }

            if (target != null)
            {
                target.Release();
                Destroy(target);
            }

            if (hologramMaterial != null)
            {
                Destroy(hologramMaterial);
            }

            if (stage != null)
            {
                Destroy(stage);
            }
        }

        private void CreatePreviewStage()
        {
            Shader shader = Shader.Find("RoyaltyBoat/UI/ShipHealthHologram");
            if (shader == null)
            {
                enabled = false;
                return;
            }

            hologramMaterial = new Material(shader)
            {
                name = "Ship Health Hologram Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            stage = new GameObject("Ship Health Preview Stage")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            stage.transform.position = PreviewOrigin;

            var previewRootObject = new GameObject("Hologram Ship")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            previewRoot = previewRootObject.transform;
            previewRoot.SetParent(stage.transform, false);

            var cameraObject = new GameObject("Ship Health Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            cameraObject.transform.SetParent(stage.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.allowHDR = true;
            previewCamera.allowMSAA = true;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;

            target = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Ship Health Render Target",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            previewCamera.targetTexture = target;

            if (viewport != null)
            {
                viewport.image = target;
                viewport.scaleMode = ScaleMode.ScaleToFit;
            }
        }

        private void AddNewBlocks()
        {
            foreach (Block block in ship.Blocks)
            {
                if (block == null || !trackedBlocks.Add(block))
                {
                    continue;
                }

                PreviewPart part = CreatePart(block);
                if (part.Renderers.Count > 0)
                {
                    parts.Add(part);
                    framed = false;
                }
            }
        }

        private PreviewPart CreatePart(Block block)
        {
            var part = new PreviewPart { Source = block };
            Renderer[] sourceRenderers = block.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer sourceRenderer in sourceRenderers)
            {
                if (sourceRenderer == null || sourceRenderer.GetComponentInParent<Block>() != block)
                {
                    continue;
                }

                Mesh mesh = null;
                if (sourceRenderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh { name = $"{sourceRenderer.name} Health Preview Mesh" };
                    skinned.BakeMesh(mesh);
                    part.BakedMeshes.Add(mesh);
                }
                else if (sourceRenderer is MeshRenderer)
                {
                    MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                    if (sourceFilter != null)
                    {
                        mesh = sourceFilter.sharedMesh;
                    }
                }

                if (mesh == null)
                {
                    continue;
                }

                var cloneObject = new GameObject(sourceRenderer.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = PreviewLayer
                };
                cloneObject.transform.SetParent(previewRoot, false);
                MeshFilter filter = cloneObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer cloneRenderer = cloneObject.AddComponent<MeshRenderer>();
                int materialCount = Mathf.Max(1, sourceRenderer.sharedMaterials.Length);
                var materials = new Material[materialCount];
                for (int i = 0; i < materialCount; i++)
                {
                    materials[i] = hologramMaterial;
                }

                cloneRenderer.sharedMaterials = materials;
                cloneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloneRenderer.receiveShadows = false;
                part.Renderers.Add(new PreviewRenderer
                {
                    Source = sourceRenderer,
                    Clone = cloneRenderer
                });
                SyncTransform(sourceRenderer.transform, cloneObject.transform);
            }

            return part;
        }

        private void RefreshParts()
        {
            foreach (PreviewPart part in parts)
            {
                if (!part.Destroyed && (part.Source == null || !part.Source.IsAlive))
                {
                    part.Destroyed = true;
                }

                if (!part.Destroyed)
                {
                    foreach (PreviewRenderer previewRenderer in part.Renderers)
                    {
                        if (previewRenderer.Source != null)
                        {
                            SyncTransform(previewRenderer.Source.transform, previewRenderer.Clone.transform);
                        }
                    }
                }

                float healthRatio = part.Destroyed || part.Source == null
                    ? 0f
                    : Mathf.Clamp01((float)part.Source.Health / Mathf.Max(1, part.Source.MaxHealth));
                if (!Mathf.Approximately(healthRatio, part.LastHealthRatio))
                {
                    ApplyHealthAppearance(part, healthRatio);
                    part.LastHealthRatio = healthRatio;
                }
            }
        }

        private void ApplyHealthAppearance(PreviewPart part, float healthRatio)
        {
            if (part == null)
            {
                return;
            }

            Color color;
            float opacity;
            float xRayAmount;
            if (part.Destroyed || healthRatio <= 0f)
            {
                color = new Color(0.28f, 0.31f, 0.34f, 1f);
                opacity = 0.14f;
                xRayAmount = 0.32f;
            }
            else if (healthRatio <= 0.25f)
            {
                color = new Color(1f, 0.04f, 0.025f, 1f);
                opacity = 0.52f;
                xRayAmount = 0.8f;
            }
            else if (healthRatio <= 0.5f)
            {
                color = new Color(1f, 0.28f, 0.025f, 1f);
                opacity = 0.64f;
                xRayAmount = 0.62f;
            }
            else if (healthRatio < 0.999f)
            {
                color = new Color(1f, 0.78f, 0.035f, 1f);
                opacity = 0.78f;
                xRayAmount = 0.42f;
            }
            else
            {
                color = Color.white;
                opacity = 0.72f;
                xRayAmount = 0f;
            }

            properties ??= new MaterialPropertyBlock();
            properties.Clear();
            properties.SetColor(BaseColorId, color);
            properties.SetFloat(OpacityId, opacity);
            properties.SetFloat(XRayAmountId, xRayAmount);
            foreach (PreviewRenderer previewRenderer in part.Renderers)
            {
                if (previewRenderer?.Clone != null)
                {
                    previewRenderer.Clone.SetPropertyBlock(properties);
                }
            }
        }

        private void SyncTransform(Transform source, Transform clone)
        {
            clone.localPosition = ship.transform.InverseTransformPoint(source.position);
            clone.localRotation = Quaternion.Inverse(ship.transform.rotation) * source.rotation;
            Vector3 shipScale = ship.transform.lossyScale;
            Vector3 sourceScale = source.lossyScale;
            clone.localScale = new Vector3(
                SafeDivide(sourceScale.x, shipScale.x),
                SafeDivide(sourceScale.y, shipScale.y),
                SafeDivide(sourceScale.z, shipScale.z));
        }

        private void FrameShip()
        {
            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!found)
            {
                return;
            }

            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            Vector3 direction = new Vector3(-1f, 0.95f, -1f).normalized;
            previewCamera.transform.position = bounds.center + direction * (radius * 4f + 1f);
            previewCamera.transform.LookAt(bounds.center + Vector3.up * (bounds.extents.y * 0.08f));
            previewCamera.orthographicSize = radius * 0.92f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = radius * 10f + 10f;
            framed = true;
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) < 0.0001f ? numerator : numerator / denominator;
        }
    }
}
