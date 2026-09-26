using System.Collections.Generic;
using UnityEngine;

namespace RoyaltyBoat.MapGeneration
{
    [CreateAssetMenu(menuName = "Royalty Boat/Map Generation/Chunk Catalog")]
    public sealed class LevelChunkCatalog : ScriptableObject
    {
        [SerializeField] private LevelChunkDefinition openingChunk;
        [SerializeField] private LevelChunkDefinition cooldownChunk;
        [SerializeField] private List<LevelChunkDefinition> hazardChunks = new List<LevelChunkDefinition>();

        public LevelChunkDefinition OpeningChunk => openingChunk;
        public LevelChunkDefinition CooldownChunk => cooldownChunk;
        public IReadOnlyList<LevelChunkDefinition> HazardChunks => hazardChunks;

        public void Configure(
            LevelChunkDefinition opening,
            LevelChunkDefinition cooldown,
            IEnumerable<LevelChunkDefinition> hazards)
        {
            openingChunk = opening;
            cooldownChunk = cooldown;
            hazardChunks.Clear();
            if (hazards != null)
            {
                hazardChunks.AddRange(hazards);
            }
        }
    }
}
