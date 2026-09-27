using UnityEngine;

namespace RoyaltyBoat.King
{
    /// <summary>
    /// Builder-only adapter that keeps the King separate from structural blocks.
    /// The builder positions him on a block and this component records which ship
    /// supports him so launch code can validate and transfer him into the voyage.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KingController))]
    public sealed class KingBuildPlacement : MonoBehaviour
    {
        private KingController controller;

        public Block SupportBlock { get; private set; }
        public Ship SupportingShip { get; private set; }
        public bool IsPlaced => SupportBlock != null
            && SupportingShip != null
            && controller != null
            && controller.CanLaunch;

        private void Awake()
        {
            CacheController();
        }

        public void EnterBuildMode(Vector3 position, Quaternion rotation)
        {
            CacheController();
            controller.PrepareForBuild(position, rotation);
            controller.Body.useGravity = false;
            controller.Body.isKinematic = true;
            ClearSupport();
        }

        public Vector3 GetPositionOn(Block block)
        {
            if (block == null)
            {
                return transform.position;
            }

            Collider supportCollider = block.GetComponent<Collider>();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (supportCollider == null || renderers.Length == 0)
            {
                return block.transform.position + Vector3.up;
            }

            Bounds visualBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                visualBounds.Encapsulate(renderers[index].bounds);
            }

            float rootAboveVisualBottom =
                transform.position.y - visualBounds.min.y;
            return new Vector3(
                block.transform.position.x,
                supportCollider.bounds.max.y + rootAboveVisualBottom,
                block.transform.position.z);
        }

        public Vector3 GetPositionInGridCell(
            Vector3 cellCenter,
            Vector3 cellUp,
            float cellSize)
        {
            Vector3 up = cellUp.sqrMagnitude > 0.001f
                ? cellUp.normalized
                : Vector3.up;
            float rootAboveVisualBottom = GetRootAboveVisualBottom(up);
            return cellCenter
                + up * (rootAboveVisualBottom - Mathf.Max(0.01f, cellSize) * 0.5f);
        }

        public Vector3 GetGridCellCenter(
            Vector3 rootPosition,
            Vector3 cellUp,
            float cellSize)
        {
            Vector3 up = cellUp.sqrMagnitude > 0.001f
                ? cellUp.normalized
                : Vector3.up;
            float rootAboveVisualBottom = GetRootAboveVisualBottom(up);
            return rootPosition
                + up * (Mathf.Max(0.01f, cellSize) * 0.5f - rootAboveVisualBottom);
        }

        public bool SetSupport(Block block)
        {
            Ship ship = block == null ? null : block.GetComponentInParent<Ship>();
            Rigidbody body = ship == null ? null : ship.GetComponent<Rigidbody>();
            ChairSeat chair = block == null ? null : block.GetComponent<ChairSeat>();

            if (block == null
                || chair == null
                || !block.IsAlive
                || ship == null
                || body == null)
            {
                ClearSupport();
                return false;
            }

            CacheController();
            UnsubscribeFromSupportBlock();
            SupportBlock = block;
            SupportingShip = ship;
            SupportBlock.Destroyed += HandleSupportDestroyed;
            controller.BoatLink.Connect(body, true);
            return true;
        }

        public void ClearSupport()
        {
            CacheController();
            UnsubscribeFromSupportBlock();
            SupportBlock = null;
            SupportingShip = null;
            controller.BoatLink.Disconnect();
        }

        public void EnterVoyage(Ship ship)
        {
            CacheController();
            SupportingShip = ship;

            Rigidbody shipBody = ship == null ? null : ship.GetComponent<Rigidbody>();
            controller.Body.isKinematic = false;
            controller.Body.useGravity = true;

            if (shipBody != null)
            {
                controller.BoatLink.Connect(shipBody, true);
            }

            controller.Body.linearVelocity = shipBody == null
                ? Vector3.zero
                : shipBody.linearVelocity;
            controller.Body.angularVelocity = Vector3.zero;
        }

        private void HandleSupportDestroyed(Block destroyedBlock)
        {
            if (destroyedBlock == SupportBlock)
            {
                ClearSupport();
            }
        }

        private void UnsubscribeFromSupportBlock()
        {
            if (SupportBlock != null)
            {
                SupportBlock.Destroyed -= HandleSupportDestroyed;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromSupportBlock();
        }

        private void CacheController()
        {
            controller ??= GetComponent<KingController>();
        }

        private float GetRootAboveVisualBottom(Vector3 up)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0f;
            }

            float lowestProjection = float.PositiveInfinity;
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                float projectedExtent =
                    Mathf.Abs(up.x) * bounds.extents.x
                    + Mathf.Abs(up.y) * bounds.extents.y
                    + Mathf.Abs(up.z) * bounds.extents.z;
                float rendererBottom = Vector3.Dot(bounds.center, up)
                    - projectedExtent;
                lowestProjection = Mathf.Min(lowestProjection, rendererBottom);
            }

            return Vector3.Dot(transform.position, up) - lowestProjection;
        }
    }
}
