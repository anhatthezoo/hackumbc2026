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
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        outlineMaterial = new Material(shader)
        {
            name = "Runtime Build Selection",
            color = color,
            renderQueue = 4000
        };
        if (outlineMaterial.HasProperty("_BaseColor")) outlineMaterial.SetColor("_BaseColor", color);

        outlineRoot = new GameObject("Build Selection Outline");
        outlineRoot.transform.SetParent(transform, false);
        Bounds localBounds = GetLocalBounds();
        Vector3 center = localBounds.center;
        Vector3 halfSize = localBounds.extents + Vector3.one * 0.025f;
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
            GameObject edgeObject = new GameObject("Selection Edge " + edge);
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

    private Bounds GetLocalBounds()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool found = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger
                || (outlineRoot != null && collider.transform.IsChildOf(outlineRoot.transform))) continue;
            Bounds world = collider.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            Vector3[] corners =
            {
                new(min.x, min.y, min.z), new(min.x, min.y, max.z),
                new(min.x, max.y, min.z), new(min.x, max.y, max.z),
                new(max.x, min.y, min.z), new(max.x, min.y, max.z),
                new(max.x, max.y, min.z), new(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 local = transform.InverseTransformPoint(corner);
                if (!found) { localBounds = new Bounds(local, Vector3.zero); found = true; }
                else localBounds.Encapsulate(local);
            }
        }
        if (found) return localBounds;
        return new Bounds(Vector3.zero, Vector3.one);
    }

    private void OnDestroy() => ClearOutline();

    private void ClearOutline()
    {
        if (outlineRoot != null)
        {
            // Detach immediately so a same-frame recolor cannot measure the old
            // line renderers and recursively inflate the next outline.
            outlineRoot.SetActive(false);
            outlineRoot.transform.SetParent(null, false);
            Destroy(outlineRoot);
            outlineRoot = null;
        }
        if (outlineMaterial != null) { Destroy(outlineMaterial); outlineMaterial = null; }
    }
}
