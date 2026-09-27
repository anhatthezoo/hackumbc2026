using RoyaltyBoat.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoyaltyBoat.Weather
{
    /// <summary>
    /// A course-local rain field. The particle emitters follow the player's boat while
    /// it is inside this chunk, so a long storm section does not require thousands of
    /// particles spread across the entire authored volume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainSection : MonoBehaviour
    {
        private const string RainShaderName = "Universal Render Pipeline/Particles/Unlit";

        [Header("Section")]
        [SerializeField, Min(1f)] private float sectionLength = 160f;
        [SerializeField, Min(1f)] private float sectionWidth = 60f;
        [SerializeField, Min(0f)] private float activationPadding = 12f;

        [Header("Rain Field")]
        [SerializeField, Min(1f)] private float fieldLength = 52f;
        [SerializeField, Min(1f)] private float fieldWidth = 72f;
        [SerializeField, Min(1f)] private float spawnHeight = 28f;
        [SerializeField, Min(1f)] private float fallSpeed = 38f;
        [SerializeField, Min(0f)] private float windSpeed = -5f;
        [SerializeField, Min(1f)] private float emissionRate = 1050f;

        private static Material sharedRainMaterial;
        private Transform followTarget;
        private ParticleSystem rainfall;
        private ParticleSystem surfaceSplashes;
        private bool isRaining;

        public float SectionLength => sectionLength;
        public float SectionWidth => sectionWidth;
        public bool IsRaining => isRaining;
        public Transform FollowTarget => followTarget;

        public void Configure(float length, float width)
        {
            sectionLength = Mathf.Max(1f, length);
            sectionWidth = Mathf.Max(1f, width);
        }

        private void Awake()
        {
            CreateRainfall();
            CreateSurfaceSplashes();
            SetRaining(false);
        }

        private void LateUpdate()
        {
            ResolveFollowTarget();
            if (followTarget == null)
            {
                SetRaining(false);
                return;
            }

            float localX = transform.InverseTransformPoint(followTarget.position).x;
            bool targetIsInSection = localX >= -activationPadding &&
                                     localX <= sectionLength + activationPadding;

            if (!targetIsInSection)
            {
                SetRaining(false);
                return;
            }

            Vector3 targetPosition = followTarget.position;
            float halfWidth = sectionWidth * 0.5f;
            float localZ = transform.InverseTransformPoint(targetPosition).z;
            localZ = Mathf.Clamp(localZ, -halfWidth, halfWidth);
            Vector3 clampedTarget = transform.TransformPoint(new Vector3(localX, 0f, localZ));

            rainfall.transform.position = clampedTarget + Vector3.up * spawnHeight;
            surfaceSplashes.transform.position = clampedTarget + Vector3.up * 0.35f;
            SetRaining(true);
        }

        private void OnDisable()
        {
            SetRaining(false);
        }

        private void SetRaining(bool enabled)
        {
            if (isRaining == enabled)
            {
                return;
            }

            isRaining = enabled;
            SetEmitterState(rainfall, enabled);
            SetEmitterState(surfaceSplashes, enabled);
        }

        private static void SetEmitterState(ParticleSystem particles, bool enabled)
        {
            if (particles == null)
            {
                return;
            }

            if (enabled)
            {
                particles.Play(true);
            }
            else
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void ResolveFollowTarget()
        {
            if (followTarget != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            VoyageCameraController cameraController = mainCamera == null
                ? null
                : mainCamera.GetComponent<VoyageCameraController>();
            if (cameraController != null && cameraController.Target != null)
            {
                followTarget = cameraController.Target;
                return;
            }

            Ship ship = FindAnyObjectByType<Ship>();
            followTarget = ship == null ? null : ship.transform;
        }

        private void CreateRainfall()
        {
            rainfall = CreateParticleObject("Rainfall");

            ParticleSystem.MainModule main = rainfall.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Mathf.Max(0.4f, (spawnHeight + 3f) / fallSpeed);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.58f, 0.76f, 0.9f, 0.34f),
                new Color(0.8f, 0.9f, 1f, 0.62f));
            main.maxParticles = 1800;

            ParticleSystem.EmissionModule emission = rainfall.emission;
            emission.rateOverTime = emissionRate;

            ParticleSystem.ShapeModule shape = rainfall.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(fieldLength, 0.5f, fieldWidth);

            ParticleSystem.VelocityOverLifetimeModule velocity = rainfall.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = windSpeed;
            velocity.y = -fallSpeed;

            ParticleSystemRenderer renderer = rainfall.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 5.5f;
            renderer.velocityScale = 0.08f;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = GetRainMaterial();
        }

        private void CreateSurfaceSplashes()
        {
            surfaceSplashes = CreateParticleObject("Rain Splashes");

            ParticleSystem.MainModule main = surfaceSplashes.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.7f, 0.86f, 1f, 0.25f),
                new Color(0.88f, 0.95f, 1f, 0.55f));
            main.gravityModifier = 1.5f;
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = surfaceSplashes.emission;
            emission.rateOverTime = emissionRate * 0.22f;

            ParticleSystem.ShapeModule shape = surfaceSplashes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(fieldLength, 0.05f, fieldWidth);

            ParticleSystemRenderer renderer = surfaceSplashes.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = GetRainMaterial();
        }

        private ParticleSystem CreateParticleObject(string objectName)
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(transform, false);
            return particleObject.AddComponent<ParticleSystem>();
        }

        private static Material GetRainMaterial()
        {
            if (sharedRainMaterial != null)
            {
                return sharedRainMaterial;
            }

            Shader shader = Shader.Find(RainShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            sharedRainMaterial = new Material(shader)
            {
                name = "Runtime Rain Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            sharedRainMaterial.SetFloat("_Surface", 1f);
            sharedRainMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            sharedRainMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            sharedRainMaterial.SetFloat("_ZWrite", 0f);
            sharedRainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedRainMaterial.renderQueue = (int)RenderQueue.Transparent;
            return sharedRainMaterial;
        }

        private void OnValidate()
        {
            sectionLength = Mathf.Max(1f, sectionLength);
            sectionWidth = Mathf.Max(1f, sectionWidth);
            activationPadding = Mathf.Max(0f, activationPadding);
            fieldLength = Mathf.Max(1f, fieldLength);
            fieldWidth = Mathf.Max(1f, fieldWidth);
            spawnHeight = Mathf.Max(1f, spawnHeight);
            fallSpeed = Mathf.Max(1f, fallSpeed);
            emissionRate = Mathf.Max(1f, emissionRate);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.4f);
            Gizmos.DrawWireCube(
                new Vector3(sectionLength * 0.5f, spawnHeight * 0.5f, 0f),
                new Vector3(sectionLength, spawnHeight, sectionWidth));
        }
    }
}
