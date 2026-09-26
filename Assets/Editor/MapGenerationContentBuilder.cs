using System;
using System.Collections.Generic;
using System.IO;
using RoyaltyBoat.MapGeneration;
using RoyaltyBoat.Obstacles;
using UnityEditor;
using UnityEngine;

public static class MapGenerationContentBuilder
{
    private const string RootFolder = "Assets/MapGeneration";
    private const string ChunkFolder = RootFolder + "/Chunks";
    private const string DefinitionFolder = RootFolder + "/Definitions";
    private const string CatalogFolder = RootFolder + "/Catalogs";
    private const string PrefabFolder = RootFolder + "/Prefabs";

    private readonly struct Placement
    {
        public Placement(string prefabPath, Vector3 position, float yaw, string name)
        {
            PrefabPath = prefabPath;
            Position = position;
            Yaw = yaw;
            Name = name;
        }

        public string PrefabPath { get; }
        public Vector3 Position { get; }
        public float Yaw { get; }
        public string Name { get; }
    }

    [MenuItem("Tools/Royalty Boat/Rebuild Map Generation Content")]
    public static void BuildAll()
    {
        EnsureFolders();

        GameObject opening = BuildChunk("OpenWater", "open-water", LaneMask.All, LaneMask.All,
            Route(0f, 0f, 80f, 0f));
        GameObject cooldown = BuildChunk("Cooldown", "cooldown", LaneMask.All, LaneMask.All,
            Route(0f, 0f, 80f, 0f));

        GameObject icebergSlalom = BuildChunk(
            "IcebergSlalom",
            "iceberg-slalom",
            LaneMask.All,
            LaneMask.All,
            Route(0f, 0f, 16f, 14f, 34f, -14f, 54f, 14f, 80f, 0f),
            Place("Iceberg", 17f, -14f, 12f),
            Place("Iceberg", 36f, 14f, -18f),
            Place("Iceberg", 56f, -14f, 20f));

        GameObject debrisField = BuildChunk(
            "DebrisField",
            "debris-field",
            LaneMask.All,
            LaneMask.All,
            Route(0f, 0f, 18f, 11f, 36f, -11f, 56f, 10f, 80f, 0f),
            Place("FloatingLog", 17f, -10f, 24f),
            Place("DebrisCluster", 31f, 12f, -15f),
            Place("FloatingLog", 45f, 2f, -28f),
            Place("DebrisCluster", 61f, -13f, 21f));

        GameObject acidSafeRight = BuildChunk(
            "AcidSafeRight",
            "acid-safe-right",
            LaneMask.All,
            LaneMask.Right,
            Route(0f, 0f, 16f, 18f, 64f, 18f, 80f, 18f),
            Place("AcidicWater", 25f, -9f, 0f),
            Place("AcidicWater", 47f, -9f, 0f),
            Place("AcidicWater", 67f, -9f, 0f));

        GameObject narrowPassage = BuildChunk(
            "NarrowIcebergPassage",
            "narrow-iceberg-passage",
            LaneMask.All,
            LaneMask.Center,
            Route(0f, 0f, 80f, 0f),
            Place("Iceberg", 30f, -15f, 10f),
            Place("Iceberg", 30f, 15f, -12f),
            Place("Iceberg", 54f, -15f, -8f),
            Place("Iceberg", 54f, 15f, 14f));

        GameObject iceAndDebris = BuildChunk(
            "IceAndDebris",
            "ice-and-debris",
            LaneMask.All,
            LaneMask.Left | LaneMask.Center,
            Route(0f, 0f, 18f, -15f, 45f, -15f, 62f, 0f, 80f, 0f),
            Place("Iceberg", 23f, 11f, 16f),
            Place("FloatingLog", 38f, -5f, -20f),
            Place("DebrisCluster", 52f, 12f, 10f),
            Place("Iceberg", 66f, -15f, -11f));

        GameObject acidDebris = BuildChunk(
            "AcidDebrisGauntlet",
            "acid-debris-gauntlet",
            LaneMask.All,
            LaneMask.Left | LaneMask.Right,
            Route(0f, 0f, 16f, -18f, 38f, -18f, 56f, 18f, 80f, 18f),
            Place("AcidicWater", 24f, 8f, 0f),
            Place("AcidicWater", 55f, -8f, 0f),
            Place("FloatingLog", 37f, -14f, 32f),
            Place("DebrisCluster", 67f, 15f, -18f));

        GameObject ghostShipEncounter = BuildChunk(
            "GhostShipEncounter",
            "ghost-ship-encounter",
            LaneMask.All,
            LaneMask.All,
            Route(0f, 0f, 18f, 11f, 42f, 15f, 64f, 4f, 80f, 0f),
            Place("GhostShip", 24f, -22f, 0f));

        LevelChunkDefinition openingDefinition = BuildDefinition("OpenWater", opening, 1f, 0f, 1, 99, 0);
        LevelChunkDefinition cooldownDefinition = BuildDefinition("Cooldown", cooldown, 1f, 0f, 1, 99, 0);
        var hazards = new List<LevelChunkDefinition>
        {
            BuildDefinition("IcebergSlalom", icebergSlalom, 3f, 2f, 1, 99, 1),
            BuildDefinition("DebrisField", debrisField, 3.5f, 1.5f, 1, 99, 1),
            BuildDefinition("AcidSafeRight", acidSafeRight, 2.5f, 2.5f, 1, 99, 1),
            BuildDefinition("NarrowIcebergPassage", narrowPassage, 1.8f, 3.5f, 2, 99, 2),
            BuildDefinition("IceAndDebris", iceAndDebris, 2.2f, 3f, 2, 99, 1),
            BuildDefinition("AcidDebrisGauntlet", acidDebris, 1.4f, 4f, 3, 99, 2),
            BuildDefinition("GhostShipEncounter", ghostShipEncounter, 1.2f, 4.5f, 3, 99, 2)
        };

        LevelChunkCatalog catalog = LoadOrCreate<LevelChunkCatalog>(CatalogFolder + "/DefaultLevelCatalog.asset");
        catalog.Configure(openingDefinition, cooldownDefinition, hazards);
        EditorUtility.SetDirty(catalog);
        BuildGeneratorPrefab(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built the default procedural map chunk library and generator prefab.");
    }

    public static void BuildAndValidate()
    {
        BuildAll();
        ValidateAll();
    }

    [MenuItem("Tools/Royalty Boat/Validate Map Generation Content")]
    public static void ValidateAll()
    {
        LevelChunkCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelChunkCatalog>(CatalogFolder + "/DefaultLevelCatalog.asset");
        if (catalog == null || catalog.OpeningChunk == null || catalog.CooldownChunk == null || catalog.HazardChunks.Count == 0)
        {
            throw new InvalidOperationException("The default level catalog is incomplete.");
        }

        foreach (LevelChunkDefinition definition in catalog.HazardChunks)
        {
            ValidateDefinition(definition, requireObstacle: true);
        }
        ValidateDefinition(catalog.OpeningChunk, requireObstacle: false);
        ValidateDefinition(catalog.CooldownChunk, requireObstacle: false);

        GameObject generatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/ProceduralLevelGenerator.prefab");
        if (generatorPrefab == null)
        {
            throw new InvalidOperationException("Missing ProceduralLevelGenerator prefab.");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(generatorPrefab);
        try
        {
            ProceduralLevelGenerator generator = instance.GetComponent<ProceduralLevelGenerator>();
            string firstSequence = GenerateAndDescribe(generator, 4831, 1);
            string repeatedSequence = GenerateAndDescribe(generator, 4831, 1);
            if (!string.Equals(firstSequence, repeatedSequence, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Identical seeds did not produce identical chunk sequences.");
            }

            string alternateSequence = GenerateAndDescribe(generator, 92817, 1);
            if (string.Equals(firstSequence, alternateSequence, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Validation seeds unexpectedly produced the same chunk sequence.");
            }

            for (int level = 1; level <= 8; level++)
            {
                GenerateAndDescribe(generator, 1200 + level * 17, level);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        Debug.Log("Validated deterministic procedural generation for levels 1 through 8.");
    }

    private static string GenerateAndDescribe(ProceduralLevelGenerator generator, int runSeed, int levelNumber)
    {
        generator.GenerateLevel(runSeed, levelNumber);
        int expectedHazards = generator.GetHazardChunkCount(levelNumber);
        int expectedCount = expectedHazards + 2;
        if (generator.GeneratedChunks.Count != expectedCount)
        {
            throw new InvalidOperationException($"Level {levelNumber} generated {generator.GeneratedChunks.Count} chunks; expected {expectedCount}.");
        }

        float cursor = 0f;
        var ids = new List<string>();
        foreach (GeneratedChunkInstance chunk in generator.GeneratedChunks)
        {
            if (!Mathf.Approximately(chunk.StartX, cursor))
            {
                throw new InvalidOperationException($"Chunk {chunk.Index} is not contiguous with its predecessor.");
            }
            cursor = chunk.EndX;
            ids.Add(chunk.ChunkId);
        }

        if (!Mathf.Approximately(generator.GeneratedLength, cursor))
        {
            throw new InvalidOperationException("GeneratedLength does not match the final chunk boundary.");
        }

        if (ids[0] != "open-water" || ids[ids.Count - 1] != "cooldown")
        {
            throw new InvalidOperationException("Generated course is missing its opening or cooldown chunk.");
        }

        return string.Join(",", ids);
    }

    private static void ValidateDefinition(LevelChunkDefinition definition, bool requireObstacle)
    {
        if (definition == null || definition.Prefab == null || definition.Authoring == null)
        {
            throw new InvalidOperationException("A chunk definition has no valid authored prefab.");
        }

        LevelChunkAuthoring authoring = definition.Authoring;
        if (!Mathf.Approximately(authoring.Length, MapGenerationDefaults.ChunkLength) || authoring.SafeRoute == null || authoring.SafeRoute.Length < 2)
        {
            throw new InvalidOperationException($"Chunk {definition.ChunkId} has invalid dimensions or no safe route.");
        }

        if (requireObstacle && definition.Prefab.GetComponentsInChildren<ObstacleDescriptor>(true).Length == 0)
        {
            throw new InvalidOperationException($"Hazard chunk {definition.ChunkId} contains no obstacles.");
        }
    }

    private static GameObject BuildChunk(
        string assetName,
        string chunkId,
        LaneMask entrances,
        LaneMask exits,
        Vector3[] safeRoute,
        params Placement[] placements)
    {
        GameObject root = new GameObject(assetName);
        try
        {
            root.AddComponent<LevelChunkAuthoring>().Configure(
                chunkId,
                MapGenerationDefaults.ChunkLength,
                MapGenerationDefaults.CourseWidth,
                entrances,
                exits,
                safeRoute);
            CreateBoundaryMarker(root.transform, "Entrance", Vector3.zero);
            CreateBoundaryMarker(root.transform, "Exit", new Vector3(MapGenerationDefaults.ChunkLength, 0f, 0f));
            CreateRouteMarkers(root.transform, safeRoute);

            foreach (Placement placement in placements)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(placement.PrefabPath);
                if (source == null)
                {
                    throw new InvalidOperationException($"Missing obstacle prefab: {placement.PrefabPath}");
                }

                GameObject obstacle = (GameObject)PrefabUtility.InstantiatePrefab(source);
                obstacle.name = placement.Name;
                obstacle.transform.SetParent(root.transform, false);
                obstacle.transform.localPosition = placement.Position;
                obstacle.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
            }

            return PrefabUtility.SaveAsPrefabAsset(root, ChunkFolder + "/" + assetName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateBoundaryMarker(Transform parent, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = position;
    }

    private static void CreateRouteMarkers(Transform parent, Vector3[] route)
    {
        GameObject routeRoot = new GameObject("Safe Route (Authoring)");
        routeRoot.transform.SetParent(parent, false);
        for (int i = 0; i < route.Length; i++)
        {
            GameObject marker = new GameObject($"Point {i + 1:00}");
            marker.transform.SetParent(routeRoot.transform, false);
            marker.transform.localPosition = route[i];
        }
    }

    private static LevelChunkDefinition BuildDefinition(
        string assetName,
        GameObject prefab,
        float weight,
        float cost,
        int minimumLevel,
        int maximumLevel,
        int cooldown)
    {
        LevelChunkDefinition definition = LoadOrCreate<LevelChunkDefinition>(DefinitionFolder + "/" + assetName + ".asset");
        definition.Configure(prefab, weight, cost, minimumLevel, maximumLevel, cooldown);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void BuildGeneratorPrefab(LevelChunkCatalog catalog)
    {
        GameObject root = new GameObject("Procedural Level Generator");
        try
        {
            root.AddComponent<ProceduralLevelGenerator>().Configure(catalog);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/ProceduralLevelGenerator.prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Placement Place(string prefabName, float x, float z, float yaw)
    {
        string path = $"Assets/Obstacles/Prefabs/{prefabName}.prefab";
        return new Placement(path, new Vector3(x, 0f, z), yaw, $"{prefabName} at {x:0}");
    }

    private static Vector3[] Route(params float[] coordinates)
    {
        if (coordinates.Length < 4 || coordinates.Length % 2 != 0)
        {
            throw new ArgumentException("A safe route requires X/Z coordinate pairs.");
        }

        var points = new Vector3[coordinates.Length / 2];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector3(coordinates[i * 2], 0f, coordinates[i * 2 + 1]);
        }
        return points;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(RootFolder);
        Directory.CreateDirectory(ChunkFolder);
        Directory.CreateDirectory(DefinitionFolder);
        Directory.CreateDirectory(CatalogFolder);
        Directory.CreateDirectory(PrefabFolder);
    }
}
