using System.Collections.Generic;
using RoyaltyBoat.King;
using UnityEngine;
using UnityEngine.Serialization;

namespace RoyaltyBoat.Obstacles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AcidWaterVolume : MonoBehaviour
    {
        [FormerlySerializedAs("damagePerSecond")]
        [SerializeField, Min(0f)] private float damagePerHit = 10f;
        [SerializeField, Min(0.05f)] private float damageInterval = 1.3f;

        private readonly Dictionary<Transform, float> nextDamageTimes = new Dictionary<Transform, float>();

        public float DamagePerHit => damagePerHit;
        public float DamagePerSecond => damagePerHit / damageInterval;
        public float DamageInterval => damageInterval;

        private void Reset()
        {
            Collider volume = GetComponent<Collider>();
            volume.isTrigger = true;
        }

        private void OnValidate()
        {
            damagePerHit = Mathf.Max(0f, damagePerHit);
            damageInterval = Mathf.Max(0.05f, damageInterval);

            Collider volume = GetComponent<Collider>();
            if (volume != null)
            {
                volume.isTrigger = true;
            }
        }

        private void OnDisable()
        {
            nextDamageTimes.Clear();
        }

        private void OnTriggerStay(Collider other)
        {
            if (damagePerHit <= 0f)
            {
                return;
            }

            Block block = other.GetComponentInParent<Block>();
            if (block != null)
            {
                ApplyPeriodicBlockDamage(block);
                return;
            }

            Transform damageRoot = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;
            if (nextDamageTimes.TryGetValue(damageRoot, out float nextTime) && Time.time < nextTime)
            {
                return;
            }

            nextDamageTimes[damageRoot] = Time.time + damageInterval;
            float damage = damagePerHit;

            if (TryApplyGenericDamage(damageRoot, damage))
            {
                return;
            }

            KingHealth kingHealth = damageRoot.GetComponentInChildren<KingHealth>();
            if (kingHealth == null)
            {
                kingHealth = other.GetComponentInParent<KingHealth>();
            }

            if (kingHealth != null)
            {
                kingHealth.ApplyDamage(damage, KingDeathCause.EnvironmentalHazard);
            }
        }

        private void ApplyPeriodicBlockDamage(Block block)
        {
            Transform damageTarget = block.transform;
            if (nextDamageTimes.TryGetValue(damageTarget, out float nextTime)
                && Time.time < nextTime)
            {
                return;
            }

            nextDamageTimes[damageTarget] = Time.time + damageInterval;
            int damage = Mathf.Max(1, Mathf.RoundToInt(damagePerHit));
            block.TakeDamage(damage);
        }

        private bool TryApplyGenericDamage(Transform targetRoot, float damage)
        {
            if (TryApplyFromBehaviours(targetRoot.GetComponentsInParent<MonoBehaviour>(), damage))
            {
                return true;
            }

            return TryApplyFromBehaviours(targetRoot.GetComponentsInChildren<MonoBehaviour>(), damage);
        }

        private bool TryApplyFromBehaviours(MonoBehaviour[] behaviours, float damage)
        {
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is not IHazardDamageReceiver receiver)
                {
                    continue;
                }

                receiver.ApplyHazardDamage(damage, HazardType.Acid, gameObject);
                return true;
            }

            return false;
        }
    }
}
