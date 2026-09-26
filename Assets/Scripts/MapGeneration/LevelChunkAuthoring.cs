using UnityEngine;

namespace RoyaltyBoat.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class LevelChunkAuthoring : MonoBehaviour
    {
        [SerializeField] private string chunkId = "chunk";
        [SerializeField, Min(1f)] private float length = 80f;
        [SerializeField, Min(1f)] private float courseWidth = 54f;
        [SerializeField] private LaneMask entranceLanes = LaneMask.All;
        [SerializeField] private LaneMask exitLanes = LaneMask.All;
        [SerializeField] private Vector3[] safeRoute = { Vector3.zero, new Vector3(80f, 0f, 0f) };

        public string ChunkId => chunkId;
        public float Length => length;
        public float CourseWidth => courseWidth;
        public LaneMask EntranceLanes => entranceLanes;
        public LaneMask ExitLanes => exitLanes;
        public Vector3[] SafeRoute => safeRoute;

        public void Configure(
            string id,
            float chunkLength,
            float width,
            LaneMask entrances,
            LaneMask exits,
            Vector3[] route)
        {
            chunkId = string.IsNullOrWhiteSpace(id) ? name : id;
            length = Mathf.Max(1f, chunkLength);
            courseWidth = Mathf.Max(1f, width);
            entranceLanes = entrances == LaneMask.None ? LaneMask.All : entrances;
            exitLanes = exits == LaneMask.None ? LaneMask.All : exits;
            safeRoute = route == null || route.Length < 2
                ? new[] { Vector3.zero, new Vector3(length, 0f, 0f) }
                : route;
        }

        private void OnValidate()
        {
            length = Mathf.Max(1f, length);
            courseWidth = Mathf.Max(1f, courseWidth);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.6f);
            Vector3 center = new Vector3(length * 0.5f, 0f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(length, 0.2f, courseWidth));

            if (safeRoute == null || safeRoute.Length < 2)
            {
                return;
            }

            Gizmos.color = new Color(0.35f, 1f, 0.35f, 0.9f);
            for (int i = 1; i < safeRoute.Length; i++)
            {
                Gizmos.DrawLine(safeRoute[i - 1], safeRoute[i]);
                Gizmos.DrawSphere(safeRoute[i], 0.65f);
            }
        }
    }
}
