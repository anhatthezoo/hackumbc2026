using RoyaltyBoat.King;
using UnityEngine;

public sealed class ShipBuildArea : MonoBehaviour
{
    [SerializeField] private Transform platform;
    [SerializeField] private Ship startingShip;
    [SerializeField] private KingController kingPrefab;
    [SerializeField] private Vector3 kingStagingOffset = new Vector3(-4f, 1f, -3f);

    public Transform Platform => platform;
    public Ship StartingShip => startingShip;
    public float AttachmentGridSize => startingShip == null
        ? Ship.DefaultAttachmentGridSize
        : startingShip.AttachmentGridSize;
    public KingBuildPlacement KingPlacement { get; private set; }

    public void ConfigureLoosePart(GameObject part)
    {
        if (part == null)
        {
            return;
        }

        float scaleRatio = startingShip == null
            ? 1f
            : startingShip.BuildScaleRatio;
        part.transform.localScale *= scaleRatio;

        GameObject loosePartsRoot = GameObject.Find("Build Pieces");
        if (loosePartsRoot != null)
        {
            part.transform.SetParent(loosePartsRoot.transform, true);
        }

        Physics.SyncTransforms();
        Collider partCollider = part.GetComponentInChildren<Collider>();
        if (partCollider == null)
        {
            return;
        }

        float floorHeight = GetBuildFloorHeight(loosePartsRoot, part);
        part.transform.position += Vector3.up
            * (floorHeight - partCollider.bounds.min.y);
        Physics.SyncTransforms();
    }

    public float GetBuildFloorHeight()
    {
        return GetBuildFloorHeight(GameObject.Find("Build Pieces"), null);
    }

    private float GetBuildFloorHeight(GameObject loosePartsRoot, GameObject ignoredPart)
    {
        if (loosePartsRoot != null)
        {
            foreach (Block block in loosePartsRoot.GetComponentsInChildren<Block>(true))
            {
                if (block == null || block.gameObject == ignoredPart)
                {
                    continue;
                }

                Collider blockCollider = block.GetComponentInChildren<Collider>();
                if (blockCollider != null)
                {
                    return blockCollider.bounds.min.y;
                }
            }
        }

        Collider platformCollider = platform == null
            ? null
            : platform.GetComponent<Collider>();
        return platformCollider == null
            ? (platform == null ? 0f : platform.position.y)
            : platformCollider.bounds.max.y;
    }

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
        SpawnKingForBuilding();
        ConfigureCamera(Camera.main);
    }

    private void SpawnKingForBuilding()
    {
        KingPlacement = FindAnyObjectByType<KingBuildPlacement>();
        if (KingPlacement != null || kingPrefab == null)
        {
            return;
        }

        Vector3 stagingPosition = platform == null
            ? transform.TransformPoint(kingStagingOffset)
            : platform.position + kingStagingOffset;
        KingController king = Instantiate(
            kingPrefab,
            stagingPosition,
            Quaternion.identity);
        king.name = "King";

        KingPlacement = king.GetComponent<KingBuildPlacement>();
        if (KingPlacement == null)
        {
            KingPlacement = king.gameObject.AddComponent<KingBuildPlacement>();
        }

        KingPlacement.EnterBuildMode(stagingPosition, Quaternion.identity);
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
