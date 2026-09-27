using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class AdaptiveBuildGrid : MonoBehaviour
{
    [Header("Grid Target")]
    [SerializeField] private Transform centerTarget;
    [SerializeField] private Ship gridSource;
    [SerializeField, Min(0f)] private float verticalOffset = 0.02f;
    [SerializeField, Min(0.1f)] private float fallbackTileSize = 2f;

    [Header("Grid Appearance")]
    [SerializeField] private Color minorLineColor =
        new Color(0.9f, 0.94f, 1f, 0.5f);
    [SerializeField] private Color majorLineColor =
        new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color axisLineColor =
        new Color(0.92f, 1f, 0.96f, 0.95f);

    private GameObject visualRoot;
    private Material lineMaterial;
    private float renderedTileSize;
    private Vector3 renderedBoundsSize;
    private Vector3 renderedSnapOrigin;

    public void SetGridSource(Ship ship)
    {
        if (gridSource == ship)
        {
            return;
        }

        gridSource = ship;
        if (isActiveAndEnabled)
        {
            RebuildGrid();
        }
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
        fallbackTileSize = Mathf.Max(0.1f, fallbackTileSize);
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
        float tileSize = GetTileSize();
        Bounds bounds = GetGridBounds();
        Vector3 snapOrigin = GetSnapOrigin();
        if (visualRoot == null
            || !Mathf.Approximately(tileSize, renderedTileSize)
            || (bounds.size - renderedBoundsSize).sqrMagnitude > 0.001f
            || !AreGridOriginsEquivalent(
                snapOrigin,
                renderedSnapOrigin,
                tileSize))
        {
            RebuildGrid();
            return;
        }

        PositionGrid(bounds);
    }

    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        ClearGrid();

        float tileSize = GetTileSize();
        Bounds bounds = GetGridBounds();
        Vector3 snapOrigin = GetSnapOrigin();
        renderedTileSize = tileSize;
        renderedBoundsSize = bounds.size;
        renderedSnapOrigin = snapOrigin;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return;
        }

        lineMaterial = new Material(shader)
        {
            name = "Runtime Build Grid Lines",
            color = Color.white,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };
        ConfigureTransparentMaterial(lineMaterial);

        visualRoot = new GameObject("Generated Build Grid")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        visualRoot.transform.SetParent(transform, true);
        PositionGrid(bounds);

        // Snapping uses block centers. Grid lines therefore sit half a tile
        // away from the snap origin so a snapped block fills one grid square
        // instead of straddling four of them.
        float localOriginX = snapOrigin.x - bounds.center.x + tileSize * 0.5f;
        float localOriginZ = snapOrigin.z - bounds.center.z + tileSize * 0.5f;
        int minimumColumn = Mathf.CeilToInt(
            (-bounds.extents.x - localOriginX) / tileSize);
        int maximumColumn = Mathf.FloorToInt(
            (bounds.extents.x - localOriginX) / tileSize);
        int minimumRow = Mathf.CeilToInt(
            (-bounds.extents.z - localOriginZ) / tileSize);
        int maximumRow = Mathf.FloorToInt(
            (bounds.extents.z - localOriginZ) / tileSize);

        for (int column = minimumColumn; column <= maximumColumn; column++)
        {
            CreateLine(
                $"Grid Column {column}",
                new Vector3(
                    localOriginX + column * tileSize,
                    0f,
                    -bounds.extents.z),
                new Vector3(
                    localOriginX + column * tileSize,
                    0f,
                    bounds.extents.z),
                GetLineColor(column),
                GetLineWidth(column));
        }

        for (int row = minimumRow; row <= maximumRow; row++)
        {
            CreateLine(
                $"Grid Row {row}",
                new Vector3(
                    -bounds.extents.x,
                    0f,
                    localOriginZ + row * tileSize),
                new Vector3(
                    bounds.extents.x,
                    0f,
                    localOriginZ + row * tileSize),
                GetLineColor(row),
                GetLineWidth(row));
        }
    }

    private float GetTileSize()
    {
        return gridSource == null
            ? fallbackTileSize
            : Mathf.Max(0.1f, gridSource.AttachmentGridSize);
    }

    private Vector3 GetSnapOrigin()
    {
        if (gridSource == null)
        {
            return centerTarget == null ? transform.position : centerTarget.position;
        }

        return gridSource.AnchorBlock == null
            ? gridSource.transform.position
            : gridSource.AnchorBlock.transform.position;
    }

    private static bool AreGridOriginsEquivalent(
        Vector3 first,
        Vector3 second,
        float tileSize)
    {
        float xDifference = Mathf.Abs(Mathf.Repeat(
            first.x - second.x + tileSize * 0.5f,
            tileSize) - tileSize * 0.5f);
        float zDifference = Mathf.Abs(Mathf.Repeat(
            first.z - second.z + tileSize * 0.5f,
            tileSize) - tileSize * 0.5f);
        return xDifference <= 0.001f && zDifference <= 0.001f;
    }

    private Bounds GetGridBounds()
    {
        Collider targetCollider = centerTarget == null
            ? null
            : centerTarget.GetComponent<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds;
        }

        Vector3 center = centerTarget == null
            ? transform.position
            : centerTarget.position;
        return new Bounds(center, new Vector3(20f, 0f, 20f));
    }

    private void PositionGrid(Bounds bounds)
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.transform.SetPositionAndRotation(
            new Vector3(bounds.center.x, bounds.max.y + verticalOffset, bounds.center.z),
            Quaternion.identity);
    }

    private Color GetLineColor(int coordinate)
    {
        if (coordinate == 0)
        {
            return axisLineColor;
        }

        return coordinate % 5 == 0 ? majorLineColor : minorLineColor;
    }

    private static float GetLineWidth(int coordinate)
    {
        if (coordinate == 0)
        {
            return 0.06f;
        }

        return coordinate % 5 == 0 ? 0.045f : 0.025f;
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

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void ClearGrid()
    {
        DestroyRuntimeObject(visualRoot);
        DestroyRuntimeObject(lineMaterial);
        visualRoot = null;
        lineMaterial = null;
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
