using UnityEngine;

namespace ShibaFeeding
{
    public sealed class JuicyPopup : MonoBehaviour
    {
        private Camera targetCamera;
        private Vector3 startPosition;
        private float lifetime = 1.25f;
        private float elapsed;
        private TextMesh label;

        public void Initialize(string message, Color color, Camera cameraToFace)
        {
            targetCamera = cameraToFace;
            startPosition = transform.position;
            label = gameObject.AddComponent<TextMesh>();
            label.text = message;
            label.color = color;
            label.fontSize = 56;
            label.characterSize = 0.035f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null && label.font != null)
                renderer.sharedMaterial = label.font.material;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            transform.position = startPosition + Vector3.up * (0.55f * t);

            float pop = t < 0.2f ? Mathf.SmoothStep(0f, 1.2f, t / 0.2f) : Mathf.Lerp(1.2f, 0f, (t - 0.72f) / 0.28f);
            transform.localScale = Vector3.one * Mathf.Max(0f, pop);
            transform.Rotate(0f, 0f, Mathf.Sin(elapsed * 9f) * 18f * Time.deltaTime);

            if (targetCamera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position);
            if (elapsed >= lifetime)
                Destroy(gameObject);
        }
    }
}
