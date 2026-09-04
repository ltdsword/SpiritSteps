using UnityEngine;
using UnityEngine.UI;

namespace ShibaFeeding
{
    /// <summary>Font-independent upward tutorial arrow drawn as a tiny UI mesh.</summary>
    public sealed class UpArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float width = Mathf.Min(rect.width, 30f);
            float height = Mathf.Min(rect.height, 34f);
            AddArrow(vh, center + new Vector2(1.5f, -2f), width, height,
                new Color(0.02f, 0.14f, 0.08f, color.a * 0.48f));
            AddArrow(vh, center, width, height, color);
        }

        private static void AddArrow(VertexHelper vh, Vector2 center, float width, float height, Color32 tint)
        {
            float shaftHalf = width * 0.11f;
            float headHalf = width * 0.42f;
            float bottom = center.y - height * 0.45f;
            float shoulder = center.y + height * 0.02f;
            float top = center.y + height * 0.47f;

            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(center.x - shaftHalf, bottom), tint, Vector2.zero);
            vh.AddVert(new Vector2(center.x - shaftHalf, shoulder), tint, Vector2.zero);
            vh.AddVert(new Vector2(center.x - headHalf, shoulder), tint, Vector2.zero);
            vh.AddVert(new Vector2(center.x, top), tint, Vector2.zero);
            vh.AddVert(new Vector2(center.x + headHalf, shoulder), tint, Vector2.zero);
            vh.AddVert(new Vector2(center.x + shaftHalf, shoulder), tint, Vector2.zero);
            vh.AddVert(new Vector2(center.x + shaftHalf, bottom), tint, Vector2.zero);

            vh.AddTriangle(start, start + 1, start + 6);
            vh.AddTriangle(start + 1, start + 5, start + 6);
            vh.AddTriangle(start + 1, start + 2, start + 3);
            vh.AddTriangle(start + 1, start + 3, start + 5);
            vh.AddTriangle(start + 3, start + 4, start + 5);
        }
    }
}
