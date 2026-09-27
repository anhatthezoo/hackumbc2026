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
    public KingBuildPlacement KingPlacement { get; private set; }

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

    public void AdoptReturningShip(
        Ship ship,
        KingBuildPlacement king,
        Vector3 shipScale,
        Vector3 kingScale)
    {
        if (ship == null || king == null)
        {
            return;
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

        Block seat = king.SupportBlock;
        startingShip = ship;
        KingPlacement = king;

        ship.transform.SetParent(transform, true);
        ship.transform.localScale = shipScale;
        ship.transform.rotation = Quaternion.identity;
        CenterShipOverPlatform(ship);

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

        king.transform.SetParent(transform, true);
        king.transform.localScale = kingScale;
        Vector3 kingPosition = seat != null && seat.IsAlive
            ? king.GetPositionOn(seat)
            : GetKingStagingPosition();
        king.EnterBuildMode(kingPosition, Quaternion.identity);

        if (seat != null && seat.IsAlive)
        {
            king.SetSupport(seat);
        }

        ship.RefreshBlocks();
        if (Application.isPlaying)
        {
            ConfigureCamera(Camera.main);
        }
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

    private Vector3 GetKingStagingPosition()
    {
        return platform == null
            ? transform.TransformPoint(kingStagingOffset)
            : platform.position + kingStagingOffset;
    }

    private void CenterShipOverPlatform(Ship ship)
    {
        Vector3 targetCenter = platform == null ? transform.position : platform.position;
        ship.transform.position = targetCenter;

        Collider[] colliders = ship.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            ship.transform.position += Vector3.up;
            return;
        }

        Bounds shipBounds = colliders[0].bounds;
        for (int index = 1; index < colliders.Length; index++)
        {
            shipBounds.Encapsulate(colliders[index].bounds);
        }

        float platformTop = targetCenter.y;
        Collider platformCollider = platform == null ? null : platform.GetComponent<Collider>();
        if (platformCollider != null)
        {
            platformTop = platformCollider.bounds.max.y;
        }

        ship.transform.position += new Vector3(
            targetCenter.x - shipBounds.center.x,
            platformTop - shipBounds.min.y + 0.02f,
            targetCenter.z - shipBounds.center.z);
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
