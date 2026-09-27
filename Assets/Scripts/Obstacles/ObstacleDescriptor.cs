using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    public enum ObstacleKind
    {
        Iceberg,
        FloatingDebris,
        AcidicWater,
        GhostShip
    }

    /// <summary>
    /// Lightweight metadata consumed by the future map generator. It deliberately contains
    /// no spawning logic so authored obstacle prefabs remain usable by any generator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObstacleDescriptor : MonoBehaviour
    {
        [SerializeField] private string obstacleId = "obstacle";
        [SerializeField] private ObstacleKind kind;
        [SerializeField, Min(0f)] private float difficultyCost = 1f;
        [SerializeField] private Vector2 footprint = new Vector2(5f, 5f);

        [Header("Contact Durability")]
        [Tooltip("Successful ship impacts required before this obstacle destroys itself.")]
        [SerializeField, Min(1)] private int hitCount = 1;
        [Tooltip("Minimum time before this obstacle can count another ship impact.")]
        [SerializeField, Min(0f)] private float hitCooldown = 0.75f;

        public string ObstacleId => obstacleId;
        public ObstacleKind Kind => kind;
        public float DifficultyCost => difficultyCost;
        public Vector2 Footprint => footprint;
        public int HitCount => hitCount;
        public float HitCooldown => hitCooldown;

        public void Configure(
            string id,
            ObstacleKind obstacleKind,
            float cost,
            Vector2 size,
            int requiredHits = 1,
            float cooldown = 0.75f)
        {
            obstacleId = string.IsNullOrWhiteSpace(id) ? name : id;
            kind = obstacleKind;
            difficultyCost = Mathf.Max(0f, cost);
            footprint = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
            hitCount = Mathf.Max(1, requiredHits);
            hitCooldown = Mathf.Max(0f, cooldown);
        }

        private void OnValidate()
        {
            difficultyCost = Mathf.Max(0f, difficultyCost);
            footprint.x = Mathf.Max(0f, footprint.x);
            footprint.y = Mathf.Max(0f, footprint.y);
            hitCount = Mathf.Max(1, hitCount);
            hitCooldown = Mathf.Max(0f, hitCooldown);
        }
    }
}
