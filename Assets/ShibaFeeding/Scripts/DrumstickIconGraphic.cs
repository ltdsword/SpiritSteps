using UnityEngine;
using UnityEngine.UI;

namespace ShibaFeeding
{
    /// <summary>A crisp, resolution-independent drumstick icon drawn directly by Unity UI.</summary>
    public sealed class DrumstickIconGraphic : MaskableGraphic
    {
        [SerializeField] private Color meatOutline = new Color(0.38f, 0.105f, 0.035f, 1f);
        [SerializeField] private Color meatColor = new Color(0.96f, 0.29f, 0.075f, 1f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.67f, 0.2f, 1f);
        [SerializeField] private Color boneColor = new Color(1f, 0.91f, 0.69f, 1f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center + new Vector2(-4f, 8f);
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 175f;
            const float rotation = -31f;

            // Layered low-alpha ellipses approximate a soft grounded shadow without a frame.
            Vector2 shadowCenter = rectTransform.rect.center + new Vector2(-13f, -49f) * scale;
            AddEllipse(vh, shadowCenter, new Vector2(61f, 16f) * scale, 0f,
                new Color(0.035f, 0.12f, 0.07f, 0.1f), 28);
            AddEllipse(vh, shadowCenter + Vector2.up * 2f * scale, new Vector2(49f, 12f) * scale, 0f,
                new Color(0.035f, 0.1f, 0.065f, 0.13f), 28);
            AddEllipse(vh, shadowCenter + Vector2.up * 4f * scale, new Vector2(35f, 8f) * scale, 0f,
                new Color(0.025f, 0.08f, 0.05f, 0.16f), 24);

            Color boneOutline = new Color(0.48f, 0.3f, 0.12f, 1f);
            AddRotatedQuad(vh, center + new Vector2(35f, 7f) * scale,
                new Vector2(83f, 24f) * scale, rotation, boneOutline);
            AddRotatedQuad(vh, center + new Vector2(34f, 8f) * scale,
                new Vector2(79f, 17f) * scale, rotation, boneColor);
            AddEllipse(vh, center + new Vector2(70f, -11f) * scale,
                new Vector2(17f, 17f) * scale, rotation, boneOutline, 18);
            AddEllipse(vh, center + new Vector2(81f, 9f) * scale,
                new Vector2(17f, 17f) * scale, rotation, boneOutline, 18);
            AddEllipse(vh, center + new Vector2(70f, -11f) * scale,
                new Vector2(13f, 13f) * scale, rotation, boneColor, 18);
            AddEllipse(vh, center + new Vector2(81f, 9f) * scale,
                new Vector2(13f, 13f) * scale, rotation, boneColor, 18);

            // Two overlapping lobes give the meat a friendlier hand-shaped silhouette.
            AddEllipse(vh, center + new Vector2(-26f, 6f) * scale,
                new Vector2(57f, 45f) * scale, rotation, meatOutline, 30);
            AddEllipse(vh, center + new Vector2(-52f, 8f) * scale,
                new Vector2(35f, 40f) * scale, rotation, meatOutline, 26);
            AddEllipse(vh, center + new Vector2(-25f, 7f) * scale,
                new Vector2(52f, 40f) * scale, rotation, meatColor, 30);
            AddEllipse(vh, center + new Vector2(-50f, 9f) * scale,
                new Vector2(30f, 35f) * scale, rotation, meatColor, 26);
            AddEllipse(vh, center + new Vector2(-43f, 28f) * scale,
                new Vector2(25f, 10f) * scale, rotation, highlightColor, 18);
            AddEllipse(vh, rectTransform.rect.center + new Vector2(-43f, 51f) * scale,
                new Vector2(17f, 7f) * scale, -28f, new Color(0.85f, 1f, 0.94f, 0.48f), 16);
        }

        private static void AddEllipse(VertexHelper vh, Vector2 center, Vector2 radius, float degrees,
            Color color, int segments)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            float radians = degrees * Mathf.Deg2Rad;
            float cosRotation = Mathf.Cos(radians);
            float sinRotation = Mathf.Sin(radians);

            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                Vector2 point = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                point = new Vector2(
                    point.x * cosRotation - point.y * sinRotation,
                    point.x * sinRotation + point.y * cosRotation);
                vh.AddVert(center + point, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
                vh.AddTriangle(start, start + i + 1, start + i + 2);
        }

        private static void AddRotatedQuad(VertexHelper vh, Vector2 center, Vector2 size, float degrees, Color color)
        {
            int start = vh.currentVertCount;
            Vector2 half = size * 0.5f;
            Vector2[] corners =
            {
                new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y),
                new Vector2(half.x, half.y), new Vector2(half.x, -half.y)
            };
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            foreach (Vector2 corner in corners)
            {
                Vector2 rotated = new Vector2(corner.x * cos - corner.y * sin, corner.x * sin + corner.y * cos);
                vh.AddVert(center + rotated, color, Vector2.zero);
            }
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
