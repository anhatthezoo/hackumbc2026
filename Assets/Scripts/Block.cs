using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Block : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;

    [Header("Material")]
    [Tooltip("Flat amount subtracted from each incoming damage hit.")]
    [SerializeField, Min(0)] private int damageReduction;

    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public int DamageReduction => damageReduction;
    public bool IsAlive => Health > 0;

    public event Action<Block> Destroyed;

    private BlockDamageVisual damageVisual;
    private bool isBreaking;

    private void Awake()
    {
        Health = maxHealth;
        damageVisual = GetComponent<BlockDamageVisual>();
        if (damageVisual == null)
        {
            damageVisual = gameObject.AddComponent<BlockDamageVisual>();
        }

        damageVisual.SetHealth(Health, maxHealth);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryTakeObstacleDamage(
            collision.collider,
            ObstacleDamage.GetImpactSpeed(collision));
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTakeObstacleDamage(other, null);
    }

    private void TryTakeObstacleDamage(Collider obstacleCollider, float? impactSpeed)
    {
        ObstacleDamage obstacle = obstacleCollider.GetComponentInParent<ObstacleDamage>();
        if (obstacle == null)
        {
            return;
        }

        if (impactSpeed.HasValue)
        {
            obstacle.TryDamage(this, impactSpeed.Value);
        }
        else
        {
            obstacle.TryDamage(this);
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || !IsAlive)
        {
            return;
        }

        int reducedDamage = Mathf.Max(0, amount - damageReduction);

        if (reducedDamage == 0)
        {
            return;
        }

        Health = Mathf.Max(0, Health - reducedDamage);
        damageVisual?.SetHealth(Health, maxHealth);

        if (!IsAlive && !isBreaking)
        {
            isBreaking = true;
            DisableColliders();
            Destroyed?.Invoke(this);
            StartCoroutine(BreakAndDisappear());
        }
    }

    public void Repair(int amount)
    {
        if (amount <= 0 || !IsAlive)
        {
            return;
        }

        Health = Mathf.Min(maxHealth, Health + amount);
        damageVisual?.SetHealth(Health, maxHealth);
    }

    private void DisableColliders()
    {
        foreach (Collider blockCollider in GetComponentsInChildren<Collider>(true))
        {
            blockCollider.enabled = false;
        }
    }

    private IEnumerator BreakAndDisappear()
    {
        const float duration = 0.4f;
        Vector3 initialScale = transform.localScale;
        Quaternion initialRotation = transform.localRotation;
        Vector3 breakRotation = new Vector3(12f, -18f, 15f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress;
            transform.localScale = Vector3.LerpUnclamped(
                initialScale,
                Vector3.zero,
                eased);
            transform.localRotation = initialRotation * Quaternion.Euler(
                breakRotation * eased);
            yield return null;
        }

        Destroy(gameObject);
    }
}
