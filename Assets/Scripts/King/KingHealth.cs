using System;
using UnityEngine;

namespace RoyaltyBoat.King
{
    public sealed class KingHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public bool IsAlive => currentHealth > 0f;

        public event Action<float, float> HealthChanged;
        public event Action<KingDeathCause> Died;

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public bool ApplyDamage(float amount, KingDeathCause cause = KingDeathCause.Unknown)
        {
            if (amount <= 0f || !IsAlive)
            {
                return false;
            }

            SetHealth(currentHealth - amount);

            if (!IsAlive)
            {
                Died?.Invoke(cause);
            }

            return true;
        }

        public void Kill(KingDeathCause cause = KingDeathCause.Unknown)
        {
            if (!IsAlive)
            {
                return;
            }

            SetHealth(0f);
            Died?.Invoke(cause);
        }

        public void Restore(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            SetHealth(currentHealth + amount);
        }

        public void ResetHealth()
        {
            SetHealth(maxHealth);
        }

        private void SetHealth(float value)
        {
            float previous = currentHealth;
            currentHealth = Mathf.Clamp(value, 0f, maxHealth);

            if (!Mathf.Approximately(previous, currentHealth))
            {
                HealthChanged?.Invoke(previous, currentHealth);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
    }
}
