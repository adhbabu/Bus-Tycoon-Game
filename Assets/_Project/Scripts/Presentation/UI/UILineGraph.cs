using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// Harici paket gerektirmeyen hafif çizgi grafik (uGUI Graphic). Fiyat panelinde kâr eğrisini,
    /// "ideal bölge" bandını, sıfır çizgisini ve seçili/önerilen fiyat işaretçilerini çizer.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UILineGraph : MaskableGraphic
    {
        [SerializeField, Min(0.5f)] float lineThickness = 3f;
        [SerializeField, Min(0.5f)] float markerThickness = 2f;
        [SerializeField] Color zeroLineColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] Color bandColor = new Color(0.3f, 0.85f, 0.45f, 0.18f);
        [SerializeField] Color primaryMarkerColor = Color.white;
        [SerializeField] Color secondaryMarkerColor = new Color(0.3f, 0.85f, 0.45f, 0.9f);

        readonly List<Vector2> points = new List<Vector2>();
        Color seriesColor = Color.white;
        float xMin, xMax = 1f, yMin, yMax = 1f;
        float? primaryMarker, secondaryMarker;
        float? bandFrom, bandTo;

        public void SetSeries(IReadOnlyList<Vector2> values, Color color)
        {
            points.Clear();
            points.AddRange(values);
            seriesColor = color;

            if (points.Count > 0)
            {
                xMin = xMax = points[0].x;
                yMin = yMax = points[0].y;
                foreach (var p in points)
                {
                    xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                    yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
                }
                // Sıfır çizgisi her zaman görünsün ve eğri kenara yapışmasın.
                yMin = Mathf.Min(yMin, 0f);
                yMax = Mathf.Max(yMax, 0f);
                float pad = (yMax - yMin) * 0.08f + 0.001f;
                yMin -= pad;
                yMax += pad;
            }
            SetVerticesDirty();
        }

        public void SetMarkers(float? primaryX, float? secondaryX)
        {
            primaryMarker = primaryX;
            secondaryMarker = secondaryX;
            SetVerticesDirty();
        }

        public void SetBand(float? fromX, float? toX)
        {
            bandFrom = fromX;
            bandTo = toX;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (points.Count < 2 || xMax <= xMin) return;

            if (bandFrom.HasValue && bandTo.HasValue)
            {
                float x0 = ToLocal(rect, bandFrom.Value, 0f).x;
                float x1 = ToLocal(rect, bandTo.Value, 0f).x;
                AddQuad(vh, new Vector2(x0, rect.yMin), new Vector2(x0, rect.yMax), new Vector2(x1, rect.yMax), new Vector2(x1, rect.yMin), bandColor);
            }

            float zeroY = ToLocal(rect, xMin, 0f).y;
            AddLine(vh, new Vector2(rect.xMin, zeroY), new Vector2(rect.xMax, zeroY), 1f, zeroLineColor);

            for (int i = 1; i < points.Count; i++)
                AddLine(vh, ToLocal(rect, points[i - 1].x, points[i - 1].y), ToLocal(rect, points[i].x, points[i].y), lineThickness, seriesColor);

            if (secondaryMarker.HasValue) AddVerticalMarker(vh, rect, secondaryMarker.Value, secondaryMarkerColor);
            if (primaryMarker.HasValue) AddVerticalMarker(vh, rect, primaryMarker.Value, primaryMarkerColor);
        }

        Vector2 ToLocal(Rect rect, float x, float y) => new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(xMin, xMax, x)),
            Mathf.Lerp(rect.yMin, rect.yMax, Mathf.InverseLerp(yMin, yMax, y)));

        void AddVerticalMarker(VertexHelper vh, Rect rect, float x, Color markerColor)
        {
            float px = ToLocal(rect, x, 0f).x;
            AddLine(vh, new Vector2(px, rect.yMin), new Vector2(px, rect.yMax), markerThickness, markerColor);
        }

        static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float thickness, Color lineColor)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);
            AddQuad(vh, from - normal, from + normal, to + normal, to - normal, lineColor);
        }

        static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color quadColor)
        {
            int start = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = quadColor;

            vertex.position = a; vh.AddVert(vertex);
            vertex.position = b; vh.AddVert(vertex);
            vertex.position = c; vh.AddVert(vertex);
            vertex.position = d; vh.AddVert(vertex);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
