using RoyaltyBoat.King;
using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public sealed class GhostShipCannonball : MonoBehaviour
    {
        [SerializeField, Min(0)] private int damage = 25;
        [SerializeField, Min(0.1f)] private float lifetime = 8f;

        private Transform ownerRoot;
        private Rigidbody body;
        private bool spent;

        public int Damage => damage;
        public float Lifetime => lifetime;

        public void Configure(int damageAmount, float secondsToLive)
        {
            damage = Mathf.Max(0, damageAmount);
            lifetime = Mathf.Max(0.1f, secondsToLive);
        }

        public void Launch(Vector3 velocity, Transform owner)
        {
            ownerRoot = owner == null ? null : owner.root;
            Body.linearVelocity = velocity;
            IgnoreOwnerColliders();
            Destroy(gameObject, lifetime);
        }

        private Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

        private void OnCollisionEnter(Collision collision)
        {
            if (spent || collision.transform.root == ownerRoot)
            {
                return;
            }

            spent = true;

            Block block = collision.collider.GetComponentInParent<Block>();
            if (block != null)
            {
                block.TakeDamage(damage);
            }

            KingHealth kingHealth = collision.collider.GetComponentInParent<KingHealth>();
            if (kingHealth != null)
            {
                kingHealth.ApplyDamage(damage, KingDeathCause.Projectile);
            }

            Destroy(gameObject);
        }

        private void IgnoreOwnerColliders()
        {
            if (ownerRoot == null)
            {
                return;
            }

            Collider projectileCollider = GetComponent<Collider>();
            foreach (Collider ownerCollider in ownerRoot.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0, damage);
            lifetime = Mathf.Max(0.1f, lifetime);
        }
    }
}
