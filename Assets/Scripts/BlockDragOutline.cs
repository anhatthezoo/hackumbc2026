using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BlockDragOutline : MonoBehaviour
{
    private static readonly int[,] EdgeIndices =
    {
        { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 },
        { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 },
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
    };

    private GameObject outlineRoot;
    private Material outlineMaterial;

    public void Configure(Color color, float width)
    {
        ClearOutline();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        outlineMaterial = new Material(shader)
        {
            name = "Runtime Block Drag Highlight",
            color = color,
            renderQueue = 4000
        };

        if (outlineMaterial.HasProperty("_BaseColor"))
        {
            outlineMaterial.SetColor("_BaseColor", color);
        }

        if (outlineMaterial.HasProperty("_EmissionColor"))
        {
            outlineMaterial.EnableKeyword("_EMISSION");
            outlineMaterial.SetColor("_EmissionColor", color * 2f);
        }

        outlineRoot = new GameObject("Drag Highlight");
        outlineRoot.transform.SetParent(transform, false);

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        Vector3 center = boxCollider != null ? boxCollider.center : Vector3.zero;
        Vector3 size = boxCollider != null ? boxCollider.size : Vector3.one;
        Vector3 halfSize = size * 0.5f + Vector3.one * 0.025f;

        Vector3[] corners =
        {
            center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
            center + new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
            center + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x,  halfSize.y, -halfSize.z),
            center + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z),
            center + new Vector3( halfSize.x,  halfSize.y,  halfSize.z)
        };

        for (int edge = 0; edge < EdgeIndices.GetLength(0); edge++)
        {
            GameObject edgeObject = new GameObject("Edge " + edge);
            edgeObject.transform.SetParent(outlineRoot.transform, false);

            LineRenderer line = edgeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, corners[EdgeIndices[edge, 0]]);
            line.SetPosition(1, corners[EdgeIndices[edge, 1]]);
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sharedMaterial = outlineMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.sortingOrder = 1000;
        }
    }

    private void OnDestroy()
    {
        ClearOutline();
    }

    private void ClearOutline()
    {
        if (outlineRoot != null)
        {
            Destroy(outlineRoot);
            outlineRoot = null;
        }

        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
            outlineMaterial = null;
        }
    }
}
