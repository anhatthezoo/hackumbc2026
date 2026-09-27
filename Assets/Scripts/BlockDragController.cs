using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class BlockDragController : MonoBehaviour
{
    [Header("Drag Settings")]
    [SerializeField] private bool draggingEnabled = true;
    [SerializeField, Min(0.01f)] private float gridSize = Ship.DefaultAttachmentGridSize;
    [SerializeField, Min(0.1f)] private float raycastDistance = 100f;
    [SerializeField] private LayerMask draggableLayers = ~0;

    [Header("Placement Bounds")]
    [SerializeField] private Transform placementCenter;
    [SerializeField, Min(0f)] private float maximumPlacementDistance = 9f;

    [Header("Drag Highlight")]
    [SerializeField] private Color dragHighlightColor = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField, Min(0.001f)] private float dragHighlightWidth = 0.045f;

    private Camera dragCamera;
    private AdaptiveBuildGrid buildGrid;
    private Transform draggedBlock;
    private Block draggedBlockComponent;
    private Ship draggedShip;
    private Ship snappingShip;
    private Rigidbody draggedBody;
    private Plane dragPlane;
    private Vector3 pointerOffset;
    private bool previousKinematic;
    private bool previousUseGravity;
    private readonly List<BlockDragOutline> activeHighlights =
        new List<BlockDragOutline>();

    public bool DraggingEnabled
    {
        get => draggingEnabled;
        set
        {
            draggingEnabled = value;

            if (!draggingEnabled)
            {
                EndDrag();
            }
        }
    }

    public Transform PlacementCenter
    {
        get => placementCenter;
        set => placementCenter = value;
    }

    private void Awake()
    {
        dragCamera = GetComponent<Camera>();
        buildGrid = Object.FindAnyObjectByType<AdaptiveBuildGrid>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (!draggingEnabled || mouse == null)
        {
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginDrag(pointerPosition);
        }

        if (draggedBlock != null && mouse.leftButton.isPressed)
        {
            Drag(pointerPosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
    }

    private void OnDisable()
    {
        EndDrag();
    }

    private void BeginDrag(Vector2 pointerPosition)
    {
        Ray ray = dragCamera.ScreenPointToRay(pointerPosition);

        if (!TryGetClosestBlockHit(ray, out RaycastHit hit))
        {
            return;
        }

        Block selectedBlock = hit.collider.GetComponentInParent<Block>();

        if (selectedBlock == null || !selectedBlock.CompareTag("Block"))
        {
            return;
        }

        Ship shipOnSelectedObject = selectedBlock.GetComponent<Ship>();

        if (shipOnSelectedObject != null)
        {
            draggedShip = shipOnSelectedObject;
            draggedBlockComponent = selectedBlock;
            draggedBlock = shipOnSelectedObject.transform;
        }
        else
        {
            Ship owningShip = selectedBlock.GetComponentInParent<Ship>();
            snappingShip = owningShip != null
                ? owningShip
                : FindClosestShip(selectedBlock.transform.position);

            if (owningShip != null)
            {
                owningShip.DetachBlock(selectedBlock);
            }

            draggedBlockComponent = selectedBlock;
            draggedBlock = selectedBlock.transform;
        }

        ConfigureDragPlane(ray);

        draggedBody = draggedBlock.GetComponent<Rigidbody>();

        if (draggedBody != null)
        {
            previousKinematic = draggedBody.isKinematic;
            previousUseGravity = draggedBody.useGravity;
            draggedBody.linearVelocity = Vector3.zero;
            draggedBody.angularVelocity = Vector3.zero;
            draggedBody.useGravity = false;
            draggedBody.isKinematic = true;
        }

        Drag(pointerPosition);
        ShowDragHighlights();
    }

    private bool TryGetClosestBlockHit(Ray ray, out RaycastHit blockHit)
    {
        blockHit = default;
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            raycastDistance,
            draggableLayers,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        bool foundBlock = false;

        foreach (RaycastHit hit in hits)
        {
            Block block = hit.collider.GetComponentInParent<Block>();

            if (block == null
                || !block.CompareTag("Block")
                || hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            blockHit = hit;
            foundBlock = true;
        }

        return foundBlock;
    }

    private void Drag(Vector2 pointerPosition)
    {
        Ray ray = dragCamera.ScreenPointToRay(pointerPosition);

        if (draggedShip == null
            && TryGetSurfacePlacement(ray, out Vector3 surfacePosition))
        {
            draggedBlock.position = surfacePosition;
            draggedBlock.rotation = snappingShip.transform.rotation;
            return;
        }

        if (!dragPlane.Raycast(ray, out float distance))
        {
            return;
        }

        Vector3 targetPosition = ray.GetPoint(distance) + pointerOffset;
        float activeGridSize = snappingShip != null
            ? snappingShip.AttachmentGridSize
            : gridSize;

        Vector3 snappedPosition = SnapToGrid(targetPosition, activeGridSize);

        draggedBlock.position = ConstrainToPlacementBounds(
            snappedPosition,
            activeGridSize);
    }

    private void ConfigureDragPlane(Ray pointerRay)
    {
        Vector3 planeNormal = GetDragPlaneNormal();
        Vector3 planePoint = draggedBlock.position;

        if (draggedShip == null
            && snappingShip != null
            && snappingShip.CoreBlock != null)
        {
            planePoint = snappingShip.CoreBlock.transform.position;
        }

        dragPlane = new Plane(planeNormal, planePoint);

        if (dragPlane.Raycast(pointerRay, out float distance))
        {
            pointerOffset = Vector3.ProjectOnPlane(
                draggedBlock.position - pointerRay.GetPoint(distance),
                planeNormal);
        }
        else
        {
            pointerOffset = Vector3.zero;
        }
    }

    private Vector3 GetDragPlaneNormal()
    {
        if (draggedShip != null)
        {
            return Vector3.up;
        }

        if (buildGrid == null)
        {
            buildGrid = Object.FindAnyObjectByType<AdaptiveBuildGrid>();
        }

        if (buildGrid != null)
        {
            return buildGrid.transform.forward.normalized;
        }

        Vector3 cameraForward = dragCamera.transform.forward;
        Vector3 absoluteForward = new Vector3(
            Mathf.Abs(cameraForward.x),
            Mathf.Abs(cameraForward.y),
            Mathf.Abs(cameraForward.z));

        if (absoluteForward.y >= absoluteForward.x
            && absoluteForward.y >= absoluteForward.z)
        {
            return Vector3.up;
        }

        return absoluteForward.x >= absoluteForward.z
            ? Vector3.right
            : Vector3.forward;
    }

    private Vector3 SnapToGrid(Vector3 targetPosition, float snapSize)
    {
        if (draggedShip != null)
        {
            return new Vector3(
                Mathf.Round(targetPosition.x / snapSize) * snapSize,
                draggedBlock.position.y,
                Mathf.Round(targetPosition.z / snapSize) * snapSize);
        }

        if (snappingShip == null || snappingShip.CoreBlock == null)
        {
            return new Vector3(
                Mathf.Round(targetPosition.x / snapSize) * snapSize,
                Mathf.Round(targetPosition.y / snapSize) * snapSize,
                Mathf.Round(targetPosition.z / snapSize) * snapSize);
        }

        Transform shipTransform = snappingShip.transform;
        Vector3 gridOrigin = snappingShip.CoreBlock.transform.position;
        Vector3 relativePosition = targetPosition - gridOrigin;

        float snappedRight = Mathf.Round(
            Vector3.Dot(relativePosition, shipTransform.right) / snapSize)
            * snapSize;
        float snappedUp = Mathf.Round(
            Vector3.Dot(relativePosition, shipTransform.up) / snapSize)
            * snapSize;
        float snappedForward = Mathf.Round(
            Vector3.Dot(relativePosition, shipTransform.forward) / snapSize)
            * snapSize;

        return gridOrigin
            + shipTransform.right * snappedRight
            + shipTransform.up * snappedUp
            + shipTransform.forward * snappedForward;
    }

    private bool TryGetSurfacePlacement(Ray ray, out Vector3 placementPosition)
    {
        placementPosition = default;
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            raycastDistance,
            draggableLayers,
            QueryTriggerInteraction.Ignore);

        float closestHitDistance = float.PositiveInfinity;
        Ship bestShip = null;
        Vector3 bestPosition = default;

        foreach (RaycastHit hit in hits)
        {
            if (draggedBlock != null
                && hit.collider.transform.IsChildOf(draggedBlock))
            {
                continue;
            }

            Block targetBlock = hit.collider.GetComponentInParent<Block>();

            if (targetBlock == null
                || targetBlock == draggedBlockComponent
                || !targetBlock.CompareTag("Block"))
            {
                continue;
            }

            Ship targetShip = targetBlock.GetComponentInParent<Ship>();

            if (targetShip == null || hit.distance >= closestHitDistance)
            {
                continue;
            }

            Vector3 faceDirection = GetClosestFaceDirection(
                hit.normal,
                targetShip.transform);
            Vector3 possiblePosition = targetBlock.transform.position
                + faceDirection * targetShip.AttachmentGridSize;

            if (!IsWithinPlacementBounds(possiblePosition)
                || !targetShip.IsAttachmentPositionAvailable(
                    possiblePosition,
                    draggedBlockComponent))
            {
                continue;
            }

            closestHitDistance = hit.distance;
            bestShip = targetShip;
            bestPosition = possiblePosition;
        }

        if (bestShip == null)
        {
            return false;
        }

        snappingShip = bestShip;
        placementPosition = bestPosition;
        return true;
    }

    private Vector3 GetClosestFaceDirection(Vector3 surfaceNormal, Transform shipTransform)
    {
        Vector3[] directions =
        {
            shipTransform.right,
            -shipTransform.right,
            shipTransform.up,
            -shipTransform.up,
            shipTransform.forward,
            -shipTransform.forward
        };

        Vector3 closestDirection = directions[0];
        float largestDot = float.NegativeInfinity;

        foreach (Vector3 direction in directions)
        {
            float dot = Vector3.Dot(surfaceNormal, direction);

            if (dot > largestDot)
            {
                largestDot = dot;
                closestDirection = direction;
            }
        }

        return closestDirection;
    }

    private void EndDrag()
    {
        HideDragHighlights();

        if (draggedBody != null)
        {
            draggedBody.isKinematic = previousKinematic;
            draggedBody.useGravity = previousUseGravity;
            draggedBody.linearVelocity = Vector3.zero;
            draggedBody.angularVelocity = Vector3.zero;
        }

        if (draggedShip != null)
        {
            draggedShip.AttachTouchingBlocks();
        }
        else if (draggedBlockComponent != null)
        {
            if (snappingShip != null
                && snappingShip.TryAttachBlock(draggedBlockComponent))
            {
                snappingShip.AttachTouchingBlocks();
            }
            else
            {
                Ship[] ships = Object.FindObjectsByType<Ship>(FindObjectsInactive.Exclude);

                foreach (Ship ship in ships)
                {
                    if (ship.TryAttachBlock(draggedBlockComponent))
                    {
                        ship.AttachTouchingBlocks();
                        break;
                    }
                }
            }
        }

        draggedBlock = null;
        draggedBlockComponent = null;
        draggedShip = null;
        snappingShip = null;
        draggedBody = null;
    }

    private bool IsWithinPlacementBounds(Vector3 position)
    {
        if (placementCenter == null || maximumPlacementDistance <= 0f)
        {
            return true;
        }

        Vector2 horizontalOffset = new Vector2(
            position.x - placementCenter.position.x,
            position.z - placementCenter.position.z);

        return horizontalOffset.sqrMagnitude
            <= maximumPlacementDistance * maximumPlacementDistance;
    }

    private Vector3 ConstrainToPlacementBounds(Vector3 position, float snapSize)
    {
        if (placementCenter == null
            || maximumPlacementDistance <= 0f
            || IsWithinPlacementBounds(position))
        {
            return position;
        }

        Vector3 center = placementCenter.position;
        Vector3 constrainedPosition = position;

        for (int step = 0; step < 64 && !IsWithinPlacementBounds(constrainedPosition); step++)
        {
            float offsetX = constrainedPosition.x - center.x;
            float offsetZ = constrainedPosition.z - center.z;

            if (Mathf.Abs(offsetX) >= Mathf.Abs(offsetZ))
            {
                constrainedPosition.x -= Mathf.Sign(offsetX) * snapSize;
            }
            else
            {
                constrainedPosition.z -= Mathf.Sign(offsetZ) * snapSize;
            }
        }

        return constrainedPosition;
    }

    private void ShowDragHighlights()
    {
        HideDragHighlights();

        Block[] highlightedBlocks = draggedShip != null
            ? draggedShip.GetComponentsInChildren<Block>(true)
            : new[] { draggedBlockComponent };

        foreach (Block block in highlightedBlocks)
        {
            if (block == null)
            {
                continue;
            }

            BlockDragOutline outline =
                block.gameObject.AddComponent<BlockDragOutline>();
            outline.Configure(dragHighlightColor, dragHighlightWidth);
            activeHighlights.Add(outline);
        }
    }

    private void HideDragHighlights()
    {
        foreach (BlockDragOutline outline in activeHighlights)
        {
            if (outline != null)
            {
                Destroy(outline);
            }
        }

        activeHighlights.Clear();
    }

    private Ship FindClosestShip(Vector3 position)
    {
        Ship closestShip = null;
        float closestDistanceSquared = float.PositiveInfinity;
        Ship[] ships = Object.FindObjectsByType<Ship>(FindObjectsInactive.Exclude);

        foreach (Ship ship in ships)
        {
            float distanceSquared = (ship.transform.position - position).sqrMagnitude;

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestShip = ship;
            }
        }

        return closestShip;
    }
}
