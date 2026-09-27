using UnityEngine;
using UnityEngine.Rendering;

namespace RoyaltyBoat.Water
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class OceanWaveBuoyancy : MonoBehaviour
    {
        private const int CornerProbeCount = 4;
        private const int CenterProbeIndex = CornerProbeCount;
        private const int SampleCount = CornerProbeCount + 1;

        [Header("Buoyancy")]
        [SerializeField, Min(0.05f)] private float floatDepth = 0.55f;
        [SerializeField, Min(0f)] private float buoyancyMultiplier = 1.3f;
        [SerializeField, Min(0f)] private float waterDrag = 0.35f;
        [SerializeField, Min(0f)] private float waterAngularDrag = 0.3f;
        [SerializeField, Range(0.1f, 1.5f)] private float maximumSubmersion = 1f;
        [Tooltip("Maximum upward acceleration available to each mass-weighted hull section. Must remain above gravity so a submerged ship can recover.")]
        [SerializeField, Min(10f)] private float maximumPointAcceleration = 20f;
        [SerializeField, Min(0.1f)] private float maximumUpwardSpeed = 5f;
        [SerializeField, Min(0.1f)] private float maximumDownwardSpeed = 18f;

        [Header("Wave Alignment")]
        [SerializeField, Min(0f)] private float waveAlignmentStrength = 3f;
        [SerializeField, Min(0f)] private float waveAlignmentDamping = 0.5f;
        [SerializeField, Min(0.1f)] private float maximumWaveAlignmentAcceleration = 2.25f;
        [SerializeField, Min(0.1f)] private float surfaceNormalResponse = 8f;

        [Header("Wave Sample Stabilization")]
        [SerializeField, Range(0.01f, 1f)] private float sampleResponse = 0.7f;
        [SerializeField, Range(0f, 2f)] private float physicsWaveHeightMultiplier = 1.3f;
        [SerializeField, Range(1f, 3f)] private float waveSlopeMultiplier = 1.5f;
        [SerializeField, Min(0.01f)] private float maximumSampleStep = 0.45f;
        [SerializeField, Min(0.1f)] private float maximumWaveDisplacement = 2f;
        [SerializeField, Min(0.1f)] private float maximumCornerHeightDifference = 1.25f;

        private readonly Vector3[] localBuoyancyPoints = new Vector3[SampleCount];
        private readonly float[] cornerMassShares = new float[CornerProbeCount];
        private readonly Vector2[] samplePositions = new Vector2[SampleCount];
        private readonly float[] sampledHeights = new float[SampleCount];
        private readonly Vector3[] worldCornerPoints = new Vector3[CornerProbeCount];
        private readonly float[] cornerSurfaceHeights = new float[CornerProbeCount];
        private Vector3 smoothedSurfaceNormal = Vector3.up;

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

        public bool HasOceanSamples => hasOceanSamples;

        public float AverageSampledHeight
        {
            get
            {
                if (sampledHeights == null || sampledHeights.Length == 0)
                {
                    return 0f;
                }

                float total = 0f;
                for (int index = 0; index < sampledHeights.Length; ++index)
                {
                    total += sampledHeights[index];
                }

                return total / sampledHeights.Length;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ship = GetComponent<Ship>();
            ocean = FindAnyObjectByType<OceanWaveGenerator>();
            samplingCompute = Resources.Load<ComputeShader>("Water/OceanPhysicsSampling");
            BuildBuoyancyPoints();

            if (samplingCompute == null)
            {
                Debug.LogError("Missing Resources/Water/OceanPhysicsSampling.compute.", this);
                return;
            }

            sampleKernel = samplingCompute.FindKernel("SampleOcean");
            samplePositionBuffer = new ComputeBuffer(localBuoyancyPoints.Length, sizeof(float) * 2);
            sampleResultBuffer = new ComputeBuffer(localBuoyancyPoints.Length, sizeof(float) * 4);
        }

        private void OnDestroy()
        {
            requestVersion++;
            samplePositionBuffer?.Release();
            sampleResultBuffer?.Release();
            samplePositionBuffer = null;
            sampleResultBuffer = null;
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

            if (ship != null && ship.BlockCount != cachedBlockCount)
            {
                BuildBuoyancyPoints();
            }

            ApplyBuoyancy();
            LimitVerticalSpeed();
            RequestOceanSamples();
        }

        private void ApplyBuoyancy()
        {
            Vector3 gravity = Physics.gravity;
            Vector3 centerPoint = transform.TransformPoint(
                localBuoyancyPoints[CenterProbeIndex]);
            float centerSurfaceHeight = CastWaterProbe(
                CenterProbeIndex,
                centerPoint);

            for (int index = 0; index < CornerProbeCount; ++index)
            {
                worldCornerPoints[index] = transform.TransformPoint(
                    localBuoyancyPoints[index]);
                float sampledCornerHeight = CastWaterProbe(
                    index,
                    worldCornerPoints[index]);
                float amplifiedSlopeOffset = Mathf.Clamp(
                    (sampledCornerHeight - centerSurfaceHeight) * waveSlopeMultiplier,
                    -maximumCornerHeightDifference,
                    maximumCornerHeightDifference);
                cornerSurfaceHeights[index] = centerSurfaceHeight + amplifiedSlopeOffset;
            }

            UpdateSurfaceNormal();

            for (int index = 0; index < CornerProbeCount; ++index)
            {
                Vector3 worldPoint = worldCornerPoints[index];
                float surfaceHeight = cornerSurfaceHeights[index];
                float submersion = Mathf.Clamp(
                    (surfaceHeight - worldPoint.y) / floatDepth,
                    0f,
                    maximumSubmersion);
                if (submersion <= 0f)
                {
                    continue;
                }

                Vector3 pointVelocity = body.GetPointVelocity(worldPoint);
                if (!IsFinite(pointVelocity))
                {
                    continue;
                }

                Vector3 gravityDirection = gravity.sqrMagnitude > 0.001f
                    ? gravity.normalized
                    : Vector3.down;
                Vector3 verticalVelocity = Vector3.Project(
                    pointVelocity,
                    gravityDirection);
                float supportedMass = body.mass * cornerMassShares[index];
                Vector3 liftForce = -gravity
                    * (supportedMass * buoyancyMultiplier * submersion);
                Vector3 dampingForce = -verticalVelocity
                    * (supportedMass * waterDrag * submersion);
                Vector3 force = Vector3.ClampMagnitude(
                    liftForce + dampingForce,
                    supportedMass * maximumPointAcceleration);
                body.AddForceAtPosition(force, worldPoint, ForceMode.Force);
            }

            body.AddTorque(
                -body.angularVelocity * waterAngularDrag,
                ForceMode.Acceleration);

            Vector3 alignmentAxis = Vector3.Cross(
                transform.up,
                smoothedSurfaceNormal);
            Vector3 rockingVelocity = Vector3.ProjectOnPlane(
                body.angularVelocity,
                smoothedSurfaceNormal);
            Vector3 alignmentAcceleration = Vector3.ClampMagnitude(
                alignmentAxis * waveAlignmentStrength
                    - rockingVelocity * waveAlignmentDamping,
                maximumWaveAlignmentAcceleration);
            body.AddTorque(alignmentAcceleration, ForceMode.Acceleration);
        }

        private void UpdateSurfaceNormal()
        {
            Vector3 rearLeft = new Vector3(
                worldCornerPoints[0].x,
                cornerSurfaceHeights[0],
                worldCornerPoints[0].z);
            Vector3 rearRight = new Vector3(
                worldCornerPoints[1].x,
                cornerSurfaceHeights[1],
                worldCornerPoints[1].z);
            Vector3 frontLeft = new Vector3(
                worldCornerPoints[2].x,
                cornerSurfaceHeights[2],
                worldCornerPoints[2].z);
            Vector3 frontRight = new Vector3(
                worldCornerPoints[3].x,
                cornerSurfaceHeights[3],
                worldCornerPoints[3].z);

            Vector3 leftCenter = (rearLeft + frontLeft) * 0.5f;
            Vector3 rightCenter = (rearRight + frontRight) * 0.5f;
            Vector3 rearCenter = (rearLeft + rearRight) * 0.5f;
            Vector3 frontCenter = (frontLeft + frontRight) * 0.5f;
            Vector3 surfaceRight = rightCenter - leftCenter;
            Vector3 surfaceForward = frontCenter - rearCenter;
            Vector3 targetNormal = Vector3.Cross(surfaceForward, surfaceRight).normalized;

            if (!IsFinite(targetNormal) || targetNormal.sqrMagnitude < 0.5f)
            {
                targetNormal = Vector3.up;
            }
            else if (Vector3.Dot(targetNormal, Vector3.up) < 0f)
            {
                targetNormal = -targetNormal;
            }

            float normalBlend = 1f - Mathf.Exp(
                -surfaceNormalResponse * Time.fixedDeltaTime);
            smoothedSurfaceNormal = Vector3.Slerp(
                smoothedSurfaceNormal,
                targetNormal,
                normalBlend).normalized;
        }

        private float CastWaterProbe(int probeIndex, Vector3 hullPoint)
        {
            float oceanBaseHeight = ocean != null
                ? ocean.transform.position.y
                : 0f;
            float waveOffset = hasOceanSamples && IsFinite(sampledHeights[probeIndex])
                ? Mathf.Clamp(
                    sampledHeights[probeIndex] * physicsWaveHeightMultiplier,
                    -maximumWaveDisplacement,
                    maximumWaveDisplacement)
                : 0f;
            float surfaceHeight = oceanBaseHeight + waveOffset;

            // The rendered ocean has no Physics collider. Cast a vertical ray
            // against the sampled wave plane at this hull corner instead.
            float rayOriginHeight = Mathf.Max(
                hullPoint.y + maximumWaveDisplacement + floatDepth,
                surfaceHeight + 0.01f);
            Ray waterRay = new Ray(
                new Vector3(hullPoint.x, rayOriginHeight, hullPoint.z),
                Vector3.down);
            Plane sampledSurface = new Plane(
                Vector3.up,
                new Vector3(hullPoint.x, surfaceHeight, hullPoint.z));

            if (sampledSurface.Raycast(waterRay, out float hitDistance))
            {
                Debug.DrawRay(
                    waterRay.origin,
                    waterRay.direction * hitDistance,
                    Color.cyan,
                    Time.fixedDeltaTime);
                return waterRay.GetPoint(hitDistance).y;
            }

            return surfaceHeight;
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
                !SystemInfo.supportsAsyncGPUReadback)
            {
                return;
            }

            int sampleCount = localBuoyancyPoints.Length;

            for (int index = 0; index < sampleCount; ++index)
            {
                Vector3 worldPoint = transform.TransformPoint(localBuoyancyPoints[index]);
                samplePositions[index] = new Vector2(worldPoint.x, worldPoint.z);
            }

            if (!ocean.BindPhysicsSamplingResources(samplingCompute, sampleKernel))
            {
                return;
            }

            samplePositionBuffer.SetData(samplePositions);
            samplingCompute.SetInt("_SampleCount", sampleCount);
            samplingCompute.SetBuffer(sampleKernel, "_SamplePositions", samplePositionBuffer);
            samplingCompute.SetBuffer(sampleKernel, "_SampleResults", sampleResultBuffer);
            samplingCompute.Dispatch(sampleKernel, Mathf.CeilToInt(sampleCount / 8f), 1, 1);

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

                for (int index = 0; index < Mathf.Min(samples.Length, sampleCount); ++index)
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

        private void BuildBuoyancyPoints()
        {
            Block[] blocks = GetComponentsInChildren<Block>(true);
            cachedBlockCount = ship != null ? ship.BlockCount : blocks.Length;
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            bool hasBounds = false;

            foreach (Block block in blocks)
            {
                if (block == null || !block.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Collider[] colliders = block.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders)
                {
                    if (collider == null || collider.isTrigger || !collider.enabled)
                    {
                        continue;
                    }

                    Bounds worldBounds = collider.bounds;
                    Vector3 localMinimum = transform.InverseTransformPoint(worldBounds.min);
                    Vector3 localMaximum = transform.InverseTransformPoint(worldBounds.max);
                    Bounds colliderBounds = new Bounds();
                    colliderBounds.SetMinMax(
                        Vector3.Min(localMinimum, localMaximum),
                        Vector3.Max(localMinimum, localMaximum));

                    if (!hasBounds)
                    {
                        localBounds = colliderBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(colliderBounds.min);
                        localBounds.Encapsulate(colliderBounds.max);
                    }
                }
            }

            if (!hasBounds)
            {
                localBounds = new Bounds(Vector3.zero, Vector3.one);
            }

            Vector3 minimum = localBounds.min;
            Vector3 maximum = localBounds.max;
            float probeHeight = minimum.y + localBounds.size.y * 0.1f;

            localBuoyancyPoints[0] = new Vector3(minimum.x, probeHeight, minimum.z);
            localBuoyancyPoints[1] = new Vector3(maximum.x, probeHeight, minimum.z);
            localBuoyancyPoints[2] = new Vector3(minimum.x, probeHeight, maximum.z);
            localBuoyancyPoints[3] = new Vector3(maximum.x, probeHeight, maximum.z);
            localBuoyancyPoints[CenterProbeIndex] = new Vector3(
                localBounds.center.x,
                probeHeight,
                localBounds.center.z);

            for (int index = 0; index < CornerProbeCount; ++index)
            {
                cornerMassShares[index] = 0f;
            }

            int contributingBlocks = 0;
            foreach (Block block in blocks)
            {
                if (block == null || !block.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 localBlockPosition = transform.InverseTransformPoint(
                    block.transform.position);
                float horizontalPosition = localBounds.size.x > 0.001f
                    ? Mathf.InverseLerp(minimum.x, maximum.x, localBlockPosition.x)
                    : 0.5f;
                float depthPosition = localBounds.size.z > 0.001f
                    ? Mathf.InverseLerp(minimum.z, maximum.z, localBlockPosition.z)
                    : 0.5f;

                cornerMassShares[0] += (1f - horizontalPosition) * (1f - depthPosition);
                cornerMassShares[1] += horizontalPosition * (1f - depthPosition);
                cornerMassShares[2] += (1f - horizontalPosition) * depthPosition;
                cornerMassShares[3] += horizontalPosition * depthPosition;
                contributingBlocks++;
            }

            if (contributingBlocks == 0)
            {
                for (int index = 0; index < CornerProbeCount; ++index)
                {
                    cornerMassShares[index] = 1f / CornerProbeCount;
                }

                return;
            }

            for (int index = 0; index < CornerProbeCount; ++index)
            {
                cornerMassShares[index] /= contributingBlocks;
            }
        }

        private void OnValidate()
        {
            floatDepth = Mathf.Max(0.05f, floatDepth);
            buoyancyMultiplier = Mathf.Max(0f, buoyancyMultiplier);
            waterDrag = Mathf.Max(0f, waterDrag);
            waterAngularDrag = Mathf.Max(0f, waterAngularDrag);
            maximumPointAcceleration = Mathf.Max(10f, maximumPointAcceleration);
            maximumUpwardSpeed = Mathf.Max(0.1f, maximumUpwardSpeed);
            maximumDownwardSpeed = Mathf.Max(0.1f, maximumDownwardSpeed);
            waveAlignmentStrength = Mathf.Max(0f, waveAlignmentStrength);
            waveAlignmentDamping = Mathf.Max(0f, waveAlignmentDamping);
            maximumWaveAlignmentAcceleration = Mathf.Max(
                0.1f,
                maximumWaveAlignmentAcceleration);
            surfaceNormalResponse = Mathf.Max(0.1f, surfaceNormalResponse);
            maximumSampleStep = Mathf.Max(0.01f, maximumSampleStep);
            maximumWaveDisplacement = Mathf.Max(0.1f, maximumWaveDisplacement);
            maximumCornerHeightDifference = Mathf.Max(0.1f, maximumCornerHeightDifference);
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
