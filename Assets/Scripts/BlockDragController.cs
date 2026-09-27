using System.Collections.Generic;
using RoyaltyBoat.King;
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
    private Transform draggedBlock;
    private Block draggedBlockComponent;
    private KingBuildPlacement draggedKing;
    private Block kingSupportCandidate;
    private Ship draggedShip;
    private Ship snappingShip;
    private Rigidbody draggedBody;
    private Plane dragPlane;
    private Vector3 pointerOffset;
    private bool previousKinematic;
    private bool previousUseGravity;
    private Transform selectedMoveRoot;
    private Block selectedBlockComponent;
    private KingBuildPlacement selectedKing;
    private GameObject moveGizmo;
    private readonly Dictionary<Collider, Vector3> gizmoHandles =
        new Dictionary<Collider, Vector3>();
    private readonly List<Material> gizmoMaterials = new List<Material>();
    private readonly List<Mesh> gizmoMeshes = new List<Mesh>();
    private bool axisDragging;
    private Vector3 activeDragAxis;
    private Vector3 axisDragStartPosition;
    private float axisPointerStart;
    private Plane axisDragPlane;
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
            if (axisDragging)
            {
                DragAlongAxis(dragCamera.ScreenPointToRay(pointerPosition));
            }
            else
            {
                Drag(pointerPosition);
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && keyboard.rKey.wasPressedThisFrame
            && draggedBlock == null)
        {
            RotateSelectionClockwise();
        }

        UpdateMoveGizmoPosition();
    }

    private void OnDisable()
    {
        EndDrag();
        ClearSelection();
    }

    private void BeginDrag(Vector2 pointerPosition)
    {
        Ray ray = dragCamera.ScreenPointToRay(pointerPosition);

        if (TryGetGizmoHandleHit(ray, out Vector3 gizmoAxis))
        {
            BeginAxisDrag(ray, gizmoAxis);
            return;
        }

        if (!TryGetClosestDraggableHit(
                ray,
                out RaycastHit hit,
                out Block selectedBlock,
                out KingBuildPlacement selectedKingPlacement))
        {
            ClearSelection();
            return;
        }

        if (selectedKingPlacement != null)
        {
            ConfigureDraggedTarget(selectedKingPlacement);
            SelectMoveTarget(null, selectedKingPlacement, draggedBlock);
        }
        else
        {
            ConfigureDraggedTarget(selectedBlock);
            SelectMoveTarget(selectedBlock, null, draggedBlock);
        }

        float dragHeight = snappingShip != null && snappingShip.AnchorBlock != null
            ? snappingShip.AnchorBlock.transform.position.y
            : draggedBlock.position.y;

        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));

        if (draggedShip == null)
        {
            // Individual blocks snap by their center so the selected tile
            // remains directly under the mouse regardless of click location.
            pointerOffset = Vector3.zero;
        }
        else if (dragPlane.Raycast(ray, out float distance))
        {
            pointerOffset = draggedBlock.position - ray.GetPoint(distance);
        }
        else
        {
            pointerOffset = Vector3.zero;
        }

        PrepareDraggedBody();
        Drag(pointerPosition);
        ShowDragHighlights();
    }

    private void ConfigureDraggedTarget(Block selectedBlock)
    {
        draggedBlock = null;
        draggedBlockComponent = null;
        draggedKing = null;
        kingSupportCandidate = null;
        draggedShip = null;
        snappingShip = null;

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
    }

    private void ConfigureDraggedTarget(KingBuildPlacement king)
    {
        draggedBlock = king.transform;
        draggedBlockComponent = null;
        draggedKing = king;
        kingSupportCandidate = king.SupportBlock;
        draggedShip = null;
        snappingShip = king.SupportingShip != null
            ? king.SupportingShip
            : FindClosestShip(king.transform.position);
        king.ClearSupport();
    }

    private void PrepareDraggedBody()
    {
        draggedBody = draggedBlock.GetComponent<Rigidbody>();

        if (draggedBody != null)
        {
            previousKinematic = draggedBody.isKinematic;
            previousUseGravity = draggedBody.useGravity;
            if (!draggedBody.isKinematic)
            {
                draggedBody.linearVelocity = Vector3.zero;
                draggedBody.angularVelocity = Vector3.zero;
            }
            draggedBody.useGravity = false;
            draggedBody.isKinematic = true;
        }
    }

    private bool TryGetClosestDraggableHit(
        Ray ray,
        out RaycastHit draggableHit,
        out Block selectedBlock,
        out KingBuildPlacement selectedKingPlacement)
    {
        draggableHit = default;
        selectedBlock = null;
        selectedKingPlacement = null;
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            raycastDistance,
            draggableLayers,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        bool foundDraggable = false;

        foreach (RaycastHit hit in hits)
        {
            Block block = hit.collider.GetComponentInParent<Block>();
            KingBuildPlacement king =
                hit.collider.GetComponentInParent<KingBuildPlacement>();

            if ((king == null && (block == null || !block.CompareTag("Block")))
                || hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            draggableHit = hit;
            selectedBlock = block;
            selectedKingPlacement = king;
            foundDraggable = true;
        }

        return foundDraggable;
    }

    private void Drag(Vector2 pointerPosition)
    {
        Ray ray = dragCamera.ScreenPointToRay(pointerPosition);

        if (draggedShip == null
            && TryGetSurfacePlacement(ray, out Vector3 surfacePosition))
        {
            TryApplyPlacement(
                surfacePosition,
                snappingShip.transform.rotation);
            return;
        }

        if (!dragPlane.Raycast(ray, out float distance))
        {
            return;
        }

        Vector3 targetPosition = ray.GetPoint(distance) + pointerOffset;
        Vector3 gridOrigin = GetGridOrigin();
        float activeGridSize = GetActiveGridSize();

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

        TryApplyPlacement(
            ConstrainToPlacementBounds(snappedPosition, activeGridSize),
            draggedBlock.rotation);
    }

    private bool TryGetGizmoHandleHit(Ray ray, out Vector3 axis)
    {
        axis = default;
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            raycastDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        bool foundHandle = false;

        foreach (RaycastHit hit in hits)
        {
            if (!gizmoHandles.TryGetValue(hit.collider, out Vector3 hitAxis)
                || hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            axis = hitAxis;
            foundHandle = true;
        }

        return foundHandle;
    }

    private void BeginAxisDrag(Ray pointerRay, Vector3 axis)
    {
        if ((selectedBlockComponent == null && selectedKing == null)
            || selectedMoveRoot == null)
        {
            return;
        }

        if (selectedKing != null)
        {
            ConfigureDraggedTarget(selectedKing);
        }
        else
        {
            ConfigureDraggedTarget(selectedBlockComponent);
        }
        PrepareDraggedBody();
        axisDragging = true;
        activeDragAxis = axis.normalized;
        axisDragStartPosition = draggedBlock.position;

        Vector3 viewDirection =
            (axisDragStartPosition - dragCamera.transform.position).normalized;
        Vector3 side = Vector3.Cross(activeDragAxis, viewDirection);
        Vector3 planeNormal = Vector3.Cross(activeDragAxis, side).normalized;

        if (planeNormal.sqrMagnitude < 0.001f)
        {
            planeNormal = Vector3.Cross(activeDragAxis, dragCamera.transform.up).normalized;
        }

        axisDragPlane = new Plane(planeNormal, axisDragStartPosition);
        axisPointerStart = 0f;

        if (axisDragPlane.Raycast(pointerRay, out float distance))
        {
            axisPointerStart = Vector3.Dot(
                pointerRay.GetPoint(distance) - axisDragStartPosition,
                activeDragAxis);
        }

        ShowDragHighlights();
    }

    private void DragAlongAxis(Ray pointerRay)
    {
        if (!axisDragPlane.Raycast(pointerRay, out float distance))
        {
            return;
        }

        float pointerPosition = Vector3.Dot(
            pointerRay.GetPoint(distance) - axisDragStartPosition,
            activeDragAxis);
        float gridStep = GetActiveGridSize();
        float snappedDistance = Mathf.Round(
            (pointerPosition - axisPointerStart) / gridStep) * gridStep;
        Vector3 targetPosition = axisDragStartPosition
            + activeDragAxis * snappedDistance;

        TryApplyPlacement(
            ConstrainToPlacementBounds(targetPosition, gridStep),
            draggedBlock.rotation);
    }

    private Vector3 GetGridOrigin()
    {
        if (snappingShip == null)
        {
            return Vector3.zero;
        }

        return snappingShip.AnchorBlock != null
            ? snappingShip.AnchorBlock.transform.position
            : snappingShip.transform.position;
    }

    private float GetActiveGridSize()
    {
        return snappingShip != null
            ? snappingShip.AttachmentGridSize
            : gridSize;
    }

    private bool TryApplyPlacement(Vector3 position, Quaternion rotation)
    {
        if (draggedBlock == null)
        {
            return false;
        }

        Vector3 previousPosition = draggedBlock.position;
        Quaternion previousRotation = draggedBlock.rotation;
        draggedBlock.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();

        if (IsPlacementClear(draggedBlock))
        {
            return true;
        }

        draggedBlock.SetPositionAndRotation(previousPosition, previousRotation);
        Physics.SyncTransforms();
        return false;
    }

    public bool IsPlacementClear(Transform movingRoot)
    {
        if (movingRoot == null)
        {
            return false;
        }

        Collider[] movingColliders =
            movingRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider movingCollider in movingColliders)
        {
            if (movingCollider == null || !movingCollider.enabled)
            {
                continue;
            }

            Collider[] nearbyColliders = Physics.OverlapBox(
                movingCollider.bounds.center,
                movingCollider.bounds.extents + Vector3.one * 0.02f,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Collider otherCollider in nearbyColliders)
            {
                if (otherCollider == null
                    || otherCollider == movingCollider
                    || !otherCollider.enabled
                    || otherCollider.isTrigger
                    || otherCollider.transform.IsChildOf(movingRoot)
                    || otherCollider.GetComponentInParent<Block>() == null)
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                        movingCollider,
                        movingCollider.transform.position,
                        movingCollider.transform.rotation,
                        otherCollider,
                        otherCollider.transform.position,
                        otherCollider.transform.rotation,
                        out _,
                        out float penetrationDistance)
                    && penetrationDistance > 0.01f)
                {
                    return false;
                }
            }
        }

        return true;
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

            Vector3 possiblePosition;

            if (draggedKing != null)
            {
                possiblePosition = draggedKing.GetPositionOn(targetBlock);
            }
            else
            {
                Vector3 faceDirection = GetClosestFaceDirection(
                    hit.normal,
                    targetShip.transform);
                possiblePosition = targetBlock.transform.position
                    + faceDirection * targetShip.AttachmentGridSize;
            }

            if (!IsWithinPlacementBounds(possiblePosition)
                || (draggedKing == null
                    && !targetShip.IsAttachmentPositionAvailable(
                        possiblePosition,
                        draggedBlockComponent)))
            {
                continue;
            }

            closestHitDistance = hit.distance;
            bestShip = targetShip;
            bestPosition = possiblePosition;
            kingSupportCandidate = draggedKing == null ? null : targetBlock;
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
            if (!previousKinematic)
            {
                draggedBody.linearVelocity = Vector3.zero;
                draggedBody.angularVelocity = Vector3.zero;
            }
        }

        if (draggedShip != null)
        {
            draggedShip.AttachTouchingBlocks();
        }
        else if (draggedKing != null)
        {
            Block support = FindKingSupportAtCurrentPosition();
            if (support != null)
            {
                draggedKing.SetSupport(support);
            }
            else
            {
                draggedKing.ClearSupport();
            }
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
        draggedKing = null;
        kingSupportCandidate = null;
        draggedShip = null;
        snappingShip = null;
        draggedBody = null;
        axisDragging = false;
        activeDragAxis = Vector3.zero;
        UpdateMoveGizmoPosition();
    }

    public void SelectBlock(Block block)
    {
        if (block == null)
        {
            ClearSelection();
            return;
        }

        Ship shipOnBlock = block.GetComponent<Ship>();
        SelectMoveTarget(
            block,
            null,
            shipOnBlock != null ? shipOnBlock.transform : block.transform);
    }

    public void SelectKing(KingBuildPlacement king)
    {
        if (king == null)
        {
            ClearSelection();
            return;
        }

        SelectMoveTarget(null, king, king.transform);
    }

    public bool RotateSelectionClockwise()
    {
        if (selectedMoveRoot == null || draggedBlock != null)
        {
            return false;
        }

        Quaternion previousRotation = selectedMoveRoot.rotation;
        Quaternion rotated = Quaternion.AngleAxis(90f, Vector3.up)
            * previousRotation;
        selectedMoveRoot.rotation = rotated;
        Physics.SyncTransforms();

        if (IsPlacementClear(selectedMoveRoot))
        {
            return true;
        }

        selectedMoveRoot.rotation = previousRotation;
        Physics.SyncTransforms();
        return false;
    }

    private void SelectMoveTarget(
        Block block,
        KingBuildPlacement king,
        Transform moveRoot)
    {
        selectedBlockComponent = block;
        selectedKing = king;
        selectedMoveRoot = moveRoot;
        CreateMoveGizmo();
        UpdateMoveGizmoPosition();
    }

    private void ClearSelection()
    {
        selectedBlockComponent = null;
        selectedKing = null;
        selectedMoveRoot = null;
        DestroyMoveGizmo();
    }

    private Block FindKingSupportAtCurrentPosition()
    {
        if (draggedKing == null)
        {
            return null;
        }

        if (kingSupportCandidate != null
            && (draggedKing.GetPositionOn(kingSupportCandidate)
                - draggedKing.transform.position).sqrMagnitude <= 0.04f)
        {
            return kingSupportCandidate;
        }

        Block closest = null;
        float closestDistanceSquared = 0.04f;

        foreach (Block block in Object.FindObjectsByType<Block>(FindObjectsInactive.Exclude))
        {
            if (block == null || !block.IsAlive)
            {
                continue;
            }

            float distanceSquared =
                (draggedKing.GetPositionOn(block) - draggedKing.transform.position)
                .sqrMagnitude;
            if (distanceSquared <= closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closest = block;
            }
        }

        return closest;
    }

    private void CreateMoveGizmo()
    {
        DestroyMoveGizmo();
        moveGizmo = new GameObject("Block Move Arrows");
        moveGizmo.hideFlags = HideFlags.DontSave;

        CreateAxisArrow(Vector3.right, new Color(0.92f, 0.22f, 0.18f));
        CreateAxisArrow(Vector3.up, new Color(0.28f, 0.82f, 0.3f));
        CreateAxisArrow(Vector3.forward, new Color(0.2f, 0.48f, 0.96f));
    }

    private void CreateAxisArrow(Vector3 axis, Color color)
    {
        Material material = CreateGizmoMaterial(color);
        gizmoMaterials.Add(material);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = $"{axis} Move Handle";
        shaft.transform.SetParent(moveGizmo.transform, false);
        shaft.transform.localPosition = axis * 0.65f;
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
        shaft.transform.localScale = new Vector3(0.11f, 0.65f, 0.11f);
        shaft.GetComponent<Renderer>().sharedMaterial = material;
        gizmoHandles[shaft.GetComponent<Collider>()] = axis;

        GameObject head = new GameObject($"{axis} Arrow Head");
        head.transform.SetParent(moveGizmo.transform, false);
        head.transform.localPosition = axis * 1.52f;
        head.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);

        Mesh arrowHeadMesh = CreateConeMesh();
        gizmoMeshes.Add(arrowHeadMesh);
        MeshFilter filter = head.AddComponent<MeshFilter>();
        filter.sharedMesh = arrowHeadMesh;
        MeshRenderer renderer = head.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        MeshCollider collider = head.AddComponent<MeshCollider>();
        collider.sharedMesh = arrowHeadMesh;
        collider.convex = true;
        gizmoHandles[collider] = axis;
    }

    private void DestroyMoveGizmo()
    {
        gizmoHandles.Clear();

        if (moveGizmo != null)
        {
            Destroy(moveGizmo);
            moveGizmo = null;
        }

        foreach (Material material in gizmoMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        foreach (Mesh mesh in gizmoMeshes)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        gizmoMaterials.Clear();
        gizmoMeshes.Clear();
    }

    private static Mesh CreateConeMesh()
    {
        const int sides = 12;
        const float radius = 0.24f;
        const float baseY = -0.22f;
        const float tipY = 0.32f;
        var vertices = new Vector3[sides + 2];
        var triangles = new int[sides * 6];
        vertices[0] = new Vector3(0f, tipY, 0f);
        vertices[1] = new Vector3(0f, baseY, 0f);

        for (int side = 0; side < sides; side++)
        {
            float angle = side * Mathf.PI * 2f / sides;
            vertices[side + 2] = new Vector3(
                Mathf.Cos(angle) * radius,
                baseY,
                Mathf.Sin(angle) * radius);

            int next = (side + 1) % sides;
            int triangle = side * 6;
            triangles[triangle] = 0;
            triangles[triangle + 1] = side + 2;
            triangles[triangle + 2] = next + 2;
            triangles[triangle + 3] = 1;
            triangles[triangle + 4] = next + 2;
            triangles[triangle + 5] = side + 2;
        }

        Mesh mesh = new Mesh { name = "Runtime Move Arrow Head" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateGizmoMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader)
        {
            name = "Runtime Move Arrow Material",
            color = color
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    private void UpdateMoveGizmoPosition()
    {
        if (moveGizmo == null || selectedMoveRoot == null)
        {
            return;
        }

        moveGizmo.transform.position = selectedMoveRoot.position;
        moveGizmo.transform.rotation = Quaternion.identity;
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

        if (draggedKing != null)
        {
            BlockDragOutline kingOutline =
                draggedKing.gameObject.AddComponent<BlockDragOutline>();
            kingOutline.Configure(dragHighlightColor, dragHighlightWidth);
            activeHighlights.Add(kingOutline);
            return;
        }

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
