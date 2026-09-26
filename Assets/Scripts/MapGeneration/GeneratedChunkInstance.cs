namespace RoyaltyBoat.MapGeneration
{
    public readonly struct GeneratedChunkInstance
    {
        public GeneratedChunkInstance(int index, float startX, float endX, LevelChunkDefinition definition, LevelChunkAuthoring instance)
        {
            Index = index;
            StartX = startX;
            EndX = endX;
            Definition = definition;
            Instance = instance;
        }

        public int Index { get; }
        public float StartX { get; }
        public float EndX { get; }
        public LevelChunkDefinition Definition { get; }
        public LevelChunkAuthoring Instance { get; }
        public string ChunkId => Definition == null ? string.Empty : Definition.ChunkId;
    }
}
