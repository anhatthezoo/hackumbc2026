using RoyaltyBoat.Economy;
using RoyaltyBoat.Flow;
using RoyaltyBoat.King;
using UnityEngine;

namespace RoyaltyBoat.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class VoyageFinishPoint : MonoBehaviour
    {
        private static readonly Color FinishGold = new Color(1f, 0.67f, 0.08f);

        private Ship targetShip;
        private int levelNumber;
        private bool completed;

        public void Configure(Ship ship, int level)
        {
            targetShip = ship;
            levelNumber = Mathf.Max(1, level);

            BoxCollider finishTrigger = GetComponent<BoxCollider>();
            finishTrigger.isTrigger = true;
            finishTrigger.center = new Vector3(0f, 3f, 0f);
            finishTrigger.size = new Vector3(2f, 12f, 60f);

            CreateGateVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (completed || targetShip == null)
            {
                return;
            }

            Ship arrivingShip = other.GetComponentInParent<Ship>();
            if (arrivingShip != targetShip)
            {
                return;
            }

            CompleteVoyage();
        }

        private void CompleteVoyage()
        {
            completed = true;
            float kingHealth = ResolveKingHealth();
            LevelRewardResult reward = LevelRewardCalculator.Calculate(levelNumber, kingHealth);
            EconomyAccess.AddFunds(reward.TotalReward);
            VoyageFlow.MarkLevelComplete(levelNumber);

            BoatMovementController movement = targetShip.GetComponent<BoatMovementController>();
            if (movement != null)
            {
                movement.enabled = false;
            }

            Rigidbody body = targetShip.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 velocity = body.linearVelocity;
                body.linearVelocity = new Vector3(0f, velocity.y, 0f);
            }

            VoyageShipPresentation presentation = targetShip.GetComponent<VoyageShipPresentation>();
            presentation?.ShowLevelComplete(levelNumber, kingHealth, reward);

            Debug.Log(
                $"Level {levelNumber} complete. King health: {kingHealth:P0}. " +
                $"Awarded {reward.TotalReward} coins.",
                this);
        }

        private float ResolveKingHealth()
        {
            KingHealth health = targetShip.GetComponentInChildren<KingHealth>(true);
            if (health == null)
            {
                health = FindAnyObjectByType<KingHealth>();
            }

            if (health != null)
            {
                return health.IsAlive ? health.NormalizedHealth : 0f;
            }

            // Ships no longer require designated core or king blocks. Older
            // voyage setups without a KingHealth component fall back to the
            // ship's current neutral anchor so completion still awards a
            // sensible health-scaled reward.
            Block fallbackBlock = targetShip.AnchorBlock;
            return fallbackBlock == null || fallbackBlock.MaxHealth <= 0
                ? 0f
                : Mathf.Clamp01((float)fallbackBlock.Health / fallbackBlock.MaxHealth);
        }

        private void CreateGateVisuals()
        {
            Material gateMaterial = CreateGateMaterial();
            CreateGatePart("Port Finish Post", new Vector3(0f, 3f, -18f),
                new Vector3(1.2f, 6f, 1.2f), gateMaterial);
            CreateGatePart("Starboard Finish Post", new Vector3(0f, 3f, 18f),
                new Vector3(1.2f, 6f, 1.2f), gateMaterial);
            CreateGatePart("Finish Beam", new Vector3(0f, 6f, 0f),
                new Vector3(1.2f, 0.8f, 37.2f), gateMaterial);
        }

        private Transform CreateGatePart(
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                Destroy(partCollider);
            }

            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material CreateGateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = "Runtime Finish Gate Gold",
                color = FinishGold
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", FinishGold);
            }

            return material;
        }
    }
}
