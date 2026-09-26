using UnityEngine;

namespace RoyaltyBoat.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class VoyageCameraController : MonoBehaviour
    {
        [Header("Framing")]
        [SerializeField] private Vector3 followOffset = new Vector3(-18f, 20f, -20f);
        [SerializeField, Min(0f)] private float lookAheadDistance = 12f;
        [SerializeField, Min(0f)] private float focusHeight = 1f;

        [Header("Smoothing")]
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.3f;
        [SerializeField, Min(0f)] private float rotationSharpness = 10f;

        private Transform target;
        private Vector3 positionVelocity;

        public Transform Target => target;

        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;
            positionVelocity = Vector3.zero;

            if (target != null && snapImmediately)
            {
                ApplyCameraPose(true);
            }
        }

        private void LateUpdate()
        {
            ApplyCameraPose(false);
        }

        private void ApplyCameraPose(bool snapImmediately)
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + followOffset;
            if (snapImmediately)
            {
                transform.position = desiredPosition;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref positionVelocity,
                    positionSmoothTime);
            }

            Vector3 focusPoint = target.position
                + Vector3.right * lookAheadDistance
                + Vector3.up * focusHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(
                focusPoint - transform.position,
                Vector3.up);

            transform.rotation = snapImmediately
                ? desiredRotation
                : Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }
    }
}
