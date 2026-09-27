using UnityEngine;

public sealed class ShipBuildArea : MonoBehaviour
{
    [SerializeField] private Transform platform;
    [SerializeField] private Ship startingShip;

    public Transform Platform => platform;
    public Ship StartingShip => startingShip;

    /// <summary>
    /// Collects every block positioned over the build platform into the neutral
    /// ship root. Connectivity is deliberately ignored: separate rafts and loose
    /// parts on the platform must all survive the voyage scene transition.
    /// </summary>
    public Ship PrepareShipForLaunch()
    {
        Ship launchShip = startingShip != null
            ? startingShip
            : FindAnyObjectByType<Ship>();

        if (launchShip == null)
        {
            return null;
        }

        Block[] sceneBlocks = FindObjectsByType<Block>(FindObjectsInactive.Exclude);

        foreach (Block block in sceneBlocks)
        {
            if (block == null
                || !IsOverPlatform(block.transform.position)
                || launchShip.ContainsBlock(block))
            {
                continue;
            }

            launchShip.AttachBlock(block);
        }

        launchShip.RefreshBlocks();
        return launchShip;
    }

    public bool IsOverPlatform(Vector3 worldPosition)
    {
        if (platform == null)
        {
            return false;
        }

        Collider platformCollider = platform.GetComponent<Collider>();
        if (platformCollider != null)
        {
            Bounds bounds = platformCollider.bounds;
            const float edgeTolerance = 0.05f;
            return worldPosition.x >= bounds.min.x - edgeTolerance
                && worldPosition.x <= bounds.max.x + edgeTolerance
                && worldPosition.z >= bounds.min.z - edgeTolerance
                && worldPosition.z <= bounds.max.z + edgeTolerance;
        }

        Vector3 localPosition = platform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(localPosition.x) <= 0.5f
            && Mathf.Abs(localPosition.z) <= 0.5f;
    }

    private void Awake()
    {
        ConfigureCamera(Camera.main);
    }

    public void ConfigureCamera(Camera targetCamera)
    {
        if (targetCamera == null || platform == null)
        {
            return;
        }

        BlockDragController dragController =
            targetCamera.GetComponent<BlockDragController>();

        if (dragController == null)
        {
            dragController =
                targetCamera.gameObject.AddComponent<BlockDragController>();
        }

        dragController.PlacementCenter = platform;

        CameraOrbitController orbitController =
            targetCamera.GetComponent<CameraOrbitController>();

        if (orbitController == null)
        {
            orbitController =
                targetCamera.gameObject.AddComponent<CameraOrbitController>();
        }

        orbitController.OrbitTarget = platform;

    }
}
