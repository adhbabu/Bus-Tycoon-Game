using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Küçük çizgi grafik (kasa, hat kârı): altı yarı saydam dolgulu çizgi ve son değerde bir nokta. Vektörle çizilir
    /// (Painter2D); yalnız veri değişince yeniden çizilir (günde bir).
    /// </summary>
    public sealed class UiSparkline : VisualElement
    {
        readonly List<float> values = new List<float>();
        readonly List<Vector2> points = new List<Vector2>();
        Color color = Color.white;
        int signature;

        public UiSparkline()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("bt-spark");
            generateVisualContent += Draw;
        }

        /// <summary>Veriyi ve rengi yazar; değişmediyse yeniden çizilmez.</summary>
        public void SetData(IReadOnlyList<float> data, Color lineColor)
        {
            int hash = lineColor.GetHashCode();
            for (int i = 0; i < data.Count; i++) hash = hash * 31 + data[i].GetHashCode();
            if (hash == signature && values.Count == data.Count) return;
            signature = hash;
            color = lineColor;
            values.Clear();
            for (int i = 0; i < data.Count; i++) values.Add(data[i]);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (values.Count < 2 || rect.width < 4f || rect.height < 4f) return;
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in values)
            {
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            float range = Mathf.Max(1e-3f, max - min);
            const float pad = 3f;
            points.Clear();
            for (int i = 0; i < values.Count; i++)
            {
                float x = rect.xMin + pad + (rect.width - 2f * pad) * i / (values.Count - 1);
                float y = rect.yMax - pad - (rect.height - 2f * pad) * (values[i] - min) / range;
                points.Add(new Vector2(x, y));
            }

            Painter2D painter = context.painter2D;
            painter.fillColor = new Color(color.r, color.g, color.b, 0.16f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(points[0].x, rect.yMax));
            foreach (Vector2 p in points) painter.LineTo(p);
            painter.LineTo(new Vector2(points[points.Count - 1].x, rect.yMax));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = color;
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) painter.LineTo(points[i]);
            painter.Stroke();

            // Son değerde nokta (sekizgen)
            Vector2 last = points[points.Count - 1];
            painter.fillColor = color;
            painter.BeginPath();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var p = last + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3f;
                if (i == 0) painter.MoveTo(p);
                else painter.LineTo(p);
            }
            painter.ClosePath();
            painter.Fill();
        }
    }
}
