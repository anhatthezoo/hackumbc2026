using UnityEngine;

/// <summary>
/// Lives beside an obstacle collider so compound Rigidbody callbacks retain the
/// exact ship block that touched that collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class ObstacleDamageRelay : MonoBehaviour
{
    private ObstacleDamage source;

    public void Configure(ObstacleDamage damageSource)
    {
        source = damageSource;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider, ObstacleDamage.GetImpactSpeed(collision));
    }

    private void TryDamage(Collider other)
    {
        TryDamage(other, null);
    }

    private void TryDamage(Collider other, float? impactSpeed)
    {
        if (source == null || other == null)
        {
            return;
        }

        Block block = other.GetComponentInParent<Block>();
        if (block != null)
        {
            if (impactSpeed.HasValue)
            {
                source.TryDamage(block, impactSpeed.Value);
            }
            else
            {
                source.TryDamage(block);
            }
        }
    }
}
