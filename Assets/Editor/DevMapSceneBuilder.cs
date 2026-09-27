using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoyaltyBoat.MapGeneration;
using RoyaltyBoat.Obstacles;
using RoyaltyBoat.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DevMapSceneBuilder
{
    private const string VoyageScenePath = "Assets/Scenes/Voyage.unity";
    private const string DevMapScenePath = "Assets/Scenes/DevMap.unity";
    private const string ObstacleFolder = "Assets/Obstacles/Prefabs";

    private readonly struct Placement
    {
        public Placement(string prefabName, float x, float z, float yaw)
        {
            PrefabName = prefabName;
            Position = new Vector3(x, 0f, z);
            Yaw = yaw;
        }

        public string PrefabName { get; }
        public Vector3 Position { get; }
        public float Yaw { get; }
    }

    [MenuItem("Tools/Royalty Boat/Rebuild Dev Map Scene")]
    public static void Build()
    {
        if (!File.Exists(VoyageScenePath))
        {
            throw new FileNotFoundException("The Voyage scene is missing.", VoyageScenePath);
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DevMapScenePath) != null)
        {
            AssetDatabase.DeleteAsset(DevMapScenePath);
        }

        if (!AssetDatabase.CopyAsset(VoyageScenePath, DevMapScenePath))
        {
            throw new System.InvalidOperationException("Could not clone Voyage into DevMap.");
        }

        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(DevMapScenePath, OpenSceneMode.Single);

        ProceduralLevelGenerator generator = Object.FindAnyObjectByType<ProceduralLevelGenerator>();
        if (generator != null)
        {
            Object.DestroyImmediate(generator.gameObject);
        }

        GameObject courseRoot = new GameObject("Dev Map Course - Dense Showcase");
        courseRoot.AddComponent<DevMapCourse>().Configure(326f);
        courseRoot.AddComponent<RainSection>().Configure(78f, MapGenerationDefaults.CourseWidth);
        courseRoot.AddComponent<LightningStormSection>();

        Placement[] placements =
        {
            // Fire: two full-width oil bands cover X 78 through 150.
            new Placement("AcidicWater", 96f, 0f, 0f),
            new Placement("AcidicWater", 132f, 0f, 0f),

            // Icebergs: X 150 through 230.
            new Placement("Iceberg", 155f, -20f, 12f),
            new Placement("Iceberg", 169f, 18f, -15f),
            new Placement("Iceberg", 183f, -4f, 18f),
            new Placement("Iceberg", 197f, 22f, -12f),
            new Placement("Iceberg", 211f, -21f, 16f),
            new Placement("Iceberg", 225f, 3f, -18f),

            // Logs: X 230 through 310.
            new Placement("FloatingLog", 233f, -20f, 24f),
            new Placement("FloatingLog", 244f, 12f, -28f),
            new Placement("FloatingLog", 255f, 23f, 18f),
            new Placement("FloatingLog", 266f, -7f, -22f),
            new Placement("FloatingLog", 277f, -24f, 30f),
            new Placement("FloatingLog", 288f, 8f, -18f),
            new Placement("FloatingLog", 299f, 21f, 26f),
            new Placement("FloatingLog", 307f, -12f, -24f)
        };

        foreach (Placement placement in placements)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{ObstacleFolder}/{placement.PrefabName}.prefab");
            if (source == null)
            {
                throw new FileNotFoundException(
                    $"Missing dev-map obstacle prefab {placement.PrefabName}.");
            }

            GameObject obstacle = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            obstacle.name = $"{placement.PrefabName} {placement.Position.x:000}";
            obstacle.transform.SetParent(courseRoot.transform, false);
            obstacle.transform.localPosition = placement.Position;
            obstacle.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built the dense DevMap scene with rain, lightning, fire, icebergs, and logs.");
    }

    [MenuItem("Tools/Royalty Boat/Validate Dev Map Scene")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(DevMapScenePath, OpenSceneMode.Single);
        DevMapCourse course = Object.FindAnyObjectByType<DevMapCourse>();
        RainSection rain = Object.FindAnyObjectByType<RainSection>();
        LightningStormSection lightning = Object.FindAnyObjectByType<LightningStormSection>();

        if (course == null || rain == null || lightning == null)
        {
            throw new System.InvalidOperationException("DevMap is missing its course weather components.");
        }

        int icebergs = CountObstacles(course, ObstacleKind.Iceberg);
        int logs = CountObstacles(course, ObstacleKind.FloatingDebris);
        int fire = CountObstacles(course, ObstacleKind.AcidicWater);
        if (icebergs < 1 || logs < 1 || fire < 1)
        {
            throw new System.InvalidOperationException("DevMap is missing one or more showcase obstacle types.");
        }

        Debug.Log($"Validated DevMap: {icebergs} icebergs, {logs} logs, {fire} fire slicks.");
    }

    private static int CountObstacles(DevMapCourse course, ObstacleKind kind)
    {
        return course.GetComponentsInChildren<ObstacleDescriptor>(true)
            .Count(descriptor => descriptor.Kind == kind);
    }

    private static void AddSceneToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Any(scene => scene.path == DevMapScenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(DevMapScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
