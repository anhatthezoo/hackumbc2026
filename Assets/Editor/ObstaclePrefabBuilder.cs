using System.Collections.Generic;
using System.IO;
using RoyaltyBoat.Obstacles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ObstaclePrefabBuilder
{
    private const string RootFolder = "Assets/Obstacles";
    private const string MaterialFolder = RootFolder + "/Materials";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string PrefabFolder = RootFolder + "/Prefabs";

    [MenuItem("Tools/Royalty Boat/Rebuild Obstacle Prefabs")]
    public static void BuildAll()
    {
        EnsureFolders();

        Material ice = CreateLitMaterial("Iceberg", new Color(0.55f, 0.86f, 0.94f), 0.05f, 0.3f);
        Material snow = CreateLitMaterial("IcebergSnow", new Color(0.9f, 0.98f, 1f), 0f, 0.18f);
        Material wood = CreateLitMaterial("Driftwood", new Color(0.34f, 0.16f, 0.065f), 0f, 0.12f);
        Material darkWood = CreateLitMaterial("DriftwoodDark", new Color(0.16f, 0.075f, 0.035f), 0f, 0.08f);
        Material metal = CreateLitMaterial("DebrisMetal", new Color(0.18f, 0.22f, 0.24f), 0.45f, 0.18f);
        Material acid = CreateTransparentMaterial("AcidSurface", new Color(0.38f, 0.95f, 0.08f, 0.68f), new Color(0.16f, 0.8f, 0.02f));
        Material acidBubble = CreateLitMaterial("AcidBubble", new Color(0.6f, 1f, 0.1f), 0f, 0.35f, new Color(0.25f, 1f, 0.02f));

        Mesh icebergMesh = CreateIcebergMesh();
        BuildIceberg(icebergMesh, ice, snow);
        BuildFloatingLog(wood, darkWood);
        BuildDebrisCluster(wood, darkWood, metal);
        BuildAcidWater(acid, acidBubble);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built iceberg, floating log, debris cluster, and acidic water prefabs.");
    }

    public static void BuildAndValidate()
    {
        BuildAll();
        ValidateAll();
    }

    [MenuItem("Tools/Royalty Boat/Validate Obstacle Prefabs")]
    public static void ValidateAll()
    {
        GameObject iceberg = RequirePrefab("Iceberg");
        RequireComponent<ObstacleDescriptor>(iceberg);
        RequireComponentInChildren<MeshCollider>(iceberg);

        GameObject log = RequirePrefab("FloatingLog");
        RequireComponent<ObstacleDescriptor>(log);
        RequireComponent<Rigidbody>(log);
        RequireComponent<SimpleBuoyantBody>(log);

        GameObject debris = RequirePrefab("DebrisCluster");
        RequireComponent<ObstacleDescriptor>(debris);
        RequireComponent<Rigidbody>(debris);
        RequireComponent<SimpleBuoyantBody>(debris);

        GameObject acid = RequirePrefab("AcidicWater");
        RequireComponent<ObstacleDescriptor>(acid);
        RequireComponent<AcidWaterVolume>(acid);
        Collider acidCollider = RequireComponent<Collider>(acid);
        if (!acidCollider.isTrigger)
        {
            throw new System.InvalidOperationException("AcidicWater collider must remain a trigger.");
        }

        Debug.Log("Validated all obstacle prefab structures successfully.");
    }

    private static void BuildIceberg(Mesh mesh, Material ice, Material snow)
    {
        GameObject root = new GameObject("Iceberg");
        try
        {
            root.AddComponent<ObstacleDescriptor>().Configure("iceberg", ObstacleKind.Iceberg, 4f, new Vector2(10f, 10f));

            GameObject body = new GameObject("Faceted Ice");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = ice;
            MeshCollider collider = body.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            AddVisualPrimitive(root.transform, PrimitiveType.Cube, "Snow Shelf A", new Vector3(-0.8f, 3.15f, 0.2f), new Vector3(3.7f, 0.35f, 2.8f), Quaternion.Euler(2f, 18f, -7f), snow);
            AddVisualPrimitive(root.transform, PrimitiveType.Cube, "Snow Shelf B", new Vector3(1.15f, 2.45f, -0.25f), new Vector3(2.5f, 0.3f, 2.1f), Quaternion.Euler(-4f, -25f, 11f), snow);

            GameObject shadow = AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "Underwater Silhouette", new Vector3(0f, -1.65f, 0f), new Vector3(4.2f, 0.75f, 4.2f), Quaternion.identity, ice);
            shadow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Iceberg.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildFloatingLog(Material wood, Material darkWood)
    {
        GameObject root = new GameObject("Floating Log");
        try
        {
            root.AddComponent<ObstacleDescriptor>().Configure("floating-log", ObstacleKind.FloatingDebris, 1.5f, new Vector2(6f, 2f));
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 55f;
            body.linearDamping = 0.1f;
            body.angularDamping = 0.35f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<SimpleBuoyantBody>();

            GameObject trunk = AddPrimitive(root.transform, PrimitiveType.Cylinder, "Log", Vector3.zero, new Vector3(0.65f, 2.5f, 0.65f), Quaternion.Euler(0f, 0f, 90f), wood);
            CapsuleCollider trunkCollider = trunk.GetComponent<CapsuleCollider>();
            trunkCollider.material = CreatePhysicsMaterial("WetWood", 0.35f, 0.08f);

            AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "Cut End Left", new Vector3(-2.51f, 0f, 0f), new Vector3(0.66f, 0.035f, 0.66f), Quaternion.Euler(0f, 0f, 90f), darkWood);
            AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "Cut End Right", new Vector3(2.51f, 0f, 0f), new Vector3(0.66f, 0.035f, 0.66f), Quaternion.Euler(0f, 0f, 90f), darkWood);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/FloatingLog.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildDebrisCluster(Material wood, Material darkWood, Material metal)
    {
        GameObject root = new GameObject("Floating Debris Cluster");
        try
        {
            root.AddComponent<ObstacleDescriptor>().Configure("debris-cluster", ObstacleKind.FloatingDebris, 2f, new Vector2(6f, 5f));
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 75f;
            body.linearDamping = 0.2f;
            body.angularDamping = 0.5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<SimpleBuoyantBody>();

            AddPrimitive(root.transform, PrimitiveType.Cube, "Broken Plank A", new Vector3(-0.6f, 0f, 0.35f), new Vector3(4.6f, 0.25f, 0.65f), Quaternion.Euler(4f, 19f, -3f), wood);
            AddPrimitive(root.transform, PrimitiveType.Cube, "Broken Plank B", new Vector3(0.75f, 0.08f, -0.65f), new Vector3(3.8f, 0.22f, 0.55f), Quaternion.Euler(-5f, -31f, 2f), darkWood);
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "Barrel", new Vector3(1.25f, 0.15f, 0.7f), new Vector3(0.55f, 0.75f, 0.55f), Quaternion.Euler(0f, 0f, 82f), wood);
            AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "Barrel Band A", new Vector3(0.55f, 0.05f, 0.7f), new Vector3(0.58f, 0.07f, 0.58f), Quaternion.Euler(0f, 0f, 82f), metal);
            AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "Barrel Band B", new Vector3(1.95f, 0.25f, 0.7f), new Vector3(0.58f, 0.07f, 0.58f), Quaternion.Euler(0f, 0f, 82f), metal);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/DebrisCluster.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildAcidWater(Material acid, Material acidBubble)
    {
        GameObject root = new GameObject("Acidic Water");
        try
        {
            root.AddComponent<ObstacleDescriptor>().Configure("acidic-water", ObstacleKind.AcidicWater, 3f, new Vector2(18f, 18f));
            BoxCollider volume = root.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, -1.5f, 0f);
            volume.size = new Vector3(18f, 4f, 18f);
            root.AddComponent<AcidWaterVolume>();

            GameObject surface = AddVisualPrimitive(root.transform, PrimitiveType.Cube, "Acid Surface", new Vector3(0f, 0.08f, 0f), new Vector3(18f, 0.05f, 18f), Quaternion.identity, acid);
            MeshRenderer surfaceRenderer = surface.GetComponent<MeshRenderer>();
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = false;

            GameObject bubbles = new GameObject("Bubbles");
            bubbles.transform.SetParent(root.transform, false);
            bubbles.AddComponent<AcidWaterVisual>();
            Vector3[] bubblePositions =
            {
                new Vector3(-5.8f, 0.22f, -2.9f), new Vector3(-3.1f, 0.18f, 4.8f),
                new Vector3(-0.9f, 0.26f, 1.9f), new Vector3(2.1f, 0.2f, -4.2f),
                new Vector3(4.9f, 0.25f, 2.8f), new Vector3(6.2f, 0.18f, -1.1f),
                new Vector3(1.4f, 0.2f, 5.9f), new Vector3(-5.2f, 0.19f, 3.2f)
            };
            for (int i = 0; i < bubblePositions.Length; i++)
            {
                float size = 0.28f + (i % 3) * 0.13f;
                GameObject bubble = AddVisualPrimitive(bubbles.transform, PrimitiveType.Sphere, $"Bubble {i + 1}", bubblePositions[i], Vector3.one * size, Quaternion.identity, acidBubble);
                bubble.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/AcidicWater.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject AddPrimitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject child = GameObject.CreatePrimitive(type);
        child.name = name;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = position;
        child.transform.localRotation = rotation;
        child.transform.localScale = scale;
        child.GetComponent<MeshRenderer>().sharedMaterial = material;
        return child;
    }

    private static GameObject AddVisualPrimitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject child = AddPrimitive(parent, type, name, position, scale, rotation, material);
        Collider collider = child.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        return child;
    }

    private static Mesh CreateIcebergMesh()
    {
        string path = MeshFolder + "/IcebergLowPoly.asset";
        AssetDatabase.DeleteAsset(path);

        float[][] radii =
        {
            new[] { 3.6f, 4.2f, 3.8f, 4.5f, 3.7f, 4.1f, 3.5f, 4.3f },
            new[] { 4.8f, 4.3f, 5.1f, 4.4f, 4.9f, 4.2f, 5.0f, 4.5f },
            new[] { 3.1f, 3.7f, 2.9f, 3.5f, 3.0f, 3.4f, 2.8f, 3.6f },
            new[] { 1.5f, 2.0f, 1.2f, 1.8f, 1.4f, 1.9f, 1.1f, 1.7f }
        };
        float[] heights = { -2.8f, 0f, 2.8f, 4.7f };
        var rings = new Vector3[heights.Length][];
        for (int level = 0; level < heights.Length; level++)
        {
            rings[level] = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f + level * 0.09f;
                rings[level][i] = new Vector3(Mathf.Cos(angle) * radii[level][i], heights[level], Mathf.Sin(angle) * radii[level][i]);
            }
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int level = 0; level < rings.Length - 1; level++)
        {
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                AddTriangle(vertices, triangles, rings[level][i], rings[level + 1][i], rings[level][next]);
                AddTriangle(vertices, triangles, rings[level][next], rings[level + 1][i], rings[level + 1][next]);
            }
        }

        Vector3 apex = new Vector3(0.45f, 6.6f, -0.25f);
        Vector3 bottom = new Vector3(0f, -3.15f, 0f);
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            AddTriangle(vertices, triangles, rings[3][i], apex, rings[3][next]);
            AddTriangle(vertices, triangles, rings[0][next], bottom, rings[0][i]);
        }

        Mesh mesh = new Mesh { name = "Iceberg Low Poly" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindLitShader()) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateTransparentMaterial(string name, Color color, Color emission)
    {
        Material material = CreateLitMaterial(name, color, 0f, 0.4f, emission);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial CreatePhysicsMaterial(string name, float friction, float bounciness)
    {
        string path = MaterialFolder + "/" + name + ".physicMaterial";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null)
        {
            material = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(material, path);
        }
        material.dynamicFriction = friction;
        material.staticFriction = friction;
        material.bounciness = bounciness;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader FindLitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        return shader != null ? shader : Shader.Find("Standard");
    }

    private static GameObject RequirePrefab(string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
        if (prefab == null)
        {
            throw new System.InvalidOperationException($"Missing generated obstacle prefab: {name}");
        }
        return prefab;
    }

    private static T RequireComponent<T>(GameObject prefab) where T : Component
    {
        T component = prefab.GetComponent<T>();
        if (component == null)
        {
            throw new System.InvalidOperationException($"{prefab.name} is missing {typeof(T).Name}.");
        }
        return component;
    }

    private static T RequireComponentInChildren<T>(GameObject prefab) where T : Component
    {
        T component = prefab.GetComponentInChildren<T>(true);
        if (component == null)
        {
            throw new System.InvalidOperationException($"{prefab.name} is missing child {typeof(T).Name}.");
        }
        return component;
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(RootFolder);
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(MeshFolder);
        Directory.CreateDirectory(PrefabFolder);
    }
}
