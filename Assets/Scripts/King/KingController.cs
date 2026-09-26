using UnityEngine;

namespace RoyaltyBoat.King
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(KingHealth))]
    [RequireComponent(typeof(KingBoatLink))]
    public sealed class KingController : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public KingHealth Health { get; private set; }
        public KingBoatLink BoatLink { get; private set; }

        public bool CanLaunch => Health.IsAlive && BoatLink.IsSupported;

        private void Awake()
        {
            CacheComponents();
        }

        public void PrepareForBuild(Vector3 worldPosition, Quaternion worldRotation)
        {
            CacheComponents();
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Health.ResetHealth();
            BoatLink.Disconnect();
        }

        public void NotifyFellIntoWater()
        {
            Health.Kill(KingDeathCause.Water);
        }

        private void CacheComponents()
        {
            Body ??= GetComponent<Rigidbody>();
            Health ??= GetComponent<KingHealth>();
            BoatLink ??= GetComponent<KingBoatLink>();
        }
    }
}
