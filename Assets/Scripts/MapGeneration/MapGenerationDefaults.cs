namespace RoyaltyBoat.MapGeneration
{
    /// <summary>
    /// Canonical defaults shared by runtime generation and editor-authored content.
    /// Generated prefabs may serialize copies of these values, but their builders always
    /// derive those copies from this source.
    /// </summary>
    public static class MapGenerationDefaults
    {
        public const float ChunkLength = 80f;
        public const float CourseWidth = 54f;

        public const int BaseHazardChunkCount = 6;
        public const int LevelsPerExtraChunk = 2;
        public const int MaximumHazardChunkCount = 10;

        public const float BaseDifficultyBudget = 10f;
        public const float DifficultyPerLevel = 3f;
    }
}
