using RoyaltyBoat.Obstacles;
using UnityEngine;

namespace RoyaltyBoat.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class DevMapCourse : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float finishX = 172f;
        private bool obstaclesConfigured;

        public float FinishX => finishX;

        public void Configure(float finishPositionX)
        {
            finishX = Mathf.Max(1f, finishPositionX);
        }

        private void Awake()
        {
            ConfigureObstacleDamage();
        }

        public void ConfigureObstacleDamage()
        {
            if (obstaclesConfigured)
            {
                return;
            }

            obstaclesConfigured = true;
            foreach (ObstacleDescriptor descriptor in GetComponentsInChildren<ObstacleDescriptor>(true))
            {
                if (descriptor.Kind == ObstacleKind.AcidicWater)
                {
                    continue;
                }

                ObstacleDamage damage = descriptor.GetComponent<ObstacleDamage>();
                if (damage == null)
                {
                    damage = descriptor.gameObject.AddComponent<ObstacleDamage>();
                }

                int amount = descriptor.Kind == ObstacleKind.Iceberg ? 35 : 15;
                damage.Configure(amount, true);
            }
        }

        private void OnValidate()
        {
            finishX = Mathf.Max(1f, finishX);
        }
    }
}
