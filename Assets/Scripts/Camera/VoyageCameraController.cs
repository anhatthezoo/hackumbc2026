using RoyaltyBoat.Settings;
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

        [Header("Visual Shake")]
        [SerializeField, Min(0f)] private float maximumShakeOffset = 0.18f;
        [SerializeField, Min(0f)] private float maximumShakeAngle = 0.75f;
        [SerializeField, Min(0.01f)] private float shakeFrequency = 5.5f;

        private Transform target;
        private Rigidbody targetBody;
        private Vector3 positionVelocity;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float impulseStrength;
        private float impulseDuration;
        private float impulseEndTime;

        public Transform Target => target;

        public void TriggerShake(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f)
            {
                return;
            }

            impulseStrength = Mathf.Max(impulseStrength, Mathf.Clamp01(strength));
            impulseDuration = Mathf.Max(0.01f, duration);
            impulseEndTime = Time.unscaledTime + impulseDuration;
        }

        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;
            targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
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
                basePosition = desiredPosition;
            }
            else
            {
                basePosition = Vector3.SmoothDamp(
                    basePosition,
                    desiredPosition,
                    ref positionVelocity,
                    positionSmoothTime);
            }

            Vector3 focusPoint = target.position
                + Vector3.right * lookAheadDistance
                + Vector3.up * focusHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(
                focusPoint - basePosition,
                Vector3.up);

            baseRotation = snapImmediately
                ? desiredRotation
                : Quaternion.Slerp(
                    baseRotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

            ApplyVisualShake();
        }

        private void ApplyVisualShake()
        {
            float setting = VisualSettings.CameraShakeIntensity;
            if (setting <= 0f || targetBody == null)
            {
                transform.SetPositionAndRotation(basePosition, baseRotation);
                return;
            }

            float boatMotion = Mathf.Clamp01(
                Mathf.Abs(targetBody.linearVelocity.y) * 0.08f +
                targetBody.angularVelocity.magnitude * 0.16f);
            float impulse = impulseEndTime > Time.unscaledTime
                ? impulseStrength * Mathf.Clamp01(
                    (impulseEndTime - Time.unscaledTime) / impulseDuration)
                : 0f;
            float strength = setting * Mathf.Clamp01(boatMotion + impulse);
            float sampleTime = Time.unscaledTime * shakeFrequency;
            float horizontalNoise = SignedNoise(sampleTime, 19.7f);
            float verticalNoise = SignedNoise(sampleTime, 47.3f);
            float rollNoise = SignedNoise(sampleTime, 83.1f);

            Vector3 localOffset = new Vector3(
                horizontalNoise,
                verticalNoise,
                0f) * (maximumShakeOffset * strength);
            Vector3 worldOffset = baseRotation * localOffset;
            Quaternion shakeRotation = Quaternion.Euler(
                verticalNoise * maximumShakeAngle * strength,
                horizontalNoise * maximumShakeAngle * strength,
                rollNoise * maximumShakeAngle * strength);
            transform.SetPositionAndRotation(
                basePosition + worldOffset,
                baseRotation * shakeRotation);
        }

        private static float SignedNoise(float time, float seed)
        {
            return Mathf.PerlinNoise(time, seed) * 2f - 1f;
        }
    }
}
