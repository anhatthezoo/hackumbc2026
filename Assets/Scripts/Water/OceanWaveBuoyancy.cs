using System;
using System.Collections.Generic;
using RoyaltyBoat.Audio;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoyaltyBoat.Water
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class OceanWaveBuoyancy : MonoBehaviour
    {
        private struct HullSample
        {
            public Vector3 LocalCenter;
            public Vector3 LocalExtents;
            public float VolumeShare;
        }

        [Header("Flotation")]
        [Tooltip("Fraction of a one-block-tall platform that rests below calm water.")]
        [SerializeField, Range(0.08f, 0.5f)] private float targetSubmergedFraction = 0.18f;
        [Tooltip("Vertical water resistance. This removes bounce without preventing the hull from following waves.")]
        [SerializeField, Min(0f)] private float verticalWaterDamping = 7f;
        [SerializeField, Min(0f)] private float waterAngularDrag = 1.2f;
        [Tooltip("Safety limit for recovery acceleration at an individual hull sample.")]
        [SerializeField, Min(10f)] private float maximumPointAcceleration = 45f;
        [SerializeField, Min(0.1f)] private float maximumUpwardSpeed = 4f;
        [SerializeField, Min(0.1f)] private float maximumDownwardSpeed = 18f;
        [Tooltip("Limits upward momentum when the hull leaves the water, preventing a recovered ship from flying.")]
        [SerializeField, Min(0f)] private float maximumSurfaceExitSpeed = 1.25f;

        [Header("Wave Sampling")]
        [SerializeField, Range(0.01f, 1f)] private float sampleResponse = 0.7f;
        [SerializeField, Range(0f, 2f)] private float physicsWaveHeightMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float maximumSampleStep = 0.45f;
        [SerializeField, Min(0.1f)] private float maximumWaveDisplacement = 2f;

        private HullSample[] hullSamples = Array.Empty<HullSample>();
        private Vector2[] samplePositions = Array.Empty<Vector2>();
        private float[] sampledHeights = Array.Empty<float>();

        private Rigidbody body;
        private Ship ship;
        private OceanWaveGenerator ocean;
        private ComputeShader samplingCompute;
        private ComputeBuffer samplePositionBuffer;
        private ComputeBuffer sampleResultBuffer;
        private int sampleKernel;
        private bool requestPending;
        private bool hasOceanSamples;
        private int requestVersion;
        private int cachedBlockCount = -1;
        private bool wasAirborne;
        private float nextSplashTime;

        public bool HasOceanSamples => hasOceanSamples;
        public int BuoyancyPointCount => hullSamples.Length;

        public float AverageSampledHeight
        {
            get
            {
                if (sampledHeights.Length == 0)
                {
                    return 0f;
                }

                float total = 0f;
                foreach (float sampledHeight in sampledHeights)
                {
                    total += sampledHeight;
                }

                return total / sampledHeights.Length;
            }
        }

        public float AverageSurfaceHeight
        {
            get
            {
                float oceanBaseHeight = ocean != null
                    ? ocean.transform.position.y
                    : 0f;
                if (!hasOceanSamples)
                {
                    return oceanBaseHeight;
                }

                float waveOffset = Mathf.Clamp(
                    AverageSampledHeight * physicsWaveHeightMultiplier,
                    -maximumWaveDisplacement,
                    maximumWaveDisplacement);
                return oceanBaseHeight + waveOffset;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ship = GetComponent<Ship>();
            ocean = FindAnyObjectByType<OceanWaveGenerator>();
            samplingCompute = Resources.Load<ComputeShader>("Water/OceanPhysicsSampling");

            if (samplingCompute == null)
            {
                Debug.LogError("Missing Resources/Water/OceanPhysicsSampling.compute.", this);
            }
            else
            {
                sampleKernel = samplingCompute.FindKernel("SampleOcean");
            }

            BuildHullSamples();
        }

        private void OnDestroy()
        {
            ReleaseSampleBuffers();
        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            if (ocean == null)
            {
                ocean = FindAnyObjectByType<OceanWaveGenerator>();
            }

            int currentBlockCount = ship != null
                ? ship.BlockCount
                : GetComponentsInChildren<Block>(true).Length;
            if (currentBlockCount != cachedBlockCount && !requestPending)
            {
                BuildHullSamples();
            }

            ApplyBuoyancy();
            LimitVerticalSpeed();
            RequestOceanSamples();
        }

        private void ApplyBuoyancy()
        {
            if (hullSamples.Length == 0)
            {
                return;
            }

            Vector3 gravity = Physics.gravity;
            float gravityMagnitude = gravity.magnitude;
            Vector3 gravityDirection = gravityMagnitude > 0.001f
                ? gravity / gravityMagnitude
                : Vector3.down;
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            float totalSubmergedShare = 0f;
            float downwardEntrySpeed = Mathf.Max(0f, -body.linearVelocity.y);

            for (int index = 0; index < hullSamples.Length; ++index)
            {
                HullSample sample = hullSamples[index];
                Vector3 worldCenter = localToWorld.MultiplyPoint3x4(sample.LocalCenter);
                float verticalHalfExtent = GetWorldVerticalHalfExtent(
                    localToWorld,
                    sample.LocalExtents);
                if (verticalHalfExtent <= 0.001f)
                {
                    continue;
                }

                float surfaceHeight = GetSurfaceHeight(index);
                float bottomHeight = worldCenter.y - verticalHalfExtent;
                float topHeight = worldCenter.y + verticalHalfExtent;
                float submergedFraction = Mathf.InverseLerp(
                    bottomHeight,
                    topHeight,
                    surfaceHeight);
                if (submergedFraction <= 0f)
                {
                    continue;
                }

                float supportedMass = body.mass * sample.VolumeShare;
                float liftScale = submergedFraction / targetSubmergedFraction;
                Vector3 liftForce = -gravity * (supportedMass * liftScale);

                float submergedTop = Mathf.Min(surfaceHeight, topHeight);
                Vector3 forcePoint = new Vector3(
                    worldCenter.x,
                    (bottomHeight + submergedTop) * 0.5f,
                    worldCenter.z);
                Vector3 pointVelocity = body.GetPointVelocity(forcePoint);
                if (!IsFinite(pointVelocity))
                {
                    continue;
                }

                Vector3 verticalVelocity = Vector3.Project(
                    pointVelocity,
                    gravityDirection);
                float dampingScale = Mathf.Sqrt(submergedFraction);
                Vector3 dampingForce = -verticalVelocity
                    * (supportedMass * verticalWaterDamping * dampingScale);
                Vector3 force = Vector3.ClampMagnitude(
                    liftForce + dampingForce,
                    supportedMass * maximumPointAcceleration);
                body.AddForceAtPosition(force, forcePoint, ForceMode.Force);
                totalSubmergedShare += sample.VolumeShare * submergedFraction;
            }

            if (totalSubmergedShare > 0f)
            {
                body.AddTorque(
                    -body.angularVelocity
                        * waterAngularDrag
                        * Mathf.Sqrt(totalSubmergedShare),
                    ForceMode.Acceleration);
            }

            if (totalSubmergedShare < targetSubmergedFraction * 0.2f)
            {
                LimitSurfaceExitSpeed(gravityDirection);
            }

            bool aboveWater = totalSubmergedShare < 0.01f &&
                              body.worldCenterOfMass.y > AverageSurfaceHeight + 0.35f;
            if (aboveWater)
            {
                wasAirborne = true;
            }
            else if (wasAirborne && totalSubmergedShare > 0.06f)
            {
                if (downwardEntrySpeed > 0.8f && Time.time >= nextSplashTime)
                {
                    GameAudio.PlaySplash(body.worldCenterOfMass);
                    nextSplashTime = Time.time + 0.8f;
                }

                wasAirborne = false;
            }
        }

        private float GetSurfaceHeight(int sampleIndex)
        {
            float oceanBaseHeight = ocean != null
                ? ocean.transform.position.y
                : 0f;
            float waveOffset = hasOceanSamples &&
                sampleIndex < sampledHeights.Length &&
                IsFinite(sampledHeights[sampleIndex])
                    ? Mathf.Clamp(
                        sampledHeights[sampleIndex] * physicsWaveHeightMultiplier,
                        -maximumWaveDisplacement,
                        maximumWaveDisplacement)
                    : 0f;
            return oceanBaseHeight + waveOffset;
        }

        private void LimitSurfaceExitSpeed(Vector3 gravityDirection)
        {
            Vector3 velocity = body.linearVelocity;
            float upwardSpeed = Vector3.Dot(velocity, -gravityDirection);
            if (upwardSpeed <= maximumSurfaceExitSpeed)
            {
                return;
            }

            velocity += gravityDirection * (upwardSpeed - maximumSurfaceExitSpeed);
            body.linearVelocity = velocity;
        }

        private void LimitVerticalSpeed()
        {
            Vector3 velocity = body.linearVelocity;
            if (!IsFinite(velocity))
            {
                body.linearVelocity = Vector3.zero;
                return;
            }

            velocity.y = Mathf.Clamp(
                velocity.y,
                -maximumDownwardSpeed,
                maximumUpwardSpeed);
            body.linearVelocity = velocity;
        }

        private void RequestOceanSamples()
        {
            if (requestPending || ocean == null || samplingCompute == null ||
                samplePositionBuffer == null || sampleResultBuffer == null ||
                hullSamples.Length == 0 ||
                !SystemInfo.supportsAsyncGPUReadback)
            {
                return;
            }

            for (int index = 0; index < hullSamples.Length; ++index)
            {
                Vector3 worldPoint = transform.TransformPoint(
                    hullSamples[index].LocalCenter);
                samplePositions[index] = new Vector2(worldPoint.x, worldPoint.z);
            }

            if (!ocean.BindPhysicsSamplingResources(samplingCompute, sampleKernel))
            {
                return;
            }

            samplePositionBuffer.SetData(samplePositions);
            samplingCompute.SetInt("_SampleCount", hullSamples.Length);
            samplingCompute.SetBuffer(
                sampleKernel,
                "_SamplePositions",
                samplePositionBuffer);
            samplingCompute.SetBuffer(
                sampleKernel,
                "_SampleResults",
                sampleResultBuffer);
            samplingCompute.Dispatch(
                sampleKernel,
                Mathf.CeilToInt(hullSamples.Length / 8f),
                1,
                1);

            requestPending = true;
            int version = requestVersion;
            AsyncGPUReadback.Request(sampleResultBuffer, request =>
            {
                requestPending = false;
                if (version != requestVersion || request.hasError || this == null)
                {
                    return;
                }

                var samples = request.GetData<Vector4>();
                bool hadSamples = hasOceanSamples;
                int resultCount = Mathf.Min(samples.Length, sampledHeights.Length);

                for (int index = 0; index < resultCount; ++index)
                {
                    float sampledHeight = samples[index].y;
                    if (!IsFinite(sampledHeight))
                    {
                        continue;
                    }

                    sampledHeight = Mathf.Clamp(
                        sampledHeight,
                        -maximumWaveDisplacement,
                        maximumWaveDisplacement);
                    if (!hadSamples)
                    {
                        sampledHeights[index] = sampledHeight;
                        continue;
                    }

                    float limitedHeight = Mathf.Clamp(
                        sampledHeight,
                        sampledHeights[index] - maximumSampleStep,
                        sampledHeights[index] + maximumSampleStep);
                    sampledHeights[index] = Mathf.Lerp(
                        sampledHeights[index],
                        limitedHeight,
                        sampleResponse);
                }

                hasOceanSamples = true;
            });
        }

        private void BuildHullSamples()
        {
            Block[] blocks = GetComponentsInChildren<Block>(true);
            cachedBlockCount = ship != null ? ship.BlockCount : blocks.Length;
            var samples = new List<HullSample>(Mathf.Max(1, blocks.Length));
            float totalVolume = 0f;

            foreach (Block block in blocks)
            {
                if (block == null || !block.IsAlive || !block.ProvidesBuoyancy ||
                    !block.gameObject.activeInHierarchy ||
                    !TryGetLocalBlockBounds(block, out Bounds localBounds))
                {
                    continue;
                }

                Vector3 size = localBounds.size;
                float volume = Mathf.Max(0.001f, size.x * size.y * size.z);
                samples.Add(new HullSample
                {
                    LocalCenter = localBounds.center,
                    LocalExtents = localBounds.extents,
                    VolumeShare = volume
                });
                totalVolume += volume;
            }

            if (samples.Count == 0)
            {
                samples.Add(new HullSample
                {
                    LocalCenter = Vector3.zero,
                    LocalExtents = Vector3.one * 0.5f,
                    VolumeShare = 1f
                });
                totalVolume = 1f;
            }

            for (int index = 0; index < samples.Count; ++index)
            {
                HullSample sample = samples[index];
                sample.VolumeShare /= totalVolume;
                samples[index] = sample;
            }

            hullSamples = samples.ToArray();
            samplePositions = new Vector2[hullSamples.Length];
            sampledHeights = new float[hullSamples.Length];
            hasOceanSamples = false;
            RecreateSampleBuffers();
        }

        private bool TryGetLocalBlockBounds(Block block, out Bounds localBounds)
        {
            localBounds = default;
            bool hasBounds = false;

            foreach (Collider blockCollider in
                block.GetComponentsInChildren<Collider>(true))
            {
                if (blockCollider == null || blockCollider.isTrigger ||
                    !blockCollider.enabled)
                {
                    continue;
                }

                if (blockCollider is BoxCollider boxCollider)
                {
                    Vector3 colliderExtents = boxCollider.size * 0.5f;
                    for (int corner = 0; corner < 8; ++corner)
                    {
                        Vector3 colliderCorner = boxCollider.center + new Vector3(
                            (corner & 1) == 0
                                ? -colliderExtents.x
                                : colliderExtents.x,
                            (corner & 2) == 0
                                ? -colliderExtents.y
                                : colliderExtents.y,
                            (corner & 4) == 0
                                ? -colliderExtents.z
                                : colliderExtents.z);
                        EncapsulateLocalPoint(
                            blockCollider.transform.TransformPoint(colliderCorner),
                            ref localBounds,
                            ref hasBounds);
                    }

                    continue;
                }

                // Block pieces currently use box colliders. This fallback keeps
                // future collider types buoyant, with a conservative AABB volume.
                Bounds worldBounds = blockCollider.bounds;
                Vector3 minimum = worldBounds.min;
                Vector3 maximum = worldBounds.max;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 worldCorner = new Vector3(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z);
                    EncapsulateLocalPoint(
                        worldCorner,
                        ref localBounds,
                        ref hasBounds);
                }
            }

            return hasBounds;
        }

        private void EncapsulateLocalPoint(
            Vector3 worldPoint,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            if (!hasBounds)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(localPoint);
            }
        }

        private void RecreateSampleBuffers()
        {
            ReleaseSampleBuffers();
            if (samplingCompute == null || hullSamples.Length == 0)
            {
                return;
            }

            samplePositionBuffer = new ComputeBuffer(
                hullSamples.Length,
                sizeof(float) * 2);
            sampleResultBuffer = new ComputeBuffer(
                hullSamples.Length,
                sizeof(float) * 4);
        }

        private void ReleaseSampleBuffers()
        {
            requestVersion++;
            requestPending = false;
            samplePositionBuffer?.Release();
            sampleResultBuffer?.Release();
            samplePositionBuffer = null;
            sampleResultBuffer = null;
        }

        private static float GetWorldVerticalHalfExtent(
            Matrix4x4 localToWorld,
            Vector3 localExtents)
        {
            return Mathf.Abs(localToWorld.m10) * localExtents.x
                + Mathf.Abs(localToWorld.m11) * localExtents.y
                + Mathf.Abs(localToWorld.m12) * localExtents.z;
        }

        private void OnValidate()
        {
            targetSubmergedFraction = Mathf.Clamp(
                targetSubmergedFraction,
                0.08f,
                0.5f);
            verticalWaterDamping = Mathf.Max(0f, verticalWaterDamping);
            waterAngularDrag = Mathf.Max(0f, waterAngularDrag);
            maximumPointAcceleration = Mathf.Max(10f, maximumPointAcceleration);
            maximumUpwardSpeed = Mathf.Max(0.1f, maximumUpwardSpeed);
            maximumDownwardSpeed = Mathf.Max(0.1f, maximumDownwardSpeed);
            maximumSurfaceExitSpeed = Mathf.Max(0f, maximumSurfaceExitSpeed);
            maximumSampleStep = Mathf.Max(0.01f, maximumSampleStep);
            maximumWaveDisplacement = Mathf.Max(0.1f, maximumWaveDisplacement);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
