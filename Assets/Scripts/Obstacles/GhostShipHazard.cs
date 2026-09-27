using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GhostShipHazard : MonoBehaviour
    {
        [SerializeField] private GhostShipCannonball cannonballPrefab;
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 1f, 4f);
        [SerializeField] private Vector3 patrolOffset = new Vector3(24f, 0f, 0f);
        [SerializeField, Min(0f)] private float patrolSpeed = 4f;
        [SerializeField, Min(0.1f)] private float fireInterval = 2.5f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 18f;

        private Rigidbody body;
        private Vector3 patrolStart;
        private Transform target;
        private float nextFireTime;

        public void Configure(
            GhostShipCannonball projectilePrefab,
            Vector3 movementOffset,
            float movementSpeed,
            float secondsBetweenShots,
            float shotSpeed)
        {
            cannonballPrefab = projectilePrefab;
            patrolOffset = movementOffset;
            patrolSpeed = Mathf.Max(0f, movementSpeed);
            fireInterval = Mathf.Max(0.1f, secondsBetweenShots);
            projectileSpeed = Mathf.Max(0.1f, shotSpeed);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            patrolStart = body.position;
            nextFireTime = Time.time + fireInterval;
        }

        private void FixedUpdate()
        {
            MoveAlongPatrol();

            if (Time.time >= nextFireTime)
            {
                FireAtPlayer();
                nextFireTime = Time.time + fireInterval;
            }
        }

        private void MoveAlongPatrol()
        {
            float distance = patrolOffset.magnitude;
            if (distance <= Mathf.Epsilon || patrolSpeed <= 0f)
            {
                return;
            }

            float progress = Mathf.PingPong(Time.time * patrolSpeed, distance) / distance;
            body.MovePosition(Vector3.Lerp(patrolStart, patrolStart + patrolOffset, progress));
        }

        private void FireAtPlayer()
        {
            if (cannonballPrefab == null)
            {
                return;
            }

            if (target == null)
            {
                target = FindClosestPlayerShip();
            }

            if (target == null)
            {
                return;
            }

            Vector3 muzzlePosition = transform.TransformPoint(muzzleOffset);
            Vector3 direction = (target.position - muzzlePosition).normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            GhostShipCannonball cannonball = Instantiate(
                cannonballPrefab,
                muzzlePosition,
                Quaternion.LookRotation(direction));
            cannonball.Launch(direction * projectileSpeed, transform);
        }

        private Transform FindClosestPlayerShip()
        {
            Ship closest = null;
            float closestDistanceSquared = float.PositiveInfinity;

            foreach (Ship ship in Object.FindObjectsByType<Ship>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!ship.IsAlive)
                {
                    continue;
                }

                float distanceSquared = (ship.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < closestDistanceSquared)
                {
                    closest = ship;
                    closestDistanceSquared = distanceSquared;
                }
            }

            return closest == null ? null : closest.transform;
        }

        private void OnValidate()
        {
            patrolSpeed = Mathf.Max(0f, patrolSpeed);
            fireInterval = Mathf.Max(0.1f, fireInterval);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        }
    }
}
