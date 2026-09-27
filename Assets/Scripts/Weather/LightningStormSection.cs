using System.Collections.Generic;
using RoyaltyBoat.Gameplay;
using RoyaltyBoat.King;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoyaltyBoat.Weather
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RainSection))]
    public sealed class LightningStormSection : MonoBehaviour
    {
        private const int WarningSegments = 40;
        private const int BoltSegments = 12;
        private const string UnlitShaderName = "Universal Render Pipeline/Particles/Unlit";

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float minimumStrikeDelay = 2.8f;
        [SerializeField, Min(0.1f)] private float maximumStrikeDelay = 5.2f;
        [SerializeField, Min(0.1f)] private float warningDuration = 1.05f;
        [SerializeField, Min(0.02f)] private float boltDuration = 0.18f;

        [Header("Targeting")]
        [SerializeField, Min(0f)] private float targetingJitter = 1.25f;
        [SerializeField, Min(0.1f)] private float damageRadius = 2.35f;
        [SerializeField, Min(0)] private int blockDamage = 70;
        [SerializeField, Min(0f)] private float impactForce = 16f;

        [Header("Presentation")]
        [SerializeField, Min(2f)] private float boltHeight = 34f;
        [SerializeField, Min(0.1f)] private float warningRadius = 2.8f;

        private static Material sharedBoltMaterial;
        private static Material sharedWarningMaterial;

        private readonly HashSet<Block> damagedBlocks = new HashSet<Block>();
        private readonly HashSet<Rigidbody> pushedBodies = new HashSet<Rigidbody>();

        private RainSection rainSection;
        private Ship targetShip;
        private LineRenderer warningRing;
        private LineRenderer bolt;
        private Light flashLight;
        private Vector3 strikePoint;
        private float nextStrikeTime;
        private float strikeTime;
        private float boltEndTime;
        private bool wasStormActive;
        private bool isWarning;
        private bool isBoltVisible;

        public bool IsWarning => isWarning;
        public bool IsBoltVisible => isBoltVisible;
        public Vector3 PendingStrikePoint => strikePoint;
        public int BlockDamage => blockDamage;
        public float DamageRadius => damageRadius;

        private void Awake()
        {
            rainSection = GetComponent<RainSection>();
            CreateWarningRing();
            CreateBolt();
            CreateFlashLight();
            HideEffects();
        }

        private void Update()
        {
            bool stormActive = rainSection != null && rainSection.IsRaining;
            if (!stormActive)
            {
                if (wasStormActive)
                {
                    CancelPendingStrike();
                }

                wasStormActive = false;
                return;
            }

            if (!wasStormActive)
            {
                wasStormActive = true;
                ResolveTargetShip();
                ScheduleNextStrike(1.2f, 2.1f);
            }

            if (isWarning)
            {
                UpdateWarningRing();
                if (Time.time >= strikeTime)
                {
                    ExecuteStrike();
                }
            }
            else if (!isBoltVisible && Time.time >= nextStrikeTime)
            {
                BeginWarning();
            }

            if (isBoltVisible)
            {
                UpdateBoltFlash();
                if (Time.time >= boltEndTime)
                {
                    isBoltVisible = false;
                    bolt.enabled = false;
                    flashLight.enabled = false;
                    ScheduleNextStrike(minimumStrikeDelay, maximumStrikeDelay);
                }
            }
        }

        private void OnDisable()
        {
            wasStormActive = false;
            CancelPendingStrike();
        }

        private void BeginWarning()
        {
            ResolveTargetShip();
            Transform target = ChooseStrikeTarget();
            if (target == null)
            {
                ScheduleNextStrike(0.5f, 1f);
                return;
            }

            Vector2 jitter = Random.insideUnitCircle * targetingJitter;
            strikePoint = target.position + new Vector3(jitter.x, 0f, jitter.y);
            strikeTime = Time.time + warningDuration;
            isWarning = true;
            warningRing.enabled = true;
            UpdateWarningRing();
        }

        private Transform ChooseStrikeTarget()
        {
            if (targetShip == null)
            {
                return rainSection == null ? null : rainSection.FollowTarget;
            }

            var candidates = new List<Transform>();
            foreach (Block block in targetShip.Blocks)
            {
                if (block != null && block.IsAlive)
                {
                    candidates.Add(block.transform);
                }
            }

            KingHealth king = targetShip.GetComponentInChildren<KingHealth>(true);
            if (king != null && king.IsAlive)
            {
                candidates.Add(king.transform);
            }

            if (candidates.Count == 0)
            {
                return targetShip.transform;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private void UpdateWarningRing()
        {
            float progress = 1f - Mathf.Clamp01((strikeTime - Time.time) / warningDuration);
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.08f;
            float radius = Mathf.Lerp(warningRadius * 1.35f, warningRadius * 0.72f, progress) * pulse;
            Vector3 center = new Vector3(strikePoint.x, 0.45f, strikePoint.z);

            for (int index = 0; index < WarningSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / WarningSegments;
                warningRing.SetPosition(
                    index,
                    center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            Color color = Color.Lerp(
                new Color(1f, 0.72f, 0.18f, 0.72f),
                new Color(1f, 0.96f, 0.7f, 1f),
                progress);
            warningRing.startColor = color;
            warningRing.endColor = color;
        }

        private void ExecuteStrike()
        {
            isWarning = false;
            warningRing.enabled = false;
            isBoltVisible = true;
            boltEndTime = Time.time + boltDuration;
            BuildBolt();
            bolt.enabled = true;
            flashLight.transform.position = strikePoint + Vector3.up * 8f;
            flashLight.intensity = 22f;
            flashLight.enabled = true;

            ApplyStrikeDamage();

            VoyageCameraController cameraController = Camera.main == null
                ? null
                : Camera.main.GetComponent<VoyageCameraController>();
            cameraController?.TriggerShake(1f, 0.34f);
        }

        private void ApplyStrikeDamage()
        {
            damagedBlocks.Clear();
            pushedBodies.Clear();
            Vector3 damageCenter = strikePoint + Vector3.up * 0.75f;
            Collider[] hits = Physics.OverlapSphere(
                damageCenter,
                damageRadius,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                Block block = hit.GetComponentInParent<Block>();
                if (block != null && damagedBlocks.Add(block))
                {
                    block.TakeDamage(blockDamage);
                }

                KingHealth king = hit.GetComponentInParent<KingHealth>();
                if (king != null && king.IsAlive)
                {
                    king.Kill(KingDeathCause.Lightning);
                }

                Rigidbody body = hit.attachedRigidbody;
                if (body != null && !body.isKinematic && pushedBodies.Add(body))
                {
                    body.AddExplosionForce(
                        impactForce,
                        damageCenter,
                        damageRadius * 2.5f,
                        1.5f,
                        ForceMode.Impulse);
                }
            }
        }

        private void BuildBolt()
        {
            bolt.positionCount = BoltSegments;
            Vector3 bottom = strikePoint + Vector3.up * 0.6f;
            Vector3 top = bottom + Vector3.up * boltHeight;
            for (int index = 0; index < BoltSegments; index++)
            {
                float t = index / (BoltSegments - 1f);
                Vector3 point = Vector3.Lerp(top, bottom, t);
                float taper = Mathf.Sin(t * Mathf.PI);
                point += new Vector3(
                    Random.Range(-1.35f, 1.35f) * taper,
                    0f,
                    Random.Range(-0.8f, 0.8f) * taper);
                bolt.SetPosition(index, point);
            }
        }

        private void UpdateBoltFlash()
        {
            float remaining = Mathf.Clamp01((boltEndTime - Time.time) / boltDuration);
            float flicker = Random.Range(0.72f, 1f);
            Color color = new Color(0.72f, 0.9f, 1f, remaining * flicker);
            bolt.startColor = color;
            bolt.endColor = Color.white;
            flashLight.intensity = 22f * remaining * flicker;
        }

        private void ResolveTargetShip()
        {
            if (targetShip != null)
            {
                return;
            }

            Transform followTarget = rainSection == null ? null : rainSection.FollowTarget;
            targetShip = followTarget == null
                ? FindAnyObjectByType<Ship>()
                : followTarget.GetComponentInParent<Ship>();
        }

        private void ScheduleNextStrike(float minimum, float maximum)
        {
            nextStrikeTime = Time.time + Random.Range(minimum, Mathf.Max(minimum, maximum));
        }

        private void CancelPendingStrike()
        {
            isWarning = false;
            isBoltVisible = false;
            if (warningRing != null)
            {
                warningRing.enabled = false;
            }

            if (bolt != null)
            {
                bolt.enabled = false;
            }

            if (flashLight != null)
            {
                flashLight.enabled = false;
            }
        }

        private void HideEffects()
        {
            warningRing.enabled = false;
            bolt.enabled = false;
            flashLight.enabled = false;
        }

        private void CreateWarningRing()
        {
            GameObject ringObject = new GameObject("Lightning Warning");
            ringObject.transform.SetParent(transform, false);
            warningRing = ringObject.AddComponent<LineRenderer>();
            warningRing.useWorldSpace = true;
            warningRing.loop = true;
            warningRing.positionCount = WarningSegments;
            warningRing.widthMultiplier = 0.15f;
            warningRing.numCornerVertices = 2;
            warningRing.shadowCastingMode = ShadowCastingMode.Off;
            warningRing.receiveShadows = false;
            warningRing.sharedMaterial = GetWarningMaterial();
        }

        private void CreateBolt()
        {
            GameObject boltObject = new GameObject("Lightning Bolt");
            boltObject.transform.SetParent(transform, false);
            bolt = boltObject.AddComponent<LineRenderer>();
            bolt.useWorldSpace = true;
            bolt.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.18f),
                new Keyframe(0.8f, 0.34f),
                new Keyframe(1f, 0.52f));
            bolt.widthMultiplier = 1f;
            bolt.numCornerVertices = 2;
            bolt.shadowCastingMode = ShadowCastingMode.Off;
            bolt.receiveShadows = false;
            bolt.sharedMaterial = GetBoltMaterial();
        }

        private void CreateFlashLight()
        {
            GameObject lightObject = new GameObject("Lightning Flash");
            lightObject.transform.SetParent(transform, false);
            flashLight = lightObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(0.72f, 0.86f, 1f);
            flashLight.range = 48f;
            flashLight.shadows = LightShadows.None;
        }

        private static Material GetBoltMaterial()
        {
            if (sharedBoltMaterial == null)
            {
                sharedBoltMaterial = CreateTransparentMaterial(
                    "Runtime Lightning Bolt",
                    new Color(0.72f, 0.9f, 1f, 1f));
            }

            return sharedBoltMaterial;
        }

        private static Material GetWarningMaterial()
        {
            if (sharedWarningMaterial == null)
            {
                sharedWarningMaterial = CreateTransparentMaterial(
                    "Runtime Lightning Warning",
                    new Color(1f, 0.72f, 0.18f, 0.9f));
            }

            return sharedWarningMaterial;
        }

        private static Material CreateTransparentMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void OnValidate()
        {
            minimumStrikeDelay = Mathf.Max(0.1f, minimumStrikeDelay);
            maximumStrikeDelay = Mathf.Max(minimumStrikeDelay, maximumStrikeDelay);
            warningDuration = Mathf.Max(0.1f, warningDuration);
            boltDuration = Mathf.Max(0.02f, boltDuration);
            targetingJitter = Mathf.Max(0f, targetingJitter);
            damageRadius = Mathf.Max(0.1f, damageRadius);
            blockDamage = Mathf.Max(0, blockDamage);
            impactForce = Mathf.Max(0f, impactForce);
            boltHeight = Mathf.Max(2f, boltHeight);
            warningRadius = Mathf.Max(0.1f, warningRadius);
        }
    }
}
