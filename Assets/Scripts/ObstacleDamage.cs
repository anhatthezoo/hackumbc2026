using UnityEngine;
using RoyaltyBoat.Audio;
using RoyaltyBoat.Obstacles;

[DisallowMultipleComponent]
public class ObstacleDamage : MonoBehaviour
{
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(1)] private int hitCount = 1;
    [SerializeField, Min(0f)] private float repeatDamageCooldown = 0.75f;
    [SerializeField, Min(0.01f)] private float fullDamageImpactSpeed = 14f;
    [SerializeField, Min(0f)] private float minimumDamageImpactSpeed = 0.75f;
    [SerializeField, Min(1f)] private float maximumDamageMultiplier = 2f;

    private int remainingHits;
    private float nextHitTime;
    private bool isDepleted;

    public int Damage => damage;
    public int HitCount => hitCount;
    public int RemainingHits => remainingHits;
    public float RepeatDamageCooldown => repeatDamageCooldown;

    public void Configure(
        int damageAmount,
        bool makeCollidersNonBlocking,
        int allowedHits = 1,
        float hitCooldown = 0.75f)
    {
        damage = Mathf.Max(0, damageAmount);
        hitCount = Mathf.Max(1, allowedHits);
        repeatDamageCooldown = Mathf.Max(0f, hitCooldown);
        ResetHitCount();

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
        if (block == null || amount <= 0 || isDepleted || Time.time < nextHitTime)
        {
            return false;
        }

        nextHitTime = Time.time + repeatDamageCooldown;
        block.TakeDamage(amount);
        remainingHits--;

        ObstacleDescriptor descriptor = GetComponent<ObstacleDescriptor>();
        bool isIceberg = descriptor != null && descriptor.Kind == ObstacleKind.Iceberg;

        if (remainingHits <= 0)
        {
            isDepleted = true;
            if (isIceberg)
            {
                GameAudio.PlayIcebergBreak(transform.position);
            }
            Destroy(gameObject);
        }
        else if (isIceberg)
        {
            GameAudio.PlayIceCrash(block.transform.position);
        }

        return true;
    }

    private void OnEnable()
    {
        ResetHitCount();
    }

    private void ResetHitCount()
    {
        remainingHits = Mathf.Max(1, hitCount);
        nextHitTime = 0f;
        isDepleted = false;
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        hitCount = Mathf.Max(1, hitCount);
        repeatDamageCooldown = Mathf.Max(0f, repeatDamageCooldown);
        fullDamageImpactSpeed = Mathf.Max(0.01f, fullDamageImpactSpeed);
        minimumDamageImpactSpeed = Mathf.Max(0f, minimumDamageImpactSpeed);
        maximumDamageMultiplier = Mathf.Max(1f, maximumDamageMultiplier);
    }
}
