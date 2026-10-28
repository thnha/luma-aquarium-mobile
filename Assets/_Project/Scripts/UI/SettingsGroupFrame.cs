using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumaquarium.UI
{
    // A transparent fieldset outline: the legend breaks the top edge without a painted patch.
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SettingsGroupFrame : MaskableGraphic
    {
        public TMP_Text legend;
        private const float Radius = 32, Thickness = 4, LegendPadding = 16;

        protected override void OnEnable()
        {
            base.OnEnable();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        protected override void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            base.OnDisable();
        }

        private void OnTextChanged(Object text)
        {
            if (text == legend) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float left = rect.xMin + Thickness / 2, right = rect.xMax - Thickness / 2;
            float bottom = rect.yMin + Thickness / 2, top = rect.yMax - Thickness / 2;
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) / 2 - Thickness);
            if (radius <= 0) return;

            float gapLeft = left + radius, gapRight = gapLeft;
            if (legend != null)
            {
                float start = rect.xMin + legend.rectTransform.anchoredPosition.x;
                gapLeft = Mathf.Clamp(start - LegendPadding, left + radius, right - radius);
                gapRight = Mathf.Clamp(start + legend.GetPreferredValues().x + LegendPadding, gapLeft, right - radius);
            }
            Line(mesh, new Vector2(left + radius, top), new Vector2(gapLeft, top));
            Line(mesh, new Vector2(gapRight, top), new Vector2(right - radius, top));
            Line(mesh, new Vector2(left, bottom + radius), new Vector2(left, top - radius));
            Line(mesh, new Vector2(right, bottom + radius), new Vector2(right, top - radius));
            Line(mesh, new Vector2(left + radius, bottom), new Vector2(right - radius, bottom));
            Arc(mesh, new Vector2(left + radius, top - radius), radius, 90);
            Arc(mesh, new Vector2(left + radius, bottom + radius), radius, 180);
            Arc(mesh, new Vector2(right - radius, bottom + radius), radius, 270);
            Arc(mesh, new Vector2(right - radius, top - radius), radius, 0);
        }

        private void Line(VertexHelper mesh, Vector2 start, Vector2 end)
        {
            if ((end - start).sqrMagnitude < .01f) return;
            var direction = (end - start).normalized;
            var normal = new Vector2(-direction.y, direction.x) * (Thickness / 2);
            Quad(mesh, start - normal, start + normal, end + normal, end - normal);
        }

        private void Arc(VertexHelper mesh, Vector2 center, float radius, float angle)
        {
            for (int step = 0; step < 12; step++)
            {
                float a = (angle + step * 7.5f) * Mathf.Deg2Rad;
                float b = (angle + (step + 1) * 7.5f) * Mathf.Deg2Rad;
                var from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, center + from * (radius - Thickness / 2), center + from * (radius + Thickness / 2),
                    center + to * (radius + Thickness / 2), center + to * (radius - Thickness / 2));
            }
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
