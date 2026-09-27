using UnityEngine;
using UnityEngine.Rendering;

namespace RoyaltyBoat.Water
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class OceanWaveBuoyancy : MonoBehaviour
    {
        private const int SampleCount = 4;

        private float floatDepth = 0.15f;
        private float buoyancyMultiplier = 4.2f;
        private float waterDrag = 2.5f;
        private float waterAngularDrag = 1f;

        private readonly Vector3[] localBuoyancyPoints = new Vector3[SampleCount];
        private readonly Vector2[] samplePositions = new Vector2[SampleCount];
        private readonly float[] sampledHeights = new float[SampleCount];

        private Rigidbody body;
        private OceanWaveGenerator ocean;
        private ComputeShader samplingCompute;
        private ComputeBuffer samplePositionBuffer;
        private ComputeBuffer sampleResultBuffer;
        private int sampleKernel;
        private bool requestPending;
        private bool hasOceanSamples;
        private int requestVersion;

        public bool HasOceanSamples => hasOceanSamples;

        public float AverageSampledHeight
        {
            get
            {
                float total = 0f;
                for (int index = 0; index < SampleCount; ++index)
                {
                    total += sampledHeights[index];
                }

                return total / SampleCount;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ocean = FindAnyObjectByType<OceanWaveGenerator>();
            samplingCompute = Resources.Load<ComputeShader>("Water/OceanPhysicsSampling");
            BuildBuoyancyPoints();

            if (samplingCompute == null)
            {
                Debug.LogError("Missing Resources/Water/OceanPhysicsSampling.compute.", this);
                return;
            }

            sampleKernel = samplingCompute.FindKernel("SampleOcean");
            samplePositionBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 2);
            sampleResultBuffer = new ComputeBuffer(SampleCount, sizeof(float) * 4);
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

            ApplyBuoyancy();
            RequestOceanSamples();
        }

        private void ApplyBuoyancy()
        {
            Vector3 gravity = Physics.gravity;
            float pointShare = 1f / SampleCount;

            for (int index = 0; index < SampleCount; ++index)
            {
                Vector3 worldPoint = transform.TransformPoint(localBuoyancyPoints[index]);
                float surfaceHeight = hasOceanSamples && IsFinite(sampledHeights[index])
                    ? sampledHeights[index]
                    : 0f;
                float submersion = Mathf.Clamp(
                    (surfaceHeight - worldPoint.y) / floatDepth,
                    0f,
                    3f);
                if (submersion <= 0f)
                {
                    continue;
                }

                Vector3 pointVelocity = body.GetPointVelocity(worldPoint);
                if (!IsFinite(pointVelocity))
                {
                    continue;
                }

                Vector3 lift = -gravity * (buoyancyMultiplier * submersion * pointShare);
                Vector3 damping = -pointVelocity * (waterDrag * submersion * pointShare);
                Vector3 acceleration = lift + damping;
                body.AddForceAtPosition(acceleration, worldPoint, ForceMode.Acceleration);
            }

            body.AddTorque(
                -body.angularVelocity * waterAngularDrag,
                ForceMode.Acceleration);
        }

        private void RequestOceanSamples()
        {
            if (requestPending || ocean == null || samplingCompute == null ||
                samplePositionBuffer == null || sampleResultBuffer == null ||
                !SystemInfo.supportsAsyncGPUReadback)
            {
                return;
            }

            for (int index = 0; index < SampleCount; ++index)
            {
                Vector3 worldPoint = transform.TransformPoint(localBuoyancyPoints[index]);
                samplePositions[index] = new Vector2(worldPoint.x, worldPoint.z);
            }

            if (!ocean.BindPhysicsSamplingResources(samplingCompute, sampleKernel))
            {
                return;
            }

            samplePositionBuffer.SetData(samplePositions);
            samplingCompute.SetInt("_SampleCount", SampleCount);
            samplingCompute.SetBuffer(sampleKernel, "_SamplePositions", samplePositionBuffer);
            samplingCompute.SetBuffer(sampleKernel, "_SampleResults", sampleResultBuffer);
            samplingCompute.Dispatch(sampleKernel, 1, 1, 1);

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
                for (int index = 0; index < Mathf.Min(samples.Length, SampleCount); ++index)
                {
                    float sampledHeight = samples[index].y;
                    sampledHeights[index] = IsFinite(sampledHeight) ? sampledHeight : 0f;
                }

                hasOceanSamples = true;
            });
        }

        private void BuildBuoyancyPoints()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            bool hasBounds = false;

            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                Bounds worldBounds = collider.bounds;
                Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
                Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);
                Bounds nextBounds = new Bounds();
                nextBounds.SetMinMax(Vector3.Min(localMin, localMax), Vector3.Max(localMin, localMax));

                if (!hasBounds)
                {
                    localBounds = nextBounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(nextBounds.min);
                    localBounds.Encapsulate(nextBounds.max);
                }
            }

            Vector3 minimum = localBounds.min;
            Vector3 maximum = localBounds.max;
            float insetX = Mathf.Max(0.1f, localBounds.size.x * 0.15f);
            float insetZ = Mathf.Max(0.1f, localBounds.size.z * 0.15f);
            float bottom = minimum.y + Mathf.Min(0.2f, localBounds.extents.y * 0.5f);

            localBuoyancyPoints[0] = new Vector3(minimum.x + insetX, bottom, minimum.z + insetZ);
            localBuoyancyPoints[1] = new Vector3(maximum.x - insetX, bottom, minimum.z + insetZ);
            localBuoyancyPoints[2] = new Vector3(minimum.x + insetX, bottom, maximum.z - insetZ);
            localBuoyancyPoints[3] = new Vector3(maximum.x - insetX, bottom, maximum.z - insetZ);
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
