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
            CreateCrack(
                "Crack Stage 1",
                new[]
                {
                    new Vector3(-0.36f, 0.506f, 0.22f),
                    new Vector3(-0.12f, 0.508f, 0.08f),
                    new Vector3(-0.2f, 0.51f, -0.1f),
                    new Vector3(0.08f, 0.512f, -0.3f)
                }),
            CreateCrack(
                "Crack Stage 2",
                new[]
                {
                    new Vector3(-0.13f, 0.514f, 0.07f),
                    new Vector3(0.08f, 0.516f, 0.15f),
                    new Vector3(0.3f, 0.518f, 0.04f),
                    new Vector3(0.42f, 0.52f, -0.17f)
                })
        };
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
