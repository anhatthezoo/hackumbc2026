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

        public bool SetSupport(Block block)
        {
            Ship ship = block == null ? null : block.GetComponentInParent<Ship>();
            Rigidbody body = ship == null ? null : ship.GetComponent<Rigidbody>();

            if (block == null || !block.IsAlive || ship == null || body == null)
            {
                ClearSupport();
                return false;
            }

            CacheController();
            SupportBlock = block;
            SupportingShip = ship;
            controller.BoatLink.Connect(body, true);
            return true;
        }

        public void ClearSupport()
        {
            CacheController();
            SupportBlock = null;
            SupportingShip = null;
            controller.BoatLink.Disconnect();
        }

        public void EnterVoyage(Ship ship)
        {
            CacheController();
            SupportingShip = ship;
            SupportBlock = null;

            Rigidbody shipBody = ship == null ? null : ship.GetComponent<Rigidbody>();
            if (shipBody != null)
            {
                controller.BoatLink.Connect(shipBody, true);
            }

            controller.Body.isKinematic = false;
            controller.Body.useGravity = true;
            controller.Body.linearVelocity = shipBody == null
                ? Vector3.zero
                : shipBody.linearVelocity;
            controller.Body.angularVelocity = Vector3.zero;
        }

        private void CacheController()
        {
            controller ??= GetComponent<KingController>();
        }
    }
}
