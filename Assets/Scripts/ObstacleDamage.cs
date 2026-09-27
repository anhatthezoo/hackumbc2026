using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleDamage : MonoBehaviour
{
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(0f)] private float repeatDamageCooldown = 0.75f;

    private readonly Dictionary<Block, float> nextDamageTimes = new Dictionary<Block, float>();

    public int Damage => damage;

    public void Configure(int damageAmount, bool makeCollidersNonBlocking)
    {
        damage = Mathf.Max(0, damageAmount);

        if (!makeCollidersNonBlocking)
        {
            return;
        }

        foreach (Collider obstacleCollider in GetComponentsInChildren<Collider>(true))
        {
            if (obstacleCollider is MeshCollider meshCollider && !meshCollider.convex)
            {
                Mesh sharedMesh = meshCollider.sharedMesh;
                meshCollider.enabled = false;

                BoxCollider triggerVolume = meshCollider.gameObject.AddComponent<BoxCollider>();
                if (sharedMesh != null)
                {
                    triggerVolume.center = sharedMesh.bounds.center;
                    triggerVolume.size = sharedMesh.bounds.size;
                }

                triggerVolume.isTrigger = true;
                continue;
            }

            obstacleCollider.isTrigger = true;
        }
    }

    public bool TryDamage(Block block)
    {
        if (block == null || damage <= 0)
        {
            return false;
        }

        if (nextDamageTimes.TryGetValue(block, out float nextTime) && Time.time < nextTime)
        {
            return false;
        }

        nextDamageTimes[block] = Time.time + repeatDamageCooldown;
        block.TakeDamage(damage);
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
    }
}
