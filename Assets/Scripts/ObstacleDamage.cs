using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleDamage : MonoBehaviour
{
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(0f)] private float repeatDamageCooldown = 0.75f;
    [SerializeField, Min(0.01f)] private float fullDamageImpactSpeed = 14f;
    [SerializeField, Min(0f)] private float minimumDamageImpactSpeed = 0.75f;
    [SerializeField, Min(1f)] private float maximumDamageMultiplier = 2f;

    private readonly Dictionary<Block, float> nextDamageTimes = new Dictionary<Block, float>();

    public int Damage => damage;

    public void Configure(int damageAmount, bool makeCollidersNonBlocking)
    {
        damage = Mathf.Max(0, damageAmount);

        foreach (Collider obstacleCollider in GetComponentsInChildren<Collider>(true))
        {
            Collider damageCollider = obstacleCollider;
            if (makeCollidersNonBlocking &&
                obstacleCollider is MeshCollider meshCollider &&
                !meshCollider.convex)
            {
                Mesh sharedMesh = meshCollider.sharedMesh;
                meshCollider.enabled = false;

                BoxCollider triggerVolume = meshCollider.GetComponent<BoxCollider>();
                if (triggerVolume == null)
                {
                    triggerVolume = meshCollider.gameObject.AddComponent<BoxCollider>();
                }

                if (sharedMesh != null)
                {
                    triggerVolume.center = sharedMesh.bounds.center;
                    triggerVolume.size = sharedMesh.bounds.size;
                }

                triggerVolume.isTrigger = true;
                damageCollider = triggerVolume;
            }
            else if (makeCollidersNonBlocking)
            {
                obstacleCollider.isTrigger = true;
            }
            else
            {
                obstacleCollider.isTrigger = false;
            }

            ObstacleDamageRelay relay =
                damageCollider.GetComponent<ObstacleDamageRelay>();
            if (relay == null)
            {
                relay = damageCollider.gameObject.AddComponent<ObstacleDamageRelay>();
            }

            relay.Configure(this);
        }
    }

    public bool TryDamage(Block block)
    {
        return TryApplyDamage(block, damage);
    }

    public bool TryDamage(Block block, float impactSpeed)
    {
        if (impactSpeed < minimumDamageImpactSpeed)
        {
            return false;
        }

        float multiplier = Mathf.Clamp(
            impactSpeed / fullDamageImpactSpeed,
            0f,
            maximumDamageMultiplier);
        int impactDamage = Mathf.CeilToInt(damage * multiplier);
        return TryApplyDamage(block, impactDamage);
    }

    public static float GetImpactSpeed(Collision collision)
    {
        if (collision == null)
        {
            return 0f;
        }

        Vector3 relativeVelocity = collision.relativeVelocity;
        float impactSpeed = 0f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            float closingSpeed = Mathf.Abs(Vector3.Dot(
                relativeVelocity,
                collision.GetContact(i).normal));
            impactSpeed = Mathf.Max(impactSpeed, closingSpeed);
        }

        return collision.contactCount > 0
            ? impactSpeed
            : relativeVelocity.magnitude;
    }

    private bool TryApplyDamage(Block block, int amount)
    {
        if (block == null || amount <= 0)
        {
            return false;
        }

        if (nextDamageTimes.TryGetValue(block, out float nextTime) && Time.time < nextTime)
        {
            return false;
        }

        nextDamageTimes[block] = Time.time + repeatDamageCooldown;
        block.TakeDamage(amount);
        return true;
    }

    private void OnDisable()
    {
        nextDamageTimes.Clear();
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        repeatDamageCooldown = Mathf.Max(0f, repeatDamageCooldown);
        fullDamageImpactSpeed = Mathf.Max(0.01f, fullDamageImpactSpeed);
        minimumDamageImpactSpeed = Mathf.Max(0f, minimumDamageImpactSpeed);
        maximumDamageMultiplier = Mathf.Max(1f, maximumDamageMultiplier);
    }
}
