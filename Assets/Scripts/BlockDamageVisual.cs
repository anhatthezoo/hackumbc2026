using UnityEngine;

/// <summary>
/// Adds lightweight world-space damage feedback directly to a ship block.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlockDamageVisual : MonoBehaviour
{
    private const string CrackRootName = "Damage Cracks";
    private static Material crackMaterial;

    private GameObject crackRoot;
    private LineRenderer[] crackLines;

    public int DamageStage { get; private set; }

    public void SetHealth(int health, int maxHealth)
    {
        float normalizedHealth = maxHealth <= 0
            ? 0f
            : Mathf.Clamp01((float)health / maxHealth);
        int nextStage = normalizedHealth <= 0.25f
            ? 2
            : normalizedHealth <= 0.5f
                ? 1
                : 0;

        if (nextStage == DamageStage && crackRoot != null)
        {
            return;
        }

        DamageStage = nextStage;
        EnsureCracks();
        crackRoot.SetActive(DamageStage > 0);
        crackLines[0].enabled = DamageStage > 0;
        crackLines[1].enabled = DamageStage > 1;
    }

    private void EnsureCracks()
    {
        if (crackRoot != null)
        {
            return;
        }

        crackRoot = new GameObject(CrackRootName);
        crackRoot.transform.SetParent(transform, false);

        crackLines = new[]
        {
            CreateCrack("Crack Stage 1", CreateCrackPath(false)),
            CreateCrack("Crack Stage 2", CreateCrackPath(true))
        };
    }

    private Vector3[] CreateCrackPath(bool secondary)
    {
        Bounds bounds = GetLocalColliderBounds();
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        bool verticalPart = bounds.size.y
            > Mathf.Max(bounds.size.x, bounds.size.z) * 1.25f;

        if (verticalPart)
        {
            float face = bounds.min.z - 0.006f;
            return secondary
                ? new[]
                {
                    new Vector3(center.x - extents.x * 0.12f, center.y + extents.y * 0.08f, face),
                    new Vector3(center.x + extents.x * 0.12f, center.y + extents.y * 0.2f, face),
                    new Vector3(center.x + extents.x * 0.38f, center.y + extents.y * 0.05f, face),
                    new Vector3(center.x + extents.x * 0.48f, center.y - extents.y * 0.2f, face)
                }
                : new[]
                {
                    new Vector3(center.x - extents.x * 0.42f, center.y + extents.y * 0.32f, face),
                    new Vector3(center.x - extents.x * 0.12f, center.y + extents.y * 0.1f, face),
                    new Vector3(center.x - extents.x * 0.22f, center.y - extents.y * 0.12f, face),
                    new Vector3(center.x + extents.x * 0.1f, center.y - extents.y * 0.38f, face)
                };
        }

        float top = bounds.max.y + 0.006f;
        return secondary
            ? new[]
            {
                new Vector3(center.x - extents.x * 0.12f, top, center.z + extents.z * 0.08f),
                new Vector3(center.x + extents.x * 0.1f, top, center.z + extents.z * 0.2f),
                new Vector3(center.x + extents.x * 0.38f, top, center.z + extents.z * 0.05f),
                new Vector3(center.x + extents.x * 0.48f, top, center.z - extents.z * 0.2f)
            }
            : new[]
            {
                new Vector3(center.x - extents.x * 0.42f, top, center.z + extents.z * 0.32f),
                new Vector3(center.x - extents.x * 0.12f, top, center.z + extents.z * 0.1f),
                new Vector3(center.x - extents.x * 0.22f, top, center.z - extents.z * 0.12f),
                new Vector3(center.x + extents.x * 0.1f, top, center.z - extents.z * 0.38f)
            };
    }

    private Bounds GetLocalColliderBounds()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
        bool hasBounds = false;

        foreach (Collider blockCollider in colliders)
        {
            if (blockCollider == null || blockCollider.isTrigger || !blockCollider.enabled)
            {
                continue;
            }

            Bounds worldBounds = blockCollider.bounds;
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? worldBounds.min.x : worldBounds.max.x,
                    (corner & 2) == 0 ? worldBounds.min.y : worldBounds.max.y,
                    (corner & 4) == 0 ? worldBounds.min.z : worldBounds.max.z);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        return localBounds;
    }

    private LineRenderer CreateCrack(string objectName, Vector3[] positions)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(crackRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.startWidth = 0.035f;
        line.endWidth = 0.018f;
        line.numCornerVertices = 2;
        line.numCapVertices = 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetCrackMaterial();
        line.startColor = new Color(0.12f, 0.055f, 0.025f, 1f);
        line.endColor = new Color(0.2f, 0.08f, 0.025f, 1f);
        return line;
    }

    private static Material GetCrackMaterial()
    {
        if (crackMaterial != null)
        {
            return crackMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        crackMaterial = new Material(shader)
        {
            name = "Runtime Block Crack Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return crackMaterial;
    }
}
