using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyaltyBoat.UI
{
    internal sealed class ProductThumbnailRenderer : IDisposable
    {
        private const int PreviewLayer = 31;
        private const int TextureSize = 256;
        private static readonly Vector3 PreviewOrigin = new(10000f, -10000f, 10000f);

        private readonly Dictionary<GameObject, Texture2D> cache = new();
        private readonly GameObject stage;
        private readonly Camera previewCamera;
        private readonly RenderTexture sharedTarget;

        public ProductThumbnailRenderer()
        {
            stage = new GameObject("Product Thumbnail Stage")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            stage.transform.position = PreviewOrigin;

            var cameraObject = new GameObject("Thumbnail Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            cameraObject.transform.SetParent(stage.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;

            sharedTarget = new RenderTexture(
                TextureSize,
                TextureSize,
                24,
                RenderTextureFormat.ARGB32)
            {
                name = "Shared Product Thumbnail Target",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            sharedTarget.Create();
            previewCamera.targetTexture = sharedTarget;

            CreateLight("Thumbnail Key", new Vector3(48f, -35f, 28f), 1.35f, new Color(1f, 0.82f, 0.62f));
            CreateLight("Thumbnail Fill", new Vector3(28f, 145f, 12f), 0.72f, new Color(0.55f, 0.76f, 1f));
            CreateLight("Thumbnail Rim", new Vector3(68f, 25f, -145f), 0.5f, new Color(1f, 0.95f, 0.82f));
        }

        public Texture2D Render(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            if (cache.TryGetValue(prefab, out Texture2D cached))
            {
                return cached;
            }

            GameObject preview = UnityEngine.Object.Instantiate(prefab, stage.transform);
            preview.name = $"{prefab.name} Thumbnail";
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            SetLayerRecursively(preview.transform, PreviewLayer);
            DisableRuntimeComponents(preview);
            preview.SetActive(true);

            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
            if (!TryGetBounds(renderers, out Bounds bounds))
            {
                preview.SetActive(false);
                UnityEngine.Object.Destroy(preview);
                return null;
            }

            Frame(bounds);
            previewCamera.Render();

            var thumbnail = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = $"{prefab.name} Thumbnail",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Graphics.CopyTexture(sharedTarget, thumbnail);
            cache.Add(prefab, thumbnail);

            preview.SetActive(false);
            UnityEngine.Object.Destroy(preview);
            return thumbnail;
        }

        public void Dispose()
        {
            foreach (Texture2D thumbnail in cache.Values)
            {
                if (thumbnail != null)
                {
                    UnityEngine.Object.Destroy(thumbnail);
                }
            }

            cache.Clear();
            previewCamera.targetTexture = null;
            sharedTarget.Release();
            UnityEngine.Object.Destroy(sharedTarget);
            UnityEngine.Object.Destroy(stage);
        }

        private void CreateLight(string name, Vector3 rotation, float intensity, Color color)
        {
            var lightObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(rotation);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.Soft;
        }

        private void Frame(Bounds bounds)
        {
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            Vector3 direction = new Vector3(1f, 0.82f, -1f).normalized;
            previewCamera.transform.position = bounds.center + direction * (radius * 4f + 1f);
            previewCamera.transform.LookAt(bounds.center + Vector3.up * (bounds.extents.y * 0.04f));
            previewCamera.orthographicSize = radius * 1.08f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = radius * 10f + 10f;
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
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

            return found;
        }

        private static void DisableRuntimeComponents(GameObject preview)
        {
            foreach (MonoBehaviour behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in preview.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }
    }
}
