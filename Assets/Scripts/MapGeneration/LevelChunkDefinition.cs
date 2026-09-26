using UnityEngine;

namespace RoyaltyBoat.MapGeneration
{
    [CreateAssetMenu(menuName = "Royalty Boat/Map Generation/Chunk Definition")]
    public sealed class LevelChunkDefinition : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.01f)] private float selectionWeight = 1f;
        [SerializeField, Min(0f)] private float difficultyCost = 1f;
        [SerializeField, Min(1)] private int minimumLevel = 1;
        [SerializeField, Min(1)] private int maximumLevel = 99;
        [SerializeField, Min(0)] private int repetitionCooldown = 1;

        public GameObject Prefab => prefab;
        public float SelectionWeight => selectionWeight;
        public float DifficultyCost => difficultyCost;
        public int MinimumLevel => minimumLevel;
        public int MaximumLevel => maximumLevel;
        public int RepetitionCooldown => repetitionCooldown;
        public LevelChunkAuthoring Authoring => prefab == null ? null : prefab.GetComponent<LevelChunkAuthoring>();
        public string ChunkId => Authoring == null ? name : Authoring.ChunkId;

        public bool SupportsLevel(int levelNumber)
        {
            return levelNumber >= minimumLevel && levelNumber <= maximumLevel;
        }

        public void Configure(
            GameObject chunkPrefab,
            float weight,
            float cost,
            int minLevel,
            int maxLevel,
            int cooldown)
        {
            prefab = chunkPrefab;
            selectionWeight = Mathf.Max(0.01f, weight);
            difficultyCost = Mathf.Max(0f, cost);
            minimumLevel = Mathf.Max(1, minLevel);
            maximumLevel = Mathf.Max(minimumLevel, maxLevel);
            repetitionCooldown = Mathf.Max(0, cooldown);
        }

        private void OnValidate()
        {
            selectionWeight = Mathf.Max(0.01f, selectionWeight);
            difficultyCost = Mathf.Max(0f, difficultyCost);
            minimumLevel = Mathf.Max(1, minimumLevel);
            maximumLevel = Mathf.Max(minimumLevel, maximumLevel);
            repetitionCooldown = Mathf.Max(0, repetitionCooldown);
        }
    }
}
