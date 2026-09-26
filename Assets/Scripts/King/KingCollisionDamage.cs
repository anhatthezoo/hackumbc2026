using UnityEngine;

namespace RoyaltyBoat.King
{
    [RequireComponent(typeof(KingHealth))]
    public sealed class KingCollisionDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float safeImpactSpeed = 5f;
        [SerializeField, Min(0f)] private float damagePerExcessSpeed = 8f;
        [SerializeField, Min(0f)] private float damageCooldown = 0.15f;

        private KingHealth kingHealth;
        private float nextDamageTime;

        private void Awake()
        {
            kingHealth = GetComponent<KingHealth>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextDamageTime || !kingHealth.IsAlive)
            {
                return;
            }

            float excessSpeed = collision.relativeVelocity.magnitude - safeImpactSpeed;
            if (excessSpeed <= 0f)
            {
                return;
            }

            kingHealth.ApplyDamage(excessSpeed * damagePerExcessSpeed, KingDeathCause.Collision);
            nextDamageTime = Time.time + damageCooldown;
        }
    }
}
