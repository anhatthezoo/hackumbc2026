using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class AdaptiveBuildGrid : MonoBehaviour
{
    [Header("Grid Target")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform centerTarget;
    [SerializeField] private float verticalOffset = 0.51f;

    [Header("Grid Appearance")]
    [SerializeField, Min(1)] private int halfSize = 10;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;
    [SerializeField] private Color minorLineColor =
        new Color(0.55f, 0.7f, 0.74f, 0.3f);
    [SerializeField] private Color majorLineColor =
        new Color(0.62f, 0.82f, 0.86f, 0.55f);
    [SerializeField] private Color axisLineColor =
        new Color(0.25f, 1f, 0.65f, 0.8f);

    private GameObject visualRoot;
    private Material lineMaterial;

    public Camera ViewCamera
    {
        get => viewCamera;
        set => viewCamera = value;
    }

    public Transform CenterTarget
    {
        get => centerTarget;
        set => centerTarget = value;
    }

    private void OnEnable()
    {
        RebuildGrid();
    }

    private void OnDisable()
    {
        ClearGrid();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RebuildAfterValidation;
        UnityEditor.EditorApplication.delayCall += RebuildAfterValidation;
#endif
    }

#if UNITY_EDITOR
    private void RebuildAfterValidation()
    {
        if (this != null && isActiveAndEnabled)
        {
            RebuildGrid();
        }
    }
#endif

    private void LateUpdate()
    {
        Camera activeCamera = viewCamera != null ? viewCamera : Camera.main;

        if (activeCamera == null || centerTarget == null)
        {
            return;
        }

        if (visualRoot == null)
        {
            RebuildGrid();
        }

        bool cameraIsAbove = activeCamera.transform.position.y
            >= centerTarget.position.y;

        transform.rotation = Quaternion.Euler(
            cameraIsAbove ? 90f : -90f,
            0f,
            0f);
        transform.position = centerTarget.position
            + Vector3.up * verticalOffset;
    }

    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        ClearGrid();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        lineMaterial = new Material(shader)
        {
            name = "Runtime Build Grid Lines",
            color = Color.white,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };

        lineMaterial.SetOverrideTag("RenderType", "Transparent");

        if (lineMaterial.HasProperty("_Surface"))
        {
            lineMaterial.SetFloat("_Surface", 1f);
        }

        if (lineMaterial.HasProperty("_SrcBlend"))
        {
            lineMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (lineMaterial.HasProperty("_DstBlend"))
        {
            lineMaterial.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha);
        }

        if (lineMaterial.HasProperty("_ZWrite"))
        {
            lineMaterial.SetFloat("_ZWrite", 0f);
        }

        lineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        visualRoot = new GameObject("Generated Grid Lines")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        visualRoot.transform.SetParent(transform, false);

        for (int coordinate = -halfSize; coordinate <= halfSize; coordinate++)
        {
            bool isAxis = coordinate == 0;
            bool isMajor = coordinate % 5 == 0;
            Color color = isAxis
                ? axisLineColor
                : isMajor
                    ? majorLineColor
                    : minorLineColor;
            float width = isAxis ? 0.045f : isMajor ? 0.028f : 0.016f;
            float linePosition = coordinate * tileSize;
            float extent = halfSize * tileSize;

            CreateLine(
                "Grid X " + coordinate,
                new Vector3(linePosition, -extent, 0f),
                new Vector3(linePosition, extent, 0f),
                color,
                width);
            CreateLine(
                "Grid Y " + coordinate,
                new Vector3(-extent, linePosition, 0f),
                new Vector3(extent, linePosition, 0f),
                color,
                width);
        }
    }

    private void CreateLine(
        string lineName,
        Vector3 start,
        Vector3 end,
        Color color,
        float width)
    {
        GameObject lineObject = new GameObject(lineName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        lineObject.transform.SetParent(visualRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sharedMaterial = lineMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 100;
    }

    private void ClearGrid()
    {
        if (visualRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(visualRoot);
            }
            else
            {
                DestroyImmediate(visualRoot);
            }

            visualRoot = null;
        }

        if (lineMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(lineMaterial);
            }
            else
            {
                DestroyImmediate(lineMaterial);
            }

            lineMaterial = null;
        }
    }
}
