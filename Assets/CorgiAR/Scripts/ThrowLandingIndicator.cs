using UnityEngine;

namespace CorgiAR
{
    /// <summary>Small runtime-only ground ring used while aiming a desktop throw.</summary>
    [DisallowMultipleComponent]
    public sealed class ThrowLandingIndicator : MonoBehaviour
    {
        private const int Segments = 32;
        private LineRenderer line;
        private Material runtimeMaterial;
        private bool limited;
        private float radius;

        public static ThrowLandingIndicator Create(float footprintRadius)
        {
            var root = new GameObject("Throw Landing Preview");
            var indicator = root.AddComponent<ThrowLandingIndicator>();
            indicator.Build(Mathf.Max(0.09f, footprintRadius * 1.15f));
            return indicator;
        }

        public static float MeasureFootprint(Transform root, float fallback = 0.14f)
        {
            if (root == null)
                return fallback;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer is TrailRenderer || !renderer.enabled)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found ? Mathf.Max(bounds.extents.x, bounds.extents.z, 0.03f) : fallback;
        }

        public void Show(Vector3 groundPoint, bool isLimited)
        {
            limited = isLimited;
            transform.position = groundPoint + Vector3.up * 0.012f;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            ApplyAppearance();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!limited || line == null)
                return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.08f;
            transform.localScale = Vector3.one * pulse;
        }

        private void Build(float ringRadius)
        {
            radius = ringRadius;
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.startWidth = 0.018f;
            line.endWidth = 0.018f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader);
                line.sharedMaterial = runtimeMaterial;
            }

            for (int index = 0; index < Segments; index++)
            {
                float angle = index * Mathf.PI * 2f / Segments;
                line.SetPosition(index,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            if (line == null)
                return;
            Color color = limited
                ? new Color(1f, 0.74f, 0.28f, 0.72f)
                : new Color(0.88f, 1f, 0.76f, 0.48f);
            line.startColor = color;
            line.endColor = color;
            if (!limited)
                transform.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
