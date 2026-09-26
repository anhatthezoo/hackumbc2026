using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    /// <summary>
    /// Implement this on future boat modules or boat roots that can take environmental damage.
    /// Hazard volumes use the collider's attached Rigidbody/root to avoid damaging one object
    /// multiple times when it owns several colliders.
    /// </summary>
    public interface IHazardDamageReceiver
    {
        void ApplyHazardDamage(float amount, HazardType hazardType, GameObject source);
    }
}
