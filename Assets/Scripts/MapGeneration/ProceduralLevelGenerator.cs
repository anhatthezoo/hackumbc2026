using System;
using System.Collections.Generic;
using RoyaltyBoat.Obstacles;
using UnityEngine;

namespace RoyaltyBoat.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class ProceduralLevelGenerator : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private LevelChunkCatalog catalog;
        [SerializeField] private Transform generatedContentParent;

        [Header("Length")]
        [SerializeField, Min(1)] private int baseHazardChunkCount = MapGenerationDefaults.BaseHazardChunkCount;
        [SerializeField, Min(0f)] private float hazardChunkGap = MapGenerationDefaults.HazardChunkGap;

        [Header("Difficulty")]
        [SerializeField, Min(0f)] private float baseDifficultyBudget = MapGenerationDefaults.BaseDifficultyBudget;
        [SerializeField, Min(0f)] private float difficultyPerLevel = MapGenerationDefaults.DifficultyPerLevel;

        [Header("Optional Preview")]
        [SerializeField] private bool generateOnStart;
        [SerializeField] private int previewRunSeed = 12345;
        [SerializeField, Min(1)] private int previewLevel = 1;

        private readonly List<GeneratedChunkInstance> generatedChunks = new List<GeneratedChunkInstance>();
        private Transform generatedRoot;

        public IReadOnlyList<GeneratedChunkInstance> GeneratedChunks => generatedChunks;
        public Transform GeneratedRoot => generatedRoot;
        public float GeneratedLength { get; private set; }
        public int ActiveRunSeed { get; private set; }
        public int ActiveLevelNumber { get; private set; }
        public float HazardChunkGap => hazardChunkGap;

        public event Action<int, int, float> CourseGenerated;
        public event Action CourseCleared;

        public int GetHazardChunkCount(int levelNumber)
        {
            int safeLevel = Mathf.Max(1, levelNumber);
            long count = (long)baseHazardChunkCount + safeLevel - 1L;
            return (int)Math.Min(count, int.MaxValue);
        }

        public int GetObstacleCount(int levelNumber)
        {
            int safeLevel = Mathf.Max(1, levelNumber);
            long count = 3L;

            for (int level = 1; level < safeLevel; level++)
            {
                count = count * 3L / 2L;
                if (count >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }

            return (int)count;
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateLevel(previewRunSeed, previewLevel);
            }
        }

        public void Configure(LevelChunkCatalog chunkCatalog)
        {
            catalog = chunkCatalog;
        }

        public void GenerateLevel(int runSeed, int levelNumber)
        {
            if (catalog == null)
            {
                Debug.LogError("Cannot generate a course without a LevelChunkCatalog.", this);
                return;
            }

            if (catalog.OpeningChunk == null || catalog.CooldownChunk == null || catalog.HazardChunks.Count == 0)
            {
                Debug.LogError("The chunk catalog requires opening, hazard, and cooldown definitions.", catalog);
                return;
            }

            ClearLevel();
            ActiveRunSeed = runSeed;
            ActiveLevelNumber = Mathf.Max(1, levelNumber);

            GameObject rootObject = new GameObject($"Generated Course - Seed {runSeed} Level {ActiveLevelNumber}");
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(generatedContentParent == null ? transform : generatedContentParent, false);

            int combinedSeed = CombineSeed(runSeed, ActiveLevelNumber);
            var random = new System.Random(combinedSeed);
            int hazardCount = GetHazardChunkCount(ActiveLevelNumber);
            int selectionLevel = GetCatalogSelectionLevel(ActiveLevelNumber);
            float remainingDifficulty = baseDifficultyBudget + (ActiveLevelNumber - 1) * difficultyPerLevel;
            float cursor = 0f;
            LaneMask availableLanes = LaneMask.All;
            var lastUsedAt = new Dictionary<LevelChunkDefinition, int>();

            AppendChunk(catalog.OpeningChunk, generatedChunks.Count, ref cursor);
            availableLanes = catalog.OpeningChunk.Authoring.ExitLanes;

            for (int hazardIndex = 0; hazardIndex < hazardCount; hazardIndex++)
            {
                int remainingSlots = hazardCount - hazardIndex;
                float desiredCost = remainingSlots <= 0 ? remainingDifficulty : remainingDifficulty / remainingSlots;
                LevelChunkDefinition selected = SelectChunk(
                    random,
                    selectionLevel,
                    availableLanes,
                    desiredCost,
                    remainingDifficulty,
                    hazardIndex,
                    lastUsedAt,
                    true);

                if (selected == null)
                {
                    Debug.LogError($"No compatible map chunk was available at hazard index {hazardIndex}.", this);
                    break;
                }

                AppendChunk(selected, generatedChunks.Count, ref cursor);
                availableLanes = selected.Authoring.ExitLanes;
                remainingDifficulty -= selected.DifficultyCost;
                lastUsedAt[selected] = hazardIndex;
                cursor += hazardChunkGap;
            }

            AppendChunk(catalog.CooldownChunk, generatedChunks.Count, ref cursor);
            ApplyObstacleDensity(random);
            GeneratedLength = cursor;

            RavineCourseBoundary ravine =
                rootObject.AddComponent<RavineCourseBoundary>();
            ravine.Generate(
                GeneratedLength,
                MapGenerationDefaults.CourseWidth,
                combinedSeed);
            AlignChunksToCourse(ravine);
            CourseGenerated?.Invoke(ActiveRunSeed, ActiveLevelNumber, GeneratedLength);
        }

        public Vector3 GetCourseCenter(float distance)
        {
            RavineCourseBoundary ravine = generatedRoot == null
                ? null
                : generatedRoot.GetComponent<RavineCourseBoundary>();
            return ravine == null
                ? new Vector3(distance, 0f, 0f)
                : ravine.GetCourseCenter(distance);
        }

        public Vector3 GetCourseTangent(float distance)
        {
            RavineCourseBoundary ravine = generatedRoot == null
                ? null
                : generatedRoot.GetComponent<RavineCourseBoundary>();
            return ravine == null ? Vector3.right : ravine.GetCourseTangent(distance);
        }

        private void AlignChunksToCourse(RavineCourseBoundary ravine)
        {
            foreach (GeneratedChunkInstance chunk in generatedChunks)
            {
                if (chunk.Instance == null)
                {
                    continue;
                }

                Vector3 start = ravine.GetCourseCenter(chunk.StartX);
                Vector3 end = ravine.GetCourseCenter(chunk.EndX);
                Vector3 direction = end - start;
                direction.y = 0f;

                Transform chunkTransform = chunk.Instance.transform;
                chunkTransform.localPosition = start;
                chunkTransform.localRotation = direction.sqrMagnitude > 0.001f
                    ? Quaternion.FromToRotation(Vector3.right, direction.normalized)
                    : Quaternion.identity;
            }
        }

        public void ClearLevel()
        {
            generatedChunks.Clear();
            GeneratedLength = 0f;

            if (generatedRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(generatedRoot.gameObject);
                }
                generatedRoot = null;
            }

            CourseCleared?.Invoke();
        }

        private LevelChunkDefinition SelectChunk(
            System.Random random,
            int levelNumber,
            LaneMask availableLanes,
            float desiredCost,
            float remainingDifficulty,
            int hazardIndex,
            IReadOnlyDictionary<LevelChunkDefinition, int> lastUsedAt,
            bool requireObstacle)
        {
            var candidates = new List<LevelChunkDefinition>();
            foreach (LevelChunkDefinition definition in catalog.HazardChunks)
            {
                if (!IsCompatible(definition, levelNumber, availableLanes))
                {
                    continue;
                }

                if (requireObstacle && !ContainsObstacle(definition))
                {
                    continue;
                }

                if (lastUsedAt.TryGetValue(definition, out int lastIndex) &&
                    hazardIndex - lastIndex <= definition.RepetitionCooldown)
                {
                    continue;
                }

                candidates.Add(definition);
            }

            if (candidates.Count == 0)
            {
                foreach (LevelChunkDefinition definition in catalog.HazardChunks)
                {
                    if (IsCompatible(definition, levelNumber, availableLanes))
                    {
                        if (requireObstacle && !ContainsObstacle(definition))
                        {
                            continue;
                        }

                        candidates.Add(definition);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            bool hasAffordableCandidate = candidates.Exists(candidate => candidate.DifficultyCost <= remainingDifficulty);
            double totalWeight = 0d;
            var weights = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                LevelChunkDefinition candidate = candidates[i];
                if (hasAffordableCandidate && candidate.DifficultyCost > remainingDifficulty)
                {
                    weights[i] = 0d;
                    continue;
                }

                float difference = Mathf.Abs(candidate.DifficultyCost - desiredCost);
                double budgetFit = 1d / (1d + difference * 0.35d);
                weights[i] = candidate.SelectionWeight * budgetFit;
                totalWeight += weights[i];
            }

            if (totalWeight <= 0d)
            {
                return candidates[random.Next(candidates.Count)];
            }

            double roll = random.NextDouble() * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0d)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static bool ContainsObstacle(LevelChunkDefinition definition)
        {
            return definition != null
                && definition.Prefab != null
                && definition.Prefab.GetComponentInChildren<ObstacleDescriptor>(true) != null;
        }

        private void ApplyObstacleDensity(System.Random random)
        {
            if (generatedRoot == null)
            {
                return;
            }

            ObstacleDescriptor[] authoredObstacles =
                generatedRoot.GetComponentsInChildren<ObstacleDescriptor>(true);
            if (authoredObstacles.Length == 0)
            {
                Debug.LogWarning(
                    $"Generated level {ActiveLevelNumber} without any obstacles.",
                    this);
                return;
            }

            int targetObstacleCount = GetObstacleCount(ActiveLevelNumber);
            var activeObstacles = new List<ObstacleDescriptor>(authoredObstacles);

            while (activeObstacles.Count > targetObstacleCount)
            {
                int removeIndex = random.Next(activeObstacles.Count);
                ObstacleDescriptor removed = activeObstacles[removeIndex];
                activeObstacles.RemoveAt(removeIndex);
                RemoveObstacle(removed);
            }

            while (activeObstacles.Count < targetObstacleCount)
            {
                ObstacleDescriptor source = activeObstacles[random.Next(activeObstacles.Count)];
                if (source == null)
                {
                    continue;
                }

                float overlapChance = ActiveLevelNumber < 3
                    ? 0f
                    : Mathf.Clamp01(0.3f + (ActiveLevelNumber - 3) * 0.12f);
                bool overlap = random.NextDouble() < overlapChance;
                float distance = overlap
                    ? Mathf.Lerp(0.75f, 2.5f, (float)random.NextDouble())
                    : Mathf.Lerp(5f, 11f, (float)random.NextDouble());
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;

                GameObject duplicate = Instantiate(
                    source.gameObject,
                    source.transform.parent);
                duplicate.name = source.gameObject.name + (overlap
                    ? " - Overlap"
                    : " - Extra");
                duplicate.transform.localPosition = source.transform.localPosition
                    + new Vector3(
                        Mathf.Cos(angle) * distance,
                        0f,
                        Mathf.Sin(angle) * distance);
                duplicate.transform.localRotation = source.transform.localRotation;
                ConfigureObstacleCollisions(duplicate);

                ObstacleDescriptor duplicateDescriptor =
                    duplicate.GetComponent<ObstacleDescriptor>();
                if (duplicateDescriptor != null)
                {
                    activeObstacles.Add(duplicateDescriptor);
                }
            }

            ApplyObstacleOverlaps(random, activeObstacles);
        }

        private void ApplyObstacleOverlaps(
            System.Random random,
            IReadOnlyList<ObstacleDescriptor> obstacles)
        {
            if (ActiveLevelNumber < 3 || obstacles.Count < 2)
            {
                return;
            }

            float overlapChance = Mathf.Clamp01(
                0.3f + (ActiveLevelNumber - 3) * 0.12f);
            for (int index = 1; index < obstacles.Count; index++)
            {
                ObstacleDescriptor obstacle = obstacles[index];
                if (obstacle == null || random.NextDouble() >= overlapChance)
                {
                    continue;
                }

                int partnerIndex = random.Next(obstacles.Count - 1);
                if (partnerIndex >= index)
                {
                    partnerIndex++;
                }

                ObstacleDescriptor partner = obstacles[partnerIndex];
                if (partner == null)
                {
                    continue;
                }

                float distance = Mathf.Lerp(
                    0.75f,
                    2.5f,
                    (float)random.NextDouble());
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                obstacle.transform.position = partner.transform.position
                    + new Vector3(
                        Mathf.Cos(angle) * distance,
                        0f,
                        Mathf.Sin(angle) * distance);
            }
        }

        private int GetCatalogSelectionLevel(int levelNumber)
        {
            int highestAuthoredLevel = 1;
            foreach (LevelChunkDefinition definition in catalog.HazardChunks)
            {
                if (definition != null)
                {
                    highestAuthoredLevel = Mathf.Max(
                        highestAuthoredLevel,
                        definition.MaximumLevel);
                }
            }

            return Mathf.Min(Mathf.Max(1, levelNumber), highestAuthoredLevel);
        }

        private static void RemoveObstacle(ObstacleDescriptor obstacle)
        {
            if (obstacle == null)
            {
                return;
            }

            obstacle.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(obstacle.gameObject);
            }
            else
            {
                DestroyImmediate(obstacle.gameObject);
            }
        }

        private static bool IsCompatible(LevelChunkDefinition definition, int levelNumber, LaneMask availableLanes)
        {
            LevelChunkAuthoring authoring = definition == null ? null : definition.Authoring;
            return authoring != null &&
                   definition.SupportsLevel(levelNumber) &&
                   (authoring.EntranceLanes & availableLanes) != LaneMask.None;
        }

        private void AppendChunk(LevelChunkDefinition definition, int index, ref float cursor)
        {
            LevelChunkAuthoring authoring = definition.Authoring;
            GameObject chunkObject = Instantiate(definition.Prefab, generatedRoot);
            chunkObject.name = $"{index:00} - {authoring.ChunkId}";
            chunkObject.transform.localPosition = new Vector3(cursor, 0f, 0f);
            chunkObject.transform.localRotation = Quaternion.identity;
            ConfigureObstacleCollisions(chunkObject);

            LevelChunkAuthoring instance = chunkObject.GetComponent<LevelChunkAuthoring>();
            float start = cursor;
            cursor += instance.Length;
            generatedChunks.Add(new GeneratedChunkInstance(index, start, cursor, definition, instance));
        }

        private static void ConfigureObstacleCollisions(GameObject chunkObject)
        {
            foreach (ObstacleDescriptor descriptor in
                chunkObject.GetComponentsInChildren<ObstacleDescriptor>(true))
            {
                if (descriptor.Kind == ObstacleKind.AcidicWater)
                {
                    continue;
                }

                ObstacleDamage obstacleDamage = descriptor.GetComponent<ObstacleDamage>();
                if (obstacleDamage == null)
                {
                    obstacleDamage = descriptor.gameObject.AddComponent<ObstacleDamage>();
                }

                obstacleDamage.Configure(
                    GetContactDamage(descriptor.Kind),
                    descriptor.Kind != ObstacleKind.Iceberg,
                    descriptor.HitCount,
                    descriptor.HitCooldown);
            }
        }

        private static int GetContactDamage(ObstacleKind kind)
        {
            return kind switch
            {
                ObstacleKind.Iceberg => 35,
                ObstacleKind.FloatingDebris => 15,
                ObstacleKind.GhostShip => 25,
                _ => 10
            };
        }

        private static int CombineSeed(int runSeed, int levelNumber)
        {
            unchecked
            {
                uint value = (uint)runSeed;
                value ^= (uint)levelNumber + 0x9e3779b9u + (value << 6) + (value >> 2);
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (int)value;
            }
        }

        private void OnValidate()
        {
            baseHazardChunkCount = Mathf.Max(1, baseHazardChunkCount);
            hazardChunkGap = Mathf.Max(0f, hazardChunkGap);
            baseDifficultyBudget = Mathf.Max(0f, baseDifficultyBudget);
            difficultyPerLevel = Mathf.Max(0f, difficultyPerLevel);
            previewLevel = Mathf.Max(1, previewLevel);
        }
    }
}
