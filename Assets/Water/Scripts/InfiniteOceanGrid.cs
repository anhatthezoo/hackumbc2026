using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class InfiniteOceanGrid : MonoBehaviour
{
    [Header("Tracking")]
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private Material _waterMaterial;

    [Header("Coverage")]
    [SerializeField, Range(5, 15)] private int _tilesPerAxis = 9;
    [SerializeField, Min(16f)] private float _tileSize = 96f;

    [Header("Mesh LODs")]
    [SerializeField] private int[] _lodSubdivisions = { 96, 48, 24, 12 };

    private sealed class OceanTile
    {
        public Transform transform;
        public Vector2Int offset;
    }

    private readonly List<OceanTile> _tiles = new List<OceanTile>();
    private readonly List<Mesh> _lodMeshes = new List<Mesh>();
    private MeshRenderer _templateRenderer;
    private Vector2Int _lastCenter = new Vector2Int(int.MinValue, int.MinValue);
    private bool _needsRebuild = true;

    public int TileCount => _tiles.Count;
    public float CoveredDiameter => _tilesPerAxis * _tileSize;

    private void Reset()
    {
        _targetCamera = Camera.main;
        _templateRenderer = GetComponent<MeshRenderer>();
        if (_templateRenderer != null)
        {
            _waterMaterial = _templateRenderer.sharedMaterial;
        }
        _needsRebuild = true;
    }

    private void OnEnable()
    {
        _needsRebuild = true;
        RebuildIfNeeded();
        UpdateGridPosition();
    }

    private void OnDisable()
    {
        ClearGeneratedObjects();
        SetTemplateRendererVisible(true);
    }

    private void OnDestroy()
    {
        ClearGeneratedObjects();
    }

    private void OnValidate()
    {
        _tilesPerAxis = Mathf.Clamp(_tilesPerAxis | 1, 5, 15);
        _tileSize = Mathf.Max(16f, _tileSize);
        if (_lodSubdivisions == null || _lodSubdivisions.Length == 0)
        {
            _lodSubdivisions = new[] { 96, 48, 24, 12 };
        }

        for (int index = 0; index < _lodSubdivisions.Length; ++index)
        {
            _lodSubdivisions[index] = Mathf.Clamp(_lodSubdivisions[index], 2, 256);
        }

        _needsRebuild = true;
    }

    private void Update()
    {
        RebuildIfNeeded();
        UpdateGridPosition();
    }

    private void RebuildIfNeeded()
    {
        if (!_needsRebuild)
        {
            return;
        }

        ClearGeneratedObjects();
        _templateRenderer = GetComponent<MeshRenderer>();
        if (_waterMaterial == null && _templateRenderer != null)
        {
            _waterMaterial = _templateRenderer.sharedMaterial;
        }

        if (_waterMaterial == null)
        {
            return;
        }

        foreach (int subdivisions in _lodSubdivisions)
        {
            _lodMeshes.Add(CreateGridMesh(subdivisions));
        }

        int half = _tilesPerAxis / 2;
        for (int z = -half; z <= half; ++z)
        {
            for (int x = -half; x <= half; ++x)
            {
                Vector2Int offset = new Vector2Int(x, z);
                int lod = GetLodLevel(offset, half);
                GameObject tileObject = new GameObject(
                    $"Ocean Tile {x:+0;-0;0},{z:+0;-0;0} LOD{lod}");
                tileObject.hideFlags = HideFlags.HideAndDontSave;
                tileObject.layer = gameObject.layer;
                tileObject.transform.SetParent(transform, false);

                var filter = tileObject.AddComponent<MeshFilter>();
                filter.sharedMesh = _lodMeshes[lod];

                var renderer = tileObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _waterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;

                _tiles.Add(new OceanTile
                {
                    transform = tileObject.transform,
                    offset = offset
                });
            }
        }

        SetTemplateRendererVisible(false);
        _lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        _needsRebuild = false;
    }

    private int GetLodLevel(Vector2Int offset, int halfWidth)
    {
        int ring = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
        if (ring <= 1)
        {
            return 0;
        }

        if (ring <= 2)
        {
            return Mathf.Min(1, _lodMeshes.Count - 1);
        }

        if (ring <= Mathf.Max(3, halfWidth - 1))
        {
            return Mathf.Min(2, _lodMeshes.Count - 1);
        }

        return _lodMeshes.Count - 1;
    }

    private void UpdateGridPosition()
    {
        Camera cameraToFollow = ResolveCamera();
        if (cameraToFollow == null || _tiles.Count == 0)
        {
            return;
        }

        Vector3 cameraPosition = cameraToFollow.transform.position;
        var center = new Vector2Int(
            Mathf.RoundToInt(cameraPosition.x / _tileSize),
            Mathf.RoundToInt(cameraPosition.z / _tileSize));
        if (center == _lastCenter)
        {
            return;
        }

        Vector3 parentPosition = transform.position;
        foreach (OceanTile tile in _tiles)
        {
            float worldX = (center.x + tile.offset.x) * _tileSize;
            float worldZ = (center.y + tile.offset.y) * _tileSize;
            tile.transform.localPosition = new Vector3(
                worldX - parentPosition.x,
                0f,
                worldZ - parentPosition.z);
        }

        _lastCenter = center;
    }

    private Camera ResolveCamera()
    {
        if (_targetCamera != null && _targetCamera.isActiveAndEnabled)
        {
            return _targetCamera;
        }

        if (Camera.main != null)
        {
            _targetCamera = Camera.main;
            return _targetCamera;
        }

        _targetCamera = FindAnyObjectByType<Camera>();
        return _targetCamera;
    }

    private Mesh CreateGridMesh(int subdivisions)
    {
        int verticesPerAxis = subdivisions + 1;
        var vertices = new Vector3[verticesPerAxis * verticesPerAxis];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[subdivisions * subdivisions * 6];

        for (int z = 0; z < verticesPerAxis; ++z)
        {
            float zPosition = ((float)z / subdivisions - 0.5f) * _tileSize;
            for (int x = 0; x < verticesPerAxis; ++x)
            {
                int vertexIndex = z * verticesPerAxis + x;
                float xPosition = ((float)x / subdivisions - 0.5f) * _tileSize;
                vertices[vertexIndex] = new Vector3(xPosition, 0f, zPosition);
                normals[vertexIndex] = Vector3.up;
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < subdivisions; ++z)
        {
            for (int x = 0; x < subdivisions; ++x)
            {
                int bottomLeft = z * verticesPerAxis + x;
                int topLeft = bottomLeft + verticesPerAxis;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topLeft + 1;
            }
        }

        var mesh = new Mesh
        {
            name = $"Ocean Grid LOD {subdivisions}",
            indexFormat = IndexFormat.UInt32,
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            normals = normals,
            triangles = triangles,
            bounds = new Bounds(Vector3.zero, new Vector3(_tileSize, 100f, _tileSize))
        };
        mesh.UploadMeshData(true);
        return mesh;
    }

    private void SetTemplateRendererVisible(bool visible)
    {
        if (_templateRenderer != null)
        {
            _templateRenderer.enabled = visible;
        }
    }

    private void ClearGeneratedObjects()
    {
        foreach (OceanTile tile in _tiles)
        {
            if (tile.transform != null)
            {
                DestroyGeneratedObject(tile.transform.gameObject);
            }
        }
        _tiles.Clear();

        foreach (Mesh mesh in _lodMeshes)
        {
            if (mesh != null)
            {
                DestroyGeneratedObject(mesh);
            }
        }
        _lodMeshes.Clear();
    }

    private static void DestroyGeneratedObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
