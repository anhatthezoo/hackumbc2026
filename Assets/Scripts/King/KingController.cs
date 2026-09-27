using RoyaltyBoat.Water;
using UnityEngine;

namespace RoyaltyBoat.King
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(KingHealth))]
    [RequireComponent(typeof(KingBoatLink))]
    public sealed class KingController : MonoBehaviour
    {
        [Header("Water Failure")]
        [Tooltip("The King's root must remain this far below the local water surface before drowning can begin.")]
        [SerializeField, Min(0.1f)] private float requiredSubmersionDepth = 0.9f;
        [Tooltip("Continuous time deeply underwater before the King drowns. Brief splashes reset this timer.")]
        [SerializeField, Min(0.1f)] private float drowningGracePeriod = 1.5f;

        [Header("Capsize Failure")]
        [Tooltip("The boat is capsized when its upward alignment falls below this value. 0.3 is roughly 73 degrees of tilt.")]
        [SerializeField, Range(-1f, 1f)] private float minimumUprightDot = 0.3f;
        [Tooltip("Continuous time beyond the capsize angle before the King dies.")]
        [SerializeField, Min(0.1f)] private float capsizeGracePeriod = 1.25f;

        private OceanWaveGenerator ocean;
        private float submergedTime;
        private float capsizedTime;

        public Rigidbody Body { get; private set; }
        public KingHealth Health { get; private set; }
        public KingBoatLink BoatLink { get; private set; }

        public bool CanLaunch => Health.IsAlive && BoatLink.IsSupported;

        private void Awake()
        {
            CacheComponents();
        }

        private void FixedUpdate()
        {
            CacheComponents();
            if (Health == null || !Health.IsAlive || Body == null || Body.isKinematic)
            {
                ResetFailureTimers();
                return;
            }

            UpdateCapsizeTimer();
            if (capsizedTime >= capsizeGracePeriod)
            {
                Health.Kill(KingDeathCause.Capsized);
                return;
            }

            float waterSurfaceHeight = ResolveWaterSurfaceHeight();
            bool deeplySubmerged = transform.position.y
                <= waterSurfaceHeight - requiredSubmersionDepth;
            submergedTime = deeplySubmerged
                ? submergedTime + Time.fixedDeltaTime
                : 0f;

            if (submergedTime >= drowningGracePeriod)
            {
                NotifyFellIntoWater();
            }
        }

        public void PrepareForBuild(Vector3 worldPosition, Quaternion worldRotation)
        {
            CacheComponents();
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Health.ResetHealth();
            BoatLink.Disconnect();
            ResetFailureTimers();
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

        private void UpdateCapsizeTimer()
        {
            bool capsized = BoatLink != null
                && BoatLink.TryGetBoatBody(out Rigidbody boatBody)
                && Vector3.Dot(boatBody.transform.up, Vector3.up) < minimumUprightDot;
            capsizedTime = capsized
                ? capsizedTime + Time.fixedDeltaTime
                : 0f;
        }

        private float ResolveWaterSurfaceHeight()
        {
            if (BoatLink != null
                && BoatLink.TryGetBoatBody(out Rigidbody boatBody))
            {
                OceanWaveBuoyancy buoyancy = boatBody.GetComponent<OceanWaveBuoyancy>();
                if (buoyancy != null && buoyancy.enabled)
                {
                    return buoyancy.AverageSurfaceHeight;
                }
            }

            ocean ??= FindAnyObjectByType<OceanWaveGenerator>();
            return ocean == null ? 0f : ocean.transform.position.y;
        }

        private void ResetFailureTimers()
        {
            submergedTime = 0f;
            capsizedTime = 0f;
        }

        private void OnValidate()
        {
            requiredSubmersionDepth = Mathf.Max(0.1f, requiredSubmersionDepth);
            drowningGracePeriod = Mathf.Max(0.1f, drowningGracePeriod);
            minimumUprightDot = Mathf.Clamp(minimumUprightDot, -1f, 1f);
            capsizeGracePeriod = Mathf.Max(0.1f, capsizeGracePeriod);
        }
    }
}
