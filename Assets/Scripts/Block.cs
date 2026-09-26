using System;
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

    private void Awake()
    {
        Health = maxHealth;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle"))
        {
            return;
        }

        ObstacleDamage obstacle = collision.gameObject.GetComponent<ObstacleDamage>();

        if (obstacle == null)
        {
            obstacle = collision.gameObject.GetComponentInParent<ObstacleDamage>();
        }

        if (obstacle != null)
        {
            TakeDamage(obstacle.Damage);
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

        if (!IsAlive)
        {
            Destroyed?.Invoke(this);
        }
    }

    public void Repair(int amount)
    {
        if (amount <= 0 || !IsAlive)
        {
            return;
        }

        Health = Mathf.Min(maxHealth, Health + amount);
    }
}
