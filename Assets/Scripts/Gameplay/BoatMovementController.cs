using UnityEngine;
using UnityEngine.InputSystem;
using RoyaltyBoat.Obstacles;
using RoyaltyBoat.MapGeneration;


namespace RoyaltyBoat.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatMovementController : MonoBehaviour
    {
        [Header("Automatic Movement")]
        [SerializeField, Min(0f)] private float forwardSpeed = 14f;
        [SerializeField, Min(0f)] private float forwardAcceleration = 8f;

        [Header("Up/Down Steering")]
        [SerializeField, Min(0f)] private float lateralSpeed = 9f;
        [SerializeField, Min(0f)] private float lateralAcceleration = 18f;

        [Header("Iceberg Collision")]
        [SerializeField, Range(0f, 1.5f)] private float icebergBounceMultiplier = 0.65f;
        [SerializeField, Min(0f)] private float minimumIcebergBounceSpeed = 5f;
        [SerializeField, Min(0f)] private float icebergControlLockDuration = 0.4f;

        private Vector3 previousPlanarVelocity;
        private float resumeControlTime;


        private Rigidbody body;
        private RavineCourseBoundary course;

        public float ForwardSpeed => forwardSpeed;
        public float LateralSpeed => lateralSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            course = FindAnyObjectByType<RavineCourseBoundary>();
        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            if (Time.time < resumeControlTime)
            {
                previousPlanarVelocity = new Vector3(
                    body.linearVelocity.x,
                    0f,
                    body.linearVelocity.z);
                return;
            }

            if (course == null)
            {
                course = FindAnyObjectByType<RavineCourseBoundary>();
            }

            float steering = ReadSteering();
            Vector3 velocity = body.linearVelocity;
            Vector3 forward = Vector3.right;
            if (course != null)
            {
                float courseDistance = course.transform.InverseTransformPoint(body.position).x;
                forward = course.transform.TransformDirection(
                    course.GetCourseTangent(courseDistance));
                forward.y = 0f;
                forward.Normalize();
            }

            Vector3 across = new Vector3(-forward.z, 0f, forward.x);
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 desiredPlanarVelocity = forward * forwardSpeed
                + across * (steering * lateralSpeed);
            planarVelocity = Vector3.MoveTowards(
                planarVelocity,
                desiredPlanarVelocity,
                Mathf.Max(forwardAcceleration, lateralAcceleration)
                    * Time.fixedDeltaTime);
            velocity.x = planarVelocity.x;
            velocity.z = planarVelocity.z;

            body.linearVelocity = velocity;
            previousPlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }

        private void OnCollisionEnter(Collision collision)
        {
            ObstacleDescriptor obstacle =
                collision.collider.GetComponentInParent<ObstacleDescriptor>();
            if (obstacle == null || obstacle.Kind != ObstacleKind.Iceberg || body == null)
            {
                return;
            }

            Vector3 incoming = previousPlanarVelocity;
            if (incoming.sqrMagnitude < 0.01f)
            {
                incoming = new Vector3(
                    body.linearVelocity.x,
                    0f,
                    body.linearVelocity.z);
            }

            Vector3 normal = collision.contactCount > 0
                ? collision.GetContact(0).normal
                : -incoming.normalized;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.01f)
            {
                normal = -incoming.normalized;
            }
            else
            {
                normal.Normalize();
            }

            if (Vector3.Dot(incoming, normal) > 0f)
            {
                normal = -normal;
            }

            Vector3 reflected = Vector3.Reflect(incoming, normal);
            float bounceSpeed = Mathf.Max(
                minimumIcebergBounceSpeed,
                incoming.magnitude * icebergBounceMultiplier);
            if (reflected.sqrMagnitude < 0.01f)
            {
                reflected = -incoming;
            }

            reflected = reflected.normalized * bounceSpeed;
            body.linearVelocity = new Vector3(
                reflected.x,
                body.linearVelocity.y,
                reflected.z);
            previousPlanarVelocity = reflected;
            resumeControlTime = Time.time + icebergControlLockDuration;
        }


        private static float ReadSteering()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            return (keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.downArrowKey.isPressed ? 1f : 0f);
        }

        private void OnValidate()
        {
            forwardSpeed = Mathf.Max(0f, forwardSpeed);
            forwardAcceleration = Mathf.Max(0f, forwardAcceleration);
            lateralSpeed = Mathf.Max(0f, lateralSpeed);
            lateralAcceleration = Mathf.Max(0f, lateralAcceleration);
            icebergBounceMultiplier = Mathf.Clamp(icebergBounceMultiplier, 0f, 1.5f);
            minimumIcebergBounceSpeed = Mathf.Max(0f, minimumIcebergBounceSpeed);
            icebergControlLockDuration = Mathf.Max(0f, icebergControlLockDuration);
        }
    }
}
