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

    public KingBuildPlacement AdoptReturningShip(
        Ship ship,
        KingBuildPlacement king,
        Vector3 shipScale,
        Vector3 kingScale)
    {
        if (ship == null)
        {
            return null;
        }

        KingBuildPlacement returningKing = king;
        KingBuildPlacement activeKing = ResolveReturningKing(returningKing);
        if (activeKing == null)
        {
            Debug.LogError("Could not restore or spawn the King in the shipyard.", this);
            return null;
        }

        if (startingShip != null && startingShip != ship)
        {
            if (Application.isPlaying)
            {
                Destroy(startingShip.gameObject);
            }
            else
            {
                DestroyImmediate(startingShip.gameObject);
            }
        }

        Block seat = returningKing == null ? null : returningKing.SupportBlock;
        startingShip = ship;
        KingPlacement = activeKing;

        Rigidbody shipBody = ship.GetComponent<Rigidbody>();
        if (shipBody != null)
        {
            if (!shipBody.isKinematic)
            {
                shipBody.linearVelocity = Vector3.zero;
                shipBody.angularVelocity = Vector3.zero;
            }

            shipBody.useGravity = false;
            shipBody.isKinematic = true;
            shipBody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        ship.transform.SetParent(transform, true);
        ship.transform.localScale = shipScale;
        ship.transform.rotation = Quaternion.identity;
        ship.RefreshBlocks();
        CenterShipOverPlatform(ship);

        RoyaltyBoat.Water.OceanWaveBuoyancy buoyancy =
            ship.GetComponent<RoyaltyBoat.Water.OceanWaveBuoyancy>();
        if (buoyancy != null)
        {
            buoyancy.enabled = false;
        }

        RoyaltyBoat.Gameplay.BoatMovementController movement =
            ship.GetComponent<RoyaltyBoat.Gameplay.BoatMovementController>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        RoyaltyBoat.Gameplay.VoyageShipPresentation presentation =
            ship.GetComponent<RoyaltyBoat.Gameplay.VoyageShipPresentation>();
        if (presentation != null)
        {
            presentation.enabled = false;
        }

        activeKing.transform.SetParent(transform, true);
        if (returningKing != null)
        {
            activeKing.transform.localScale = kingScale;
        }

        Vector3 kingPosition = seat != null && seat.IsAlive
            ? activeKing.GetPositionOn(seat)
            : GetKingStagingPosition();
        activeKing.EnterBuildMode(kingPosition, Quaternion.identity);

        if (seat != null && seat.IsAlive)
        {
            activeKing.SetSupport(seat);
        }

        ship.RefreshBlocks();
        if (Application.isPlaying)
        {
            ConfigureCamera(Camera.main);
        }

        return activeKing;
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

        Vector3 stagingPosition = GetKingStagingPosition();
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

    private KingBuildPlacement ResolveReturningKing(KingBuildPlacement returningKing)
    {
        if (returningKing != null)
        {
            if (KingPlacement != null && KingPlacement != returningKing)
            {
                if (Application.isPlaying)
                {
                    Destroy(KingPlacement.gameObject);
                }
                else
                {
                    DestroyImmediate(KingPlacement.gameObject);
                }
            }

            returningKing.gameObject.SetActive(true);
            KingPlacement = returningKing;
            return KingPlacement;
        }

        if (KingPlacement == null)
        {
            SpawnKingForBuilding();
        }

        if (KingPlacement != null)
        {
            KingPlacement.gameObject.SetActive(true);
        }

        return KingPlacement;
    }

    private Vector3 GetKingStagingPosition()
    {
        return platform == null
            ? transform.TransformPoint(kingStagingOffset)
            : platform.position + kingStagingOffset;
    }

    private void CenterShipOverPlatform(Ship ship)
    {
        Vector3 targetCenter = platform == null ? transform.position : platform.position;
        float platformTop = targetCenter.y;
        Collider platformCollider = platform == null ? null : platform.GetComponent<Collider>();
        if (platformCollider != null)
        {
            platformTop = platformCollider.bounds.max.y;
        }

        Physics.SyncTransforms();

        Collider[] colliders = ship.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            ship.transform.position = new Vector3(
                targetCenter.x,
                platformTop + 1f,
                targetCenter.z);
            Physics.SyncTransforms();
            return;
        }

        Bounds shipBounds = colliders[0].bounds;
        for (int index = 1; index < colliders.Length; index++)
        {
            shipBounds.Encapsulate(colliders[index].bounds);
        }

        Vector3 placementOffset = new Vector3(
            targetCenter.x - shipBounds.center.x,
            platformTop - shipBounds.min.y + 0.02f,
            targetCenter.z - shipBounds.center.z);
        ship.transform.position += new Vector3(
            placementOffset.x,
            placementOffset.y,
            placementOffset.z);
        Physics.SyncTransforms();
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
