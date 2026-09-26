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

    private Camera dragCamera;
    private Transform draggedBlock;
    private Block draggedBlockComponent;
    private Ship draggedShip;
    private Ship snappingShip;
    private Rigidbody draggedBody;
    private Plane dragPlane;
    private Vector3 pointerOffset;
    private bool previousKinematic;
    private bool previousUseGravity;

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

    private void Awake()
    {
        dragCamera = GetComponent<Camera>();
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

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                raycastDistance,
                draggableLayers,
                QueryTriggerInteraction.Ignore))
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

        float dragHeight = snappingShip != null && snappingShip.CoreBlock != null
            ? snappingShip.CoreBlock.transform.position.y
            : draggedBlock.position.y;

        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));

        if (dragPlane.Raycast(ray, out float distance))
        {
            pointerOffset = draggedBlock.position - ray.GetPoint(distance);
        }
        else
        {
            pointerOffset = Vector3.zero;
        }

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
        Vector3 gridOrigin = snappingShip != null && snappingShip.CoreBlock != null
            ? snappingShip.CoreBlock.transform.position
            : Vector3.zero;

        float activeGridSize = snappingShip != null
            ? snappingShip.AttachmentGridSize
            : gridSize;

        float snappedHeight = snappingShip != null
            ? gridOrigin.y
            : draggedBlock.position.y;

        Vector3 snappedPosition = new Vector3(
            gridOrigin.x
                + Mathf.Round((targetPosition.x - gridOrigin.x) / activeGridSize)
                * activeGridSize,
            snappedHeight,
            gridOrigin.z
                + Mathf.Round((targetPosition.z - gridOrigin.z) / activeGridSize)
                * activeGridSize);

        draggedBlock.position = snappedPosition;
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

            if (!targetShip.IsAttachmentPositionAvailable(
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
