using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoyaltyBoat.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class RavineCourseBoundary : MonoBehaviour
    {
        private const string GeneratedName = "Generated Ravine";
        private const int WetMaterialIndex = 0;
        private const int RockMaterialIndex = 1;
        private const int DarkRockMaterialIndex = 2;
        private const int GroundMaterialIndex = 3;
        private const int SandMaterialIndex = 4;
        private const int GravelMaterialIndex = 5;

        [Header("Shape")]
        [SerializeField, Min(20f)] private float courseLength = 400f;
        [SerializeField, Min(10f)] private float courseWidth =
            MapGenerationDefaults.CourseWidth;
        [SerializeField, Min(2f)] private float segmentLength = 8f;
        [SerializeField, Min(0f)] private float endExtension = 48f;
        [SerializeField, Min(4f)] private float cliffHeight = 18f;
        [SerializeField, Min(24f)] private float outerBankDepth = 88f;
        [SerializeField] private int generationSeed = 12345;

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private Transform generatedRoot;

        public float CourseLength => courseLength;
        public float CourseWidth => courseWidth;

        public void Generate(float length, float width, int seed)
        {
            courseLength = Mathf.Max(20f, length);
            courseWidth = Mathf.Max(10f, width);
            generationSeed = seed;
            Rebuild();
        }

        [ContextMenu("Rebuild Ravine")]
        public void Rebuild()
        {
            ClearGenerated();

            GameObject rootObject = new GameObject(GeneratedName);
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);

            Material[] materials = CreateMaterials();
            CreateBank(generatedRoot, -1f, "Left Ravine Bank", materials);
            CreateBank(generatedRoot, 1f, "Right Ravine Bank", materials);
        }

        private void CreateBank(
            Transform parent,
            float side,
            string bankName,
            Material[] materials)
        {
            float startX = -endExtension;
            float totalLength = courseLength + endExtension * 2f;
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(totalLength / segmentLength));
            const int crossSectionCount = 9;
            var sections = new Vector3[segmentCount + 1, crossSectionCount];
            float halfWidth = courseWidth * 0.5f;

            for (int index = 0; index <= segmentCount; ++index)
            {
                float progress = index / (float)segmentCount;
                float x = startX + totalLength * progress;
                float centerDrift =
                    (FractalNoise(x, 0f, 21, 3, 0.0048f) - 0.5f) * 18f
                    + (FractalNoise(x, 0f, 22, 2, 0.018f) - 0.5f) * 5f;
                float detailNoise =
                    FractalNoise(x, side * 67f, 1, 3, 0.047f);
                float bankSquiggle =
                    (FractalNoise(x, side * 83f, 4, 3, 0.011f) - 0.5f) * 11f;
                float edgeDistance = halfWidth + 2f + bankSquiggle;
                float steepness = FractalNoise(
                    x,
                    side * 109f,
                    12,
                    3,
                    0.0042f);
                float shelfVariation = FractalNoise(
                    x,
                    side * 151f,
                    14,
                    3,
                    0.0068f);
                float profilePower = Mathf.Lerp(0.48f, 2.25f,
                    Mathf.Clamp01(steepness * 0.72f + shelfVariation * 0.45f));

                sections[index, 0] = new Vector3(
                    x,
                    -4.5f,
                    centerDrift + side * edgeDistance);
                sections[index, 1] = new Vector3(
                    x,
                    Mathf.Lerp(0.18f, 2.4f, steepness) + detailNoise * 0.45f,
                    centerDrift + side *
                        (edgeDistance
                            + Mathf.Lerp(0.35f, 2.8f, shelfVariation)
                            + detailNoise));

                for (int crossSection = 2;
                    crossSection < crossSectionCount;
                    ++crossSection)
                {
                    float crossProgress =
                        (crossSection - 1f) / (crossSectionCount - 2f);
                    float shapedProgress =
                        Mathf.Pow(crossProgress, profilePower);
                    float bankDistance =
                        Mathf.Lerp(4.5f, outerBankDepth, shapedProgress);
                    float lateralWarp =
                        (FractalNoise(x, bankDistance * side, 30, 3, 0.009f) - 0.5f) * 8f
                        + (FractalNoise(x, bankDistance * side, 31, 2, 0.027f) - 0.5f) * 2.5f;
                    float z = centerDrift + side *
                        (edgeDistance + bankDistance + lateralWarp);
                    float elevation =
                        GetMountainHeight(x, z, bankDistance, side);
                    sections[index, crossSection] =
                        new Vector3(x, elevation, z);
                }
            }

            var vertices = new List<Vector3>(segmentCount * 32);
            var uvs = new List<Vector2>(segmentCount * 32);
            var triangles = new List<int>[6];
            for (int materialIndex = 0; materialIndex < triangles.Length; ++materialIndex)
            {
                triangles[materialIndex] = new List<int>();
            }

            for (int segment = 0; segment < segmentCount; ++segment)
            {
                for (int band = 0; band < crossSectionCount - 1; ++band)
                {
                    AddFacetedQuad(
                        vertices,
                        uvs,
                        triangles,
                        sections[segment, band],
                        sections[segment + 1, band],
                        sections[segment + 1, band + 1],
                        sections[segment, band + 1],
                        side > 0f,
                        side,
                        segment,
                        band);
                }
            }

            Mesh mesh = new Mesh
            {
                name = bankName + " Mesh",
                indexFormat = vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int materialIndex = 0; materialIndex < triangles.Length; ++materialIndex)
            {
                mesh.SetTriangles(triangles[materialIndex], materialIndex);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);

            GameObject bank = new GameObject(bankName);
            bank.transform.SetParent(parent, false);
            MeshFilter filter = bank.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = bank.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            MeshCollider collider = bank.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            CreateOutcrops(parent, sections, side, bankName + " Outcrops", materials);
        }

        private int GetMaterialIndex(int band, Vector3 center, float side)
        {
            if (band == 0)
            {
                float shoreline = FractalNoise(
                    center.x,
                    side * 31f,
                    140,
                    3,
                    0.006f);
                if (shoreline < 0.38f)
                {
                    return SandMaterialIndex;
                }

                return shoreline > 0.68f
                    ? GravelMaterialIndex
                    : WetMaterialIndex;
            }

            float grassReach = FractalNoise(
                center.x,
                side * 47f,
                150,
                3,
                0.0045f);
            int lowestGrassBand = grassReach > 0.66f
                ? 1
                : grassReach > 0.52f ? 3 : 5;
            if (band >= lowestGrassBand)
            {
                return GroundMaterialIndex;
            }

            if (band >= 5)
            {
                return GroundMaterialIndex;
            }

            float stoneTone = SampleNoise(
                center.x,
                center.z,
                160 + band,
                0.025f);
            return stoneTone < 0.3f
                ? DarkRockMaterialIndex
                : RockMaterialIndex;
        }

        private void AddFacetedQuad(
            List<Vector3> vertices,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> triangles,
            Vector3 firstInner,
            Vector3 secondInner,
            Vector3 secondOuter,
            Vector3 firstOuter,
            bool reverseWinding,
            float side,
            int segment,
            int band)
        {
            Vector3 center =
                (firstInner + secondInner + secondOuter + firstOuter) * 0.25f;
            float facetOffset = Mathf.Sin(segment * 2.173f + band * 4.719f);
            center.y += facetOffset * (band >= 6 ? 0.7f : 1.15f);
            center.z += (reverseWinding ? 1f : -1f) * facetOffset * 0.45f;

            int primaryMaterial = GetMaterialIndex(band, center, side);
            int accentMaterial = band >= 1 && band <= 5
                ? (primaryMaterial == RockMaterialIndex
                    ? DarkRockMaterialIndex
                    : RockMaterialIndex)
                : primaryMaterial;
            bool useAccent = SampleNoise(
                center.x,
                center.z,
                180 + band,
                0.041f) > 0.72f;
            AddTriangle(vertices, uvs, triangles[primaryMaterial],
                firstInner, secondInner, center, reverseWinding);
            AddTriangle(vertices, uvs, triangles[useAccent ? accentMaterial : primaryMaterial],
                secondInner, secondOuter, center, reverseWinding);
            AddTriangle(vertices, uvs, triangles[primaryMaterial],
                secondOuter, firstOuter, center, reverseWinding);
            AddTriangle(vertices, uvs, triangles[primaryMaterial],
                firstOuter, firstInner, center, reverseWinding);
        }

        private static void AddTriangle(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            bool reverseWinding)
        {
            int firstVertex = vertices.Count;
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            uvs.Add(new Vector2(first.x * 0.04f, first.z * 0.04f));
            uvs.Add(new Vector2(second.x * 0.04f, second.z * 0.04f));
            uvs.Add(new Vector2(third.x * 0.04f, third.z * 0.04f));
            triangles.Add(firstVertex);
            triangles.Add(reverseWinding ? firstVertex + 2 : firstVertex + 1);
            triangles.Add(reverseWinding ? firstVertex + 1 : firstVertex + 2);
        }

        private void CreateOutcrops(
            Transform parent,
            Vector3[,] sections,
            float side,
            string outcropName,
            Material[] materials)
        {
            int sectionCount = sections.GetLength(0);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>[2]
            {
                new List<int>(),
                new List<int>()
            };
            var random = new System.Random(
                generationSeed ^ (side > 0f ? 0x4f1bbcdc : 0x2c9277b5));

            for (int section = 2; section < sectionCount - 2;)
            {
                if (random.NextDouble() < 0.48)
                {
                    section += random.Next(5, 10);
                    continue;
                }

                Vector3 lower = sections[section, 1];
                Vector3 ridge = sections[section, sections.GetLength(1) - 2];
                float slopePosition = 0.14f +
                    (float)random.NextDouble() * 0.78f;
                Vector3 center = Vector3.Lerp(lower, ridge, slopePosition);
                center.x += ((float)random.NextDouble() - 0.5f) * segmentLength * 1.3f;
                center.z -= side * (1.2f + (float)random.NextDouble() * 2.4f);
                float radiusX = 3f + (float)random.NextDouble() * 3.8f;
                float radiusZ = 2.4f + (float)random.NextDouble() * 2.8f;
                float height = 7f + (float)random.NextDouble() * 9f;
                AddRockSpire(
                    vertices,
                    uvs,
                    triangles,
                    center,
                    radiusX,
                    radiusZ,
                    height,
                    random);
                section += random.Next(5, 10);
            }

            if (vertices.Count == 0)
            {
                return;
            }

            Mesh mesh = new Mesh
            {
                name = outcropName + " Mesh",
                indexFormat = vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(triangles[0], 0);
            mesh.SetTriangles(triangles[1], 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);

            GameObject outcrops = new GameObject(outcropName);
            outcrops.transform.SetParent(parent, false);
            MeshFilter filter = outcrops.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = outcrops.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                materials[RockMaterialIndex],
                materials[DarkRockMaterialIndex]
            };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            MeshCollider collider = outcrops.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private static void AddRockSpire(
            List<Vector3> vertices,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> triangles,
            Vector3 center,
            float radiusX,
            float radiusZ,
            float height,
            System.Random random)
        {
            const int sides = 6;
            var lowerRing = new Vector3[sides];
            var upperRing = new Vector3[sides];
            for (int sideIndex = 0; sideIndex < sides; ++sideIndex)
            {
                float angle = sideIndex / (float)sides * Mathf.PI * 2f;
                float irregularity = 0.78f + (float)random.NextDouble() * 0.42f;
                lowerRing[sideIndex] = center + new Vector3(
                    Mathf.Cos(angle) * radiusX * irregularity,
                    -2.2f,
                    Mathf.Sin(angle) * radiusZ * irregularity);
                upperRing[sideIndex] = center + new Vector3(
                    Mathf.Cos(angle + 0.16f) * radiusX * 0.62f * irregularity,
                    height * 0.58f,
                    Mathf.Sin(angle + 0.16f) * radiusZ * 0.62f * irregularity);
            }

            Vector3 apex = center + new Vector3(
                ((float)random.NextDouble() - 0.5f) * radiusX * 0.55f,
                height,
                ((float)random.NextDouble() - 0.5f) * radiusZ * 0.55f);
            for (int sideIndex = 0; sideIndex < sides; ++sideIndex)
            {
                int next = (sideIndex + 1) % sides;
                AddTriangle(vertices, uvs, triangles[sideIndex % 2],
                    lowerRing[sideIndex], lowerRing[next], upperRing[sideIndex], false);
                AddTriangle(vertices, uvs, triangles[(sideIndex + 1) % 2],
                    lowerRing[next], upperRing[next], upperRing[sideIndex], false);
                AddTriangle(vertices, uvs, triangles[sideIndex % 2],
                    upperRing[sideIndex], upperRing[next], apex, false);
            }
        }

        private Material[] CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material wetRock = CreateMaterial(
                shader,
                "Ravine Wet Rock",
                new Color(0.12f, 0.17f, 0.18f));
            Material rock = CreateMaterial(
                shader,
                "Ravine Stone",
                new Color(0.31f, 0.30f, 0.27f));
            Material darkRock = CreateMaterial(
                shader,
                "Ravine Shadow Stone",
                new Color(0.22f, 0.23f, 0.22f));
            Material ground = CreateMaterial(
                shader,
                "Ravine High Ground",
                new Color(0.31f, 0.34f, 0.23f));
            Material sand = CreateMaterial(
                shader,
                "Ravine Sand",
                new Color(0.52f, 0.43f, 0.29f));
            Material gravel = CreateMaterial(
                shader,
                "Ravine Gravel",
                new Color(0.40f, 0.38f, 0.33f));
            return new[] { wetRock, rock, darkRock, ground, sand, gravel };
        }

        private Material CreateMaterial(Shader shader, string materialName, Color color)
        {
            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            generatedMaterials.Add(material);
            return material;
        }

        private float GetMountainHeight(
            float x,
            float z,
            float bankDistance,
            float side)
        {
            float progress = Mathf.InverseLerp(4.5f, outerBankDepth, bankDistance);
            float steepness = FractalNoise(
                x,
                side * 109f,
                12,
                3,
                0.0042f);
            float heightRegion = FractalNoise(
                x,
                side * 137f,
                13,
                3,
                0.0032f);
            float mountainStart = Mathf.Lerp(0.48f, 0.08f, steepness);
            float mountainEnvelope = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(mountainStart, mountainStart + 0.28f, progress));

            float warpX = (FractalNoise(x, z, 70, 2, 0.0065f) - 0.5f) * 42f;
            float warpZ = (FractalNoise(x, z, 71, 2, 0.008f) - 0.5f) * 34f;
            float warpedX = x + warpX;
            float warpedZ = z + warpZ;

            float broadShape = FractalNoise(
                warpedX,
                warpedZ,
                side > 0f ? 80 : 81,
                4,
                0.009f);
            float ridgeShape = RidgedMultifractal(
                warpedX,
                warpedZ,
                side > 0f ? 90 : 91,
                5,
                0.0125f);
            float secondaryRange = RidgedMultifractal(
                warpedX + 137f,
                warpedZ - 83f,
                side > 0f ? 100 : 101,
                3,
                0.022f);

            float regionalScale = Mathf.Lerp(0.52f, 1.48f, heightRegion);
            float foothillHeight = Mathf.Lerp(
                Mathf.Lerp(3.8f, 8f, steepness),
                cliffHeight * Mathf.Lerp(0.55f, 1.05f, heightRegion),
                Mathf.SmoothStep(0f, 1f, progress));
            float rangeHeight = ridgeShape * Mathf.Lerp(12f, 42f, progress)
                * regionalScale;
            float brokenPeaks = secondaryRange * 13f * regionalScale *
                Mathf.SmoothStep(0f, 1f, progress);
            return foothillHeight
                + broadShape * Mathf.Lerp(5f, 13f, heightRegion)
                + mountainEnvelope * (rangeHeight + brokenPeaks);
        }

        private float FractalNoise(
            float x,
            float z,
            int channel,
            int octaves,
            float frequency)
        {
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float value = 0f;
            for (int octave = 0; octave < octaves; ++octave)
            {
                value += SampleNoise(x, z, channel + octave * 13, frequency)
                    * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }

            return value / Mathf.Max(0.0001f, amplitudeSum);
        }

        private float RidgedMultifractal(
            float x,
            float z,
            int channel,
            int octaves,
            float frequency)
        {
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float value = 0f;
            float weight = 1f;
            for (int octave = 0; octave < octaves; ++octave)
            {
                float signal = 1f - Mathf.Abs(
                    SampleNoise(x, z, channel + octave * 17, frequency) * 2f - 1f);
                signal *= signal;
                signal *= weight;
                weight = Mathf.Clamp01(signal * 2.2f);
                value += signal * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.52f;
                frequency *= 2.07f;
            }

            return value / Mathf.Max(0.0001f, amplitudeSum);
        }

        private float SampleNoise(
            float x,
            float z,
            int channel,
            float frequency)
        {
            float seedX = generationSeed * 0.173f + channel * 97.1f;
            float seedZ = generationSeed * 0.071f + channel * 53.7f;
            return Mathf.PerlinNoise(
                (x + seedX) * frequency,
                (z + seedZ) * frequency);
        }

        private void ClearGenerated()
        {
            for (int childIndex = transform.childCount - 1;
                childIndex >= 0;
                --childIndex)
            {
                Transform child = transform.GetChild(childIndex);
                if (child.name != GeneratedName)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            generatedRoot = null;

            foreach (Mesh mesh in generatedMeshes)
            {
                DestroyRuntimeObject(mesh);
            }

            foreach (Material material in generatedMaterials)
            {
                DestroyRuntimeObject(material);
            }

            generatedMeshes.Clear();
            generatedMaterials.Clear();
        }

        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeObject);
            }
            else
            {
                DestroyImmediate(runtimeObject);
            }
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }

        private void OnValidate()
        {
            courseLength = Mathf.Max(20f, courseLength);
            courseWidth = Mathf.Max(10f, courseWidth);
            segmentLength = Mathf.Max(2f, segmentLength);
            endExtension = Mathf.Max(0f, endExtension);
            cliffHeight = Mathf.Max(4f, cliffHeight);
            outerBankDepth = Mathf.Max(24f, outerBankDepth);
        }
    }
}
