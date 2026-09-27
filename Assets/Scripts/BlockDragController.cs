using System.Collections.Generic;
using RoyaltyBoat.King;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class BlockDragController : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private bool draggingEnabled = true;
    [SerializeField, Min(0.1f)] private float raycastDistance = 100f;
    [SerializeField] private LayerMask draggableLayers = ~0;
    [SerializeField, Min(1f)] private float pointerDragThreshold = 7f;

    [Header("Grid Placement")]
    [SerializeField, Min(0.01f)] private float gridSize = Ship.DefaultAttachmentGridSize;
    [SerializeField] private Transform placementCenter;
    [SerializeField, Min(0f)] private float maximumPlacementDistance = 9f;
    [SerializeField, Min(1)] private int maximumBuildLayers = 8;

    [Header("Placement Feedback")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.73f, 0.22f, 1f);
    [SerializeField] private Color validColor = new Color(0.25f, 0.95f, 0.48f, 1f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField, Min(0.001f)] private float outlineWidth = 0.045f;

    private Camera buildCamera;
    private Block selectedBlock;
    private KingBuildPlacement selectedKing;
    private Transform selectedTransform;
    private BlockDragOutline selectionOutline;
    private bool pointerArmed;
    private bool placementActive;
    private bool placementValid;
    private Vector2 pointerDownPosition;
    private Vector3 pointerOffset;
    private float activeLayerHeight;
    private PlacementSnapshot snapshot;
    private Ship targetShip;
    private Rigidbody movingBody;
    private bool previousKinematic;
    private bool previousUseGravity;
    private GameObject moveGizmo;
    private readonly Dictionary<Collider, Vector3Int> gizmoDirections = new();
    private readonly List<Material> gizmoMaterials = new();
    private readonly List<Mesh> gizmoMeshes = new();

    private sealed class PlacementSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Transform Parent;
        public Ship Ship;
        public Block KingSupport;
    }

    public bool DraggingEnabled
    {
        get => draggingEnabled;
        set
        {
            draggingEnabled = value;
            if (!value) CancelActivePlacement();
        }
    }

    public Transform PlacementCenter
    {
        get => placementCenter;
        set => placementCenter = value;
    }

    public Transform SelectedTransform => selectedTransform;
    public bool HasActiveSelection => selectedTransform != null;
    public bool IsCurrentPlacementValid => !placementActive || placementValid;

    private void Awake() => buildCamera = GetComponent<Camera>();

    private void Update()
    {
        if (!draggingEnabled) return;
        HandleKeyboard();
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            UpdateMoveGizmo();
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame) HandlePointerPressed(pointerPosition);
        if (pointerArmed && mouse.leftButton.isPressed)
        {
            if (!placementActive && Vector2.Distance(pointerDownPosition, pointerPosition) >= pointerDragThreshold)
                BeginPlacement();
            if (placementActive) UpdatePointerPlacement(pointerPosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (placementActive) FinishPlacement();
            pointerArmed = false;
        }

        UpdateMoveGizmo();
    }

    private void OnDisable()
    {
        CancelActivePlacement();
        ClearSelection();
    }

    private void HandlePointerPressed(Vector2 pointerPosition)
    {
        Ray ray = buildCamera.ScreenPointToRay(pointerPosition);
        if (TryGetGizmoDirection(ray, out Vector3Int direction))
        {
            TryNudgeSelection(direction);
            pointerArmed = false;
            return;
        }

        if (!TryGetClosestDraggable(ray, out Block block, out KingBuildPlacement king))
        {
            ClearSelection();
            pointerArmed = false;
            return;
        }

        if (king != null) SelectKing(king); else SelectBlock(block);
        pointerDownPosition = pointerPosition;
        activeLayerHeight = selectedTransform.position.y;
        Plane plane = new(Vector3.up, new Vector3(0f, activeLayerHeight, 0f));
        pointerOffset = Vector3.zero;
        if (plane.Raycast(ray, out float distance))
        {
            pointerOffset = selectedTransform.position - ray.GetPoint(distance);
            pointerOffset.y = 0f;
        }
        pointerArmed = true;
    }

    private void HandleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || selectedTransform == null || pointerArmed) return;
        if (keyboard.rKey.wasPressedThisFrame) RotateSelectionClockwise();
        else if (keyboard.leftArrowKey.wasPressedThisFrame) TryNudgeSelection(Vector3Int.left);
        else if (keyboard.rightArrowKey.wasPressedThisFrame) TryNudgeSelection(Vector3Int.right);
        else if (keyboard.upArrowKey.wasPressedThisFrame) TryNudgeSelection(new Vector3Int(0, 0, 1));
        else if (keyboard.downArrowKey.wasPressedThisFrame) TryNudgeSelection(new Vector3Int(0, 0, -1));
        else if (keyboard.eKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame) TryNudgeSelection(Vector3Int.up);
        else if (keyboard.qKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame) TryNudgeSelection(Vector3Int.down);
    }

    private bool TryGetClosestDraggable(Ray ray, out Block blockHit, out KingBuildPlacement kingHit)
    {
        blockHit = null;
        kingHit = null;
        float closest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, raycastDistance, draggableLayers, QueryTriggerInteraction.Ignore))
        {
            KingBuildPlacement king = hit.collider.GetComponentInParent<KingBuildPlacement>();
            Block block = hit.collider.GetComponentInParent<Block>();
            bool isBlock = block != null && block.CompareTag("Block");
            if ((king == null && !isBlock) || hit.distance >= closest) continue;
            closest = hit.distance;
            kingHit = king;
            blockHit = king == null ? block : null;
        }
        return kingHit != null || blockHit != null;
    }

    private void BeginPlacement()
    {
        if (selectedTransform == null || placementActive) return;
        snapshot = new PlacementSnapshot
        {
            Position = selectedTransform.position,
            Rotation = selectedTransform.rotation,
            Parent = selectedTransform.parent,
            Ship = selectedBlock == null ? null : selectedBlock.GetComponentInParent<Ship>(),
            KingSupport = selectedKing == null ? null : selectedKing.SupportBlock
        };
        targetShip = snapshot.Ship != null ? snapshot.Ship : FindClosestShip(selectedTransform.position);
        if (selectedBlock != null && snapshot.Ship != null) snapshot.Ship.DetachBlock(selectedBlock);
        else if (selectedKing != null) selectedKing.ClearSupport();

        movingBody = selectedTransform.GetComponent<Rigidbody>();
        if (movingBody != null)
        {
            previousKinematic = movingBody.isKinematic;
            previousUseGravity = movingBody.useGravity;
            if (!movingBody.isKinematic)
            {
                movingBody.linearVelocity = Vector3.zero;
                movingBody.angularVelocity = Vector3.zero;
            }
            movingBody.useGravity = false;
            movingBody.isKinematic = true;
        }

        placementActive = true;
        placementValid = EvaluatePlacement();
        RefreshSelectionOutline(placementValid ? validColor : invalidColor);
        SetGizmoVisible(false);
    }

    private void UpdatePointerPlacement(Vector2 pointerPosition)
    {
        Ray ray = buildCamera.ScreenPointToRay(pointerPosition);
        if (selectedKing != null && TryGetChairPlacement(ray, out Vector3 kingPosition))
        {
            ApplyPreview(kingPosition, selectedTransform.rotation);
            return;
        }

        Plane plane = new(Vector3.up, new Vector3(0f, activeLayerHeight, 0f));
        if (!plane.Raycast(ray, out float distance)) return;
        Vector3 target = ray.GetPoint(distance) + pointerOffset;
        target.y = activeLayerHeight;
        ApplyPreview(SnapHorizontal(target), selectedTransform.rotation);
    }

    private Vector3 SnapHorizontal(Vector3 worldPosition)
    {
        float step = GetGridSize();
        Quaternion frameRotation = targetShip == null ? Quaternion.identity : targetShip.transform.rotation;
        Vector3 origin = GetGridOrigin();
        Vector3 local = Quaternion.Inverse(frameRotation) * (worldPosition - origin);
        local.x = Mathf.Round(local.x / step) * step;
        local.z = Mathf.Round(local.z / step) * step;
        Vector3 snapped = origin + frameRotation * local;
        snapped.y = activeLayerHeight;
        return snapped;
    }

    private Vector3 GetGridOrigin()
    {
        if (targetShip != null && targetShip.AnchorBlock != null) return targetShip.AnchorBlock.transform.position;
        Vector3 center = placementCenter == null ? Vector3.zero : placementCenter.position;
        center.y = activeLayerHeight;
        return center;
    }

    private float GetGridSize() => targetShip == null ? gridSize : targetShip.AttachmentGridSize;

    private void ApplyPreview(Vector3 position, Quaternion rotation)
    {
        if (selectedTransform == null) return;
        selectedTransform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        bool valid = EvaluatePlacement();
        if (valid == placementValid) return;
        placementValid = valid;
        RefreshSelectionOutline(valid ? validColor : invalidColor);
    }

    private bool EvaluatePlacement() => selectedTransform != null
        && IsWithinPlacementBounds(selectedTransform)
        && IsWithinHeightLimit(selectedTransform.position.y)
        && IsPlacementClear(selectedTransform);

    private bool IsWithinHeightLimit(float height)
    {
        float floor = GetBuildFloorHeight();
        float allowance = GetGridSize() * maximumBuildLayers + GetSelectionHalfHeight();
        return height >= floor - 0.05f && height <= floor + allowance;
    }

    private float GetBuildFloorHeight()
    {
        Collider platformCollider = placementCenter == null ? null : placementCenter.GetComponent<Collider>();
        return platformCollider == null
            ? (placementCenter == null ? 0f : placementCenter.position.y)
            : platformCollider.bounds.max.y;
    }

    private float GetSelectionHalfHeight() => TryGetCombinedBounds(selectedTransform, out Bounds bounds)
        ? bounds.extents.y : GetGridSize() * 0.5f;

    private void FinishPlacement()
    {
        if (!placementActive) return;
        if (!placementValid) RestoreSnapshot(); else CommitCurrentPosition();
        RestoreMovingBody();
        placementActive = false;
        snapshot = null;
        targetShip = null;
        movingBody = null;
        RefreshSelectionOutline(selectedColor);
        SetGizmoVisible(true);
        UpdateMoveGizmo();
    }

    private void CommitCurrentPosition()
    {
        if (selectedKing != null)
        {
            Block support = FindKingSupportAtCurrentPosition();
            if (support != null) selectedKing.SetSupport(support); else selectedKing.ClearSupport();
            return;
        }
        if (selectedBlock == null) return;
        Ship ship = targetShip != null ? targetShip : FindClosestShip(selectedTransform.position);
        if (ship != null && ship.TryAttachBlock(selectedBlock))
        {
            ship.AttachTouchingBlocks();
            return;
        }
        GameObject looseRoot = GameObject.Find("Build Pieces");
        selectedTransform.SetParent(looseRoot == null ? null : looseRoot.transform, true);
    }

    private void RestoreSnapshot()
    {
        if (snapshot == null || selectedTransform == null) return;
        selectedTransform.SetParent(snapshot.Parent, true);
        selectedTransform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
        Physics.SyncTransforms();
        if (selectedBlock != null && snapshot.Ship != null) snapshot.Ship.AttachBlock(selectedBlock);
        else if (selectedKing != null && snapshot.KingSupport != null) selectedKing.SetSupport(snapshot.KingSupport);
    }

    private void RestoreMovingBody()
    {
        if (movingBody == null) return;
        movingBody.isKinematic = previousKinematic;
        movingBody.useGravity = previousUseGravity;
        if (!previousKinematic)
        {
            movingBody.linearVelocity = Vector3.zero;
            movingBody.angularVelocity = Vector3.zero;
        }
    }

    public void CancelActivePlacement()
    {
        if (placementActive)
        {
            RestoreSnapshot();
            RestoreMovingBody();
        }
        placementActive = false;
        pointerArmed = false;
        snapshot = null;
        targetShip = null;
        movingBody = null;
        RefreshSelectionOutline(selectedColor);
        SetGizmoVisible(true);
    }

    public bool TryNudgeSelection(Vector3Int gridDirection)
    {
        if (selectedTransform == null || placementActive || gridDirection == Vector3Int.zero) return false;
        BeginPlacement();
        if (!placementActive) return false;
        Quaternion frameRotation = targetShip == null ? Quaternion.identity : targetShip.transform.rotation;
        Vector3 direction = frameRotation * new Vector3(gridDirection.x, gridDirection.y, gridDirection.z);
        Vector3 target = selectedTransform.position + direction * GetGridSize();
        activeLayerHeight = target.y;
        ApplyPreview(target, selectedTransform.rotation);
        bool result = placementValid;
        FinishPlacement();
        return result;
    }

    public bool RotateSelectionClockwise()
    {
        if (selectedTransform == null || placementActive) return false;
        BeginPlacement();
        if (!placementActive) return false;
        Quaternion rotated = Quaternion.AngleAxis(90f, Vector3.up) * selectedTransform.rotation;
        ApplyPreview(selectedTransform.position, rotated);
        bool result = placementValid;
        FinishPlacement();
        return result;
    }

    public void SelectBlock(Block block)
    {
        if (block == null) { ClearSelection(); return; }
        SetSelection(block, null, block.transform);
    }

    public void SelectKing(KingBuildPlacement king)
    {
        if (king == null) { ClearSelection(); return; }
        SetSelection(null, king, king.transform);
    }

    private void SetSelection(Block block, KingBuildPlacement king, Transform target)
    {
        if (selectedTransform == target) return;
        CancelActivePlacement();
        RemoveSelectionOutline();
        selectedBlock = block;
        selectedKing = king;
        selectedTransform = target;
        RefreshSelectionOutline(selectedColor);
        CreateMoveGizmo();
        UpdateMoveGizmo();
    }

    private void ClearSelection()
    {
        CancelActivePlacement();
        RemoveSelectionOutline();
        selectedBlock = null;
        selectedKing = null;
        selectedTransform = null;
        DestroyMoveGizmo();
    }

    public bool IsPlacementClear(Transform movingRoot)
    {
        if (movingRoot == null) return false;
        foreach (Collider movingCollider in movingRoot.GetComponentsInChildren<Collider>(true))
        {
            if (movingCollider == null || !movingCollider.enabled || movingCollider.isTrigger) continue;
            Collider[] nearby = Physics.OverlapBox(movingCollider.bounds.center,
                movingCollider.bounds.extents + Vector3.one * 0.015f,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider other in nearby)
            {
                if (other == null || other == movingCollider || !other.enabled || other.isTrigger
                    || other.transform.IsChildOf(movingRoot)
                    || (other.GetComponentInParent<Block>() == null
                        && other.GetComponentInParent<KingBuildPlacement>() == null)) continue;
                if (Physics.ComputePenetration(movingCollider, movingCollider.transform.position,
                        movingCollider.transform.rotation, other, other.transform.position,
                        other.transform.rotation, out _, out float depth) && depth > 0.015f) return false;
            }
        }
        return true;
    }

    private bool IsWithinPlacementBounds(Transform movingRoot)
    {
        if (placementCenter == null) return true;
        if (TryGetCombinedBounds(movingRoot, out Bounds movingBounds))
        {
            Collider platformCollider = placementCenter.GetComponent<Collider>();
            if (platformCollider != null)
            {
                Bounds platformBounds = platformCollider.bounds;
                const float tolerance = 0.04f;
                return movingBounds.min.x >= platformBounds.min.x - tolerance
                    && movingBounds.max.x <= platformBounds.max.x + tolerance
                    && movingBounds.min.z >= platformBounds.min.z - tolerance
                    && movingBounds.max.z <= platformBounds.max.z + tolerance;
            }
        }
        if (maximumPlacementDistance <= 0f) return true;
        Vector2 offset = new(movingRoot.position.x - placementCenter.position.x,
            movingRoot.position.z - placementCenter.position.z);
        return offset.sqrMagnitude <= maximumPlacementDistance * maximumPlacementDistance;
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null) return false;
        bool hasBounds = false;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger) continue;
            if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        if (hasBounds) return true;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return hasBounds;
    }

    private bool TryGetChairPlacement(Ray ray, out Vector3 position)
    {
        position = default;
        float closest = float.PositiveInfinity;
        Block bestBlock = null;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, raycastDistance, draggableLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(selectedTransform)) continue;
            Block block = hit.collider.GetComponentInParent<Block>();
            if (block == null || block.GetComponent<ChairSeat>() == null || hit.distance >= closest) continue;
            closest = hit.distance;
            bestBlock = block;
        }
        if (bestBlock == null) return false;
        targetShip = bestBlock.GetComponentInParent<Ship>();
        position = selectedKing.GetPositionOn(bestBlock);
        return true;
    }

    private Block FindKingSupportAtCurrentPosition()
    {
        if (selectedKing == null) return null;
        Block closest = null;
        float closestDistanceSquared = 0.04f;
        foreach (Block block in Object.FindObjectsByType<Block>(FindObjectsInactive.Exclude))
        {
            if (block == null || !block.IsAlive || block.GetComponent<ChairSeat>() == null) continue;
            float distanceSquared = (selectedKing.GetPositionOn(block) - selectedKing.transform.position).sqrMagnitude;
            if (distanceSquared <= closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closest = block;
            }
        }
        return closest;
    }

    private Ship FindClosestShip(Vector3 position)
    {
        Ship closest = null;
        float closestDistanceSquared = float.PositiveInfinity;
        foreach (Ship ship in Object.FindObjectsByType<Ship>(FindObjectsInactive.Exclude))
        {
            float distanceSquared = (ship.transform.position - position).sqrMagnitude;
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closest = ship;
            }
        }
        return closest;
    }

    private void RefreshSelectionOutline(Color color)
    {
        if (selectedTransform == null) return;
        if (selectionOutline == null)
            selectionOutline = selectedTransform.GetComponent<BlockDragOutline>()
                ?? selectedTransform.gameObject.AddComponent<BlockDragOutline>();
        if (selectionOutline != null) selectionOutline.Configure(color, outlineWidth);
    }

    private void RemoveSelectionOutline()
    {
        if (selectionOutline == null) return;
        DestroyGenerated(selectionOutline);
        selectionOutline = null;
    }

    private bool TryGetGizmoDirection(Ray ray, out Vector3Int direction)
    {
        direction = Vector3Int.zero;
        float closest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, raycastDistance, ~0, QueryTriggerInteraction.Collide))
        {
            if (!gizmoDirections.TryGetValue(hit.collider, out Vector3Int hitDirection) || hit.distance >= closest) continue;
            closest = hit.distance;
            direction = hitDirection;
        }
        return direction != Vector3Int.zero;
    }

    private void CreateMoveGizmo()
    {
        DestroyMoveGizmo();
        if (selectedTransform == null) return;
        moveGizmo = new GameObject("Build Move Arrows") { hideFlags = HideFlags.DontSave };
        CreateAxisPair(Vector3Int.right, new Color(0.95f, 0.28f, 0.2f));
        CreateAxisPair(new Vector3Int(0, 0, 1), new Color(0.24f, 0.52f, 1f));
        // Put vertical controls beside the part. A conventional negative-Y arrow
        // centered on a floor-level block is buried below the build platform.
        CreateNudgeArrow(Vector3Int.up, new Color(0.3f, 0.9f, 0.38f), new Vector3(1.45f, 0.2f, 0f));
        CreateNudgeArrow(Vector3Int.down, new Color(0.2f, 0.68f, 0.28f), new Vector3(2.05f, 1.7f, 0f));
        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hub.name = "Move Gizmo Hub";
        hub.transform.SetParent(moveGizmo.transform, false);
        hub.transform.localScale = Vector3.one * 0.22f;
        DestroyGenerated(hub.GetComponent<Collider>());
        Material material = CreateGizmoMaterial(new Color(1f, 0.82f, 0.42f));
        gizmoMaterials.Add(material);
        hub.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void CreateAxisPair(Vector3Int direction, Color color)
    {
        CreateNudgeArrow(direction, color, Vector3.zero);
        CreateNudgeArrow(-direction, Color.Lerp(color, Color.black, 0.2f), Vector3.zero);
    }

    private void CreateNudgeArrow(Vector3Int gridDirection, Color color, Vector3 origin)
    {
        Vector3 direction = new(gridDirection.x, gridDirection.y, gridDirection.z);
        Material material = CreateGizmoMaterial(color);
        gizmoMaterials.Add(material);
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = $"Nudge {gridDirection}";
        shaft.transform.SetParent(moveGizmo.transform, false);
        shaft.transform.localPosition = origin + direction * 0.72f;
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        shaft.transform.localScale = new Vector3(0.1f, 0.48f, 0.1f);
        shaft.GetComponent<Renderer>().sharedMaterial = material;
        gizmoDirections[shaft.GetComponent<Collider>()] = gridDirection;
        GameObject head = new($"Nudge {gridDirection} Head");
        head.transform.SetParent(moveGizmo.transform, false);
        head.transform.localPosition = origin + direction * 1.34f;
        head.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        Mesh cone = CreateConeMesh();
        gizmoMeshes.Add(cone);
        head.AddComponent<MeshFilter>().sharedMesh = cone;
        head.AddComponent<MeshRenderer>().sharedMaterial = material;
        MeshCollider collider = head.AddComponent<MeshCollider>();
        collider.sharedMesh = cone;
        collider.convex = true;
        gizmoDirections[collider] = gridDirection;
    }

    private void UpdateMoveGizmo()
    {
        if (moveGizmo == null || selectedTransform == null || placementActive) return;
        if (buildCamera == null) buildCamera = GetComponent<Camera>();
        if (buildCamera == null) return;
        Vector3 center = TryGetCombinedBounds(selectedTransform, out Bounds bounds) ? bounds.center : selectedTransform.position;
        moveGizmo.transform.position = center;
        moveGizmo.transform.rotation = Quaternion.identity;
        float distance = Vector3.Distance(buildCamera.transform.position, center);
        moveGizmo.transform.localScale = Vector3.one * Mathf.Clamp(distance * 0.052f, 0.68f, 1.35f);
    }

    private void SetGizmoVisible(bool visible)
    {
        if (moveGizmo != null) moveGizmo.SetActive(visible);
    }

    private void DestroyMoveGizmo()
    {
        gizmoDirections.Clear();
        if (moveGizmo != null) { DestroyGenerated(moveGizmo); moveGizmo = null; }
        foreach (Material material in gizmoMaterials) DestroyGenerated(material);
        foreach (Mesh mesh in gizmoMeshes) DestroyGenerated(mesh);
        gizmoMaterials.Clear();
        gizmoMeshes.Clear();
    }

    private static Mesh CreateConeMesh()
    {
        const int sides = 12;
        const float radius = 0.25f;
        const float baseY = -0.24f;
        const float tipY = 0.34f;
        var vertices = new Vector3[sides + 2];
        var triangles = new int[sides * 6];
        vertices[0] = new Vector3(0f, tipY, 0f);
        vertices[1] = new Vector3(0f, baseY, 0f);
        for (int side = 0; side < sides; side++)
        {
            float angle = side * Mathf.PI * 2f / sides;
            vertices[side + 2] = new Vector3(Mathf.Cos(angle) * radius, baseY, Mathf.Sin(angle) * radius);
            int next = (side + 1) % sides;
            int triangle = side * 6;
            triangles[triangle] = 0;
            triangles[triangle + 1] = side + 2;
            triangles[triangle + 2] = next + 2;
            triangles[triangle + 3] = 1;
            triangles[triangle + 4] = next + 2;
            triangles[triangle + 5] = side + 2;
        }
        Mesh mesh = new() { name = "Build Nudge Arrow", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateGizmoMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material material = new(shader) { name = "Build Move Arrow Material", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        return material;
    }

    private static void DestroyGenerated(Object generated)
    {
        if (generated == null) return;
        if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
    }
}
