using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    /// <summary>
    /// Cheap buoyancy for loose hazards. This is intentionally independent from the visual
    /// ocean simulation; a future water service can update WaterHeight each physics tick.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleBuoyantBody : MonoBehaviour
    {
        [SerializeField] private float waterHeight;
        [SerializeField, Min(0.05f)] private float floatDepth = 0.75f;
        [SerializeField, Min(0f)] private float buoyancyMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float waterDrag = 1.5f;
        [SerializeField, Min(0f)] private float waterAngularDrag = 1.2f;
        [SerializeField] private Vector3[] localBuoyancyPoints =
        {
            new Vector3(-1.5f, -0.25f, 0f),
            new Vector3(1.5f, -0.25f, 0f)
        };

        private Rigidbody body;

        public float WaterHeight
        {
            get => waterHeight;
            set => waterHeight = value;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic || localBuoyancyPoints == null || localBuoyancyPoints.Length == 0)
            {
                return;
            }

            Vector3 gravity = Physics.gravity;
            float pointShare = 1f / localBuoyancyPoints.Length;

            foreach (Vector3 localPoint in localBuoyancyPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(localPoint);
                float submersion = Mathf.Clamp01((waterHeight - worldPoint.y) / floatDepth);
                if (submersion <= 0f)
                {
                    continue;
                }

                Vector3 pointVelocity = body.GetPointVelocity(worldPoint);
                Vector3 lift = -gravity * (buoyancyMultiplier * submersion * pointShare);
                Vector3 damping = -pointVelocity * (waterDrag * submersion * pointShare);
                body.AddForceAtPosition(lift + damping, worldPoint, ForceMode.Acceleration);
                body.AddTorque(-body.angularVelocity * (waterAngularDrag * submersion * pointShare), ForceMode.Acceleration);
            }
        }

        private void OnValidate()
        {
            floatDepth = Mathf.Max(0.05f, floatDepth);
            buoyancyMultiplier = Mathf.Max(0f, buoyancyMultiplier);
            waterDrag = Mathf.Max(0f, waterDrag);
            waterAngularDrag = Mathf.Max(0f, waterAngularDrag);
        }
    }
}
