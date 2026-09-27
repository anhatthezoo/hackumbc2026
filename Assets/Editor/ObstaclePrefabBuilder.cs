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
        Material ghostWood = CreateTransparentMaterial("GhostShip", new Color(0.2f, 0.85f, 0.78f, 0.62f), new Color(0.05f, 0.6f, 0.5f));
        Material cannonballMaterial = CreateLitMaterial("GhostCannonball", new Color(0.08f, 0.12f, 0.14f), 0.6f, 0.25f);

        Mesh icebergMesh = CreateIcebergMesh();
        GhostShipCannonball cannonball = BuildGhostShipCannonball(cannonballMaterial);
        BuildIceberg(icebergMesh, ice, snow);
        BuildFloatingLog(wood, darkWood);
        BuildDebrisCluster(wood, darkWood, metal);
        BuildBurningOilSlick();
        BuildGhostShip(ghostWood, cannonball);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built iceberg, floating log, debris cluster, burning oil slick, and ghost ship prefabs.");
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
        RequireComponent<BurningOilSlickVisual>(acid);
        Collider acidCollider = RequireComponent<Collider>(acid);
        if (!acidCollider.isTrigger)
        {
            throw new System.InvalidOperationException("AcidicWater collider must remain a trigger.");
        }

        GameObject cannonball = RequirePrefab("GhostShipCannonball");
        RequireComponent<GhostShipCannonball>(cannonball);
        Rigidbody cannonballBody = RequireComponent<Rigidbody>(cannonball);
        if (cannonballBody.useGravity)
        {
            throw new System.InvalidOperationException("GhostShipCannonball must not use gravity.");
        }

        GameObject ghostShip = RequirePrefab("GhostShip");
        RequireComponent<ObstacleDescriptor>(ghostShip);
        RequireComponent<GhostShipHazard>(ghostShip);
        Rigidbody ghostBody = RequireComponent<Rigidbody>(ghostShip);
        if (!ghostBody.isKinematic)
        {
            throw new System.InvalidOperationException("GhostShip must use a kinematic Rigidbody.");
        }

        Debug.Log("Validated all obstacle prefab structures successfully.");
    }

    private static GhostShipCannonball BuildGhostShipCannonball(Material material)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "Ghost Ship Cannonball";
        try
        {
            root.tag = "Obstacle";
            root.transform.localScale = Vector3.one * 0.65f;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GhostShipCannonball cannonball = root.AddComponent<GhostShipCannonball>();
            cannonball.Configure(25, 8f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/GhostShipCannonball.prefab");
            return prefab.GetComponent<GhostShipCannonball>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildGhostShip(Material material, GhostShipCannonball cannonballPrefab)
    {
        GameObject root = new GameObject("Ghost Ship");
        try
        {
            root.tag = "Obstacle";
            root.AddComponent<ObstacleDescriptor>().Configure("ghost-ship", ObstacleKind.GhostShip, 4.5f, new Vector2(12f, 7f));

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 500f;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.25f, 0f);
            collider.size = new Vector3(10f, 2.5f, 5f);

            GhostShipHazard hazard = root.AddComponent<GhostShipHazard>();
            hazard.Configure(cannonballPrefab, new Vector3(24f, 0f, 0f), 4f, 2.5f, 18f);

            AddVisualPrimitive(
                root.transform,
                PrimitiveType.Cube,
                "Primitive Ghost Ship",
                new Vector3(0f, 1.25f, 0f),
                new Vector3(10f, 2.5f, 5f),
                Quaternion.identity,
                material);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/GhostShip.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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

    private static void BuildBurningOilSlick()
    {
        GameObject root = new GameObject("Burning Oil Slick");
        try
        {
            Vector2 oilSize = new Vector2(36f, 60f);
            root.AddComponent<ObstacleDescriptor>().Configure("burning-oil-slick", ObstacleKind.AcidicWater, 3f, oilSize);
            BoxCollider volume = root.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, -1.5f, 0f);
            volume.size = new Vector3(oilSize.x, 4f, oilSize.y);
            root.AddComponent<AcidWaterVolume>();
            root.AddComponent<BurningOilSlickVisual>().Configure(oilSize, 16);

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
