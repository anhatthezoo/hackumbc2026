using UnityEngine;
using UnityEngine.InputSystem;

namespace RoyaltyBoat.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatMovementController : MonoBehaviour
    {
        [Header("Automatic Movement")]
        [SerializeField, Min(0f)] private float forwardSpeed = 10f;
        [SerializeField, Min(0f)] private float forwardAcceleration = 8f;

        [Header("Up/Down Steering")]
        [SerializeField, Min(0f)] private float lateralSpeed = 9f;
        [SerializeField, Min(0f)] private float lateralAcceleration = 18f;
        [SerializeField, Min(1f)] private float courseHalfWidth = 25f;

        private Rigidbody body;

        public float ForwardSpeed => forwardSpeed;
        public float LateralSpeed => lateralSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            float steering = ReadSteering();
            Vector3 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(
                velocity.x,
                forwardSpeed,
                forwardAcceleration * Time.fixedDeltaTime);
            velocity.z = Mathf.MoveTowards(
                velocity.z,
                steering * lateralSpeed,
                lateralAcceleration * Time.fixedDeltaTime);

            if (Mathf.Abs(body.position.z) >= courseHalfWidth &&
                Mathf.Sign(velocity.z) == Mathf.Sign(body.position.z))
            {
                velocity.z = 0f;
            }

            body.linearVelocity = velocity;
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
            courseHalfWidth = Mathf.Max(1f, courseHalfWidth);
        }
    }
}
