using System.Collections;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Lightweight, asset-free level-up effect for mobile/AR: two expanding
    /// ground rings and a small upward sparkle burst. It listens to growth stage
    /// changes and creates its runtime-only renderers lazily.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PetGrowthController), typeof(CapsuleCollider))]
    public sealed class PetGrowthVfx : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.2f)] private float duration = 1.25f;

        [Header("Rings")]
        [SerializeField, Range(24, 64)] private int ringSegments = 40;
        [SerializeField, Min(0.005f)] private float ringWidth = 0.018f;
        [SerializeField] private Color youngColor = new(1f, 0.86f, 0.32f, 0.9f);
        [SerializeField] private Color adultColor = new(1f, 0.58f, 0.12f, 0.95f);

        [Header("Sparkles")]
        [SerializeField, Range(6, 48)] private int youngSparkles = 18;
        [SerializeField, Range(6, 64)] private int adultSparkles = 28;

        private PetGrowthController growth;
        private Transform effectRoot;
        private LineRenderer outerRing;
        private LineRenderer innerRing;
        private ParticleSystem sparkles;
        private Material ringMaterial;
        private Material sparkleMaterial;
        private Texture2D sparkleTexture;
        private Coroutine playRoutine;

        public bool IsPlaying => playRoutine != null;

        private void Awake()
        {
            growth = GetComponent<PetGrowthController>();
        }

        private void OnEnable()
        {
            if (growth == null) growth = GetComponent<PetGrowthController>();
            if (growth != null) growth.StageChanged += Play;
        }

        private void OnDisable()
        {
            if (growth != null) growth.StageChanged -= Play;
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
            SetRingsVisible(false);
            if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Play(PetGrowthStage stage)
        {
            if (stage == PetGrowthStage.Baby || !isActiveAndEnabled)
                return;

            EnsureBuilt();
            if (playRoutine != null)
                StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayEffect(stage));
        }

        private void EnsureBuilt()
        {
            if (effectRoot != null)
                return;

            var root = new GameObject("Growth VFX (Runtime)");
            effectRoot = root.transform;
            effectRoot.SetParent(transform, false);
            effectRoot.localPosition = Vector3.up * 0.008f;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
                spriteShader = Shader.Find("Universal Render Pipeline/Unlit");

            ringMaterial = new Material(spriteShader)
            {
                name = "Growth Ring (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            outerRing = CreateRing("Outer Ring", 0);
            innerRing = CreateRing("Inner Ring", 1);

            sparkleTexture = CreateSoftDotTexture();
            sparkleMaterial = new Material(spriteShader)
            {
                name = "Growth Sparkle (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = sparkleTexture
            };
            sparkles = CreateSparkles();
            SetRingsVisible(false);
        }

        private LineRenderer CreateRing(string objectName, int sortingOrder)
        {
            var ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(effectRoot, false);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = ringSegments;
            ring.startWidth = ringWidth;
            ring.endWidth = ringWidth;
            ring.numCornerVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = ringMaterial;
            ring.sortingOrder = sortingOrder;

            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
            return ring;
        }

        private ParticleSystem CreateSparkles()
        {
            var sparkleObject = new GameObject("Rising Sparkles");
            sparkleObject.transform.SetParent(effectRoot, false);
            var system = sparkleObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 64;
            main.startSpeed = 0f;
            main.startLifetime = 0.9f;
            main.startSize = 0.04f;
            main.startColor = Color.white;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLife = system.colorOverLifetime;
            colorOverLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLife.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 0.15f)));

            ParticleSystemRenderer renderer = sparkleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = sparkleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 2;
            return system;
        }

        private IEnumerator PlayEffect(PetGrowthStage stage)
        {
            Color color = stage == PetGrowthStage.Adult ? adultColor : youngColor;
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            float radius = Mathf.Max(0.2f, capsule.radius * 1.35f);
            float petHeight = Mathf.Max(0.35f, capsule.height);

            SetRingsVisible(true);
            EmitSparkles(color, stage == PetGrowthStage.Adult ? adultSparkles : youngSparkles,
                radius, petHeight);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float alpha = 1f - Mathf.SmoothStep(0.35f, 1f, t);

                SetRing(outerRing, radius * Mathf.Lerp(0.55f, 1.7f, eased),
                    color, alpha);

                float innerT = Mathf.Clamp01((elapsed - 0.12f) /
                                             Mathf.Max(0.01f, duration - 0.12f));
                float innerEase = 1f - Mathf.Pow(1f - innerT, 3f);
                SetRing(innerRing, radius * Mathf.Lerp(0.35f, 1.28f, innerEase),
                    Color.Lerp(Color.white, color, 0.62f), alpha * innerT * 0.82f);
                yield return null;
            }

            SetRingsVisible(false);
            playRoutine = null;
        }

        private void EmitSparkles(Color color, int count, float radius, float petHeight)
        {
            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparkles.Play(true);

            float angleOffset = Time.time * 1.7f;
            for (int i = 0; i < count; i++)
            {
                float angle = angleOffset + i * Mathf.PI * 2f / count;
                float distance = radius * Mathf.Lerp(0.42f, 0.92f, (i % 4) / 3f);
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var emit = new ParticleSystem.EmitParams
                {
                    position = radial * distance + Vector3.up * (0.015f + (i % 3) * 0.018f),
                    velocity = Vector3.up * Mathf.Lerp(petHeight * 0.55f, petHeight * 0.95f,
                                   (i % 5) / 4f) + radial * 0.045f,
                    startLifetime = Mathf.Lerp(0.68f, 1.08f, (i % 6) / 5f),
                    startSize = Mathf.Lerp(0.026f, 0.055f, (i % 4) / 3f),
                    startColor = Color.Lerp(Color.white, color, 0.58f + (i % 3) * 0.15f)
                };
                sparkles.Emit(emit, 1);
            }
        }

        private static void SetRing(LineRenderer ring, float scale, Color color, float alpha)
        {
            ring.transform.localScale = new Vector3(scale, 1f, scale);
            color.a *= Mathf.Clamp01(alpha);
            ring.startColor = color;
            ring.endColor = color;
        }

        private void SetRingsVisible(bool visible)
        {
            if (outerRing != null) outerRing.enabled = visible;
            if (innerRing != null) innerRing.enabled = visible;
        }

        private static Texture2D CreateSoftDotTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Growth Sparkle Dot (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
            if (sparkleMaterial != null) Destroy(sparkleMaterial);
            if (sparkleTexture != null) Destroy(sparkleTexture);
        }
    }
}
