using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Ana ekrandaki şehir mini haritası: arka planda şehrin sokak haritası (kırpılarak kutuyu doldurur), üstünde
    /// vektörle hatlar ve dolmuşlar. Koordinatlar harita dokusunun UV'si (0..1, v yukarı). Dolmuşlar saniyede birkaç
    /// kez yeniden çizilir (çağıran <see cref="SetVehicles"/> ile).
    /// </summary>
    public sealed class UiCityMinimap : VisualElement
    {
        readonly List<(Vector2[] points, Color color)> lines = new List<(Vector2[], Color)>();
        readonly List<(Vector2 uv, Color color)> vehicles = new List<(Vector2, Color)>();
        readonly List<Vector2> scratch = new List<Vector2>();
        float textureAspect = 2f;

        public UiCityMinimap()
        {
            AddToClassList("bt-cityminimap");
            generateVisualContent += Draw;
        }

        public void SetTexture(Texture2D texture)
        {
            if (texture == null) return;
            style.backgroundImage = new StyleBackground(texture);
            if (texture.height > 0) textureAspect = texture.width / (float)texture.height;
            MarkDirtyRepaint();
        }

        public void SetLines(IReadOnlyList<(Vector2[] points, Color color)> data)
        {
            lines.Clear();
            for (int i = 0; i < data.Count; i++) lines.Add(data[i]);
            MarkDirtyRepaint();
        }

        public void SetVehicles(IReadOnlyList<(Vector2 uv, Color color)> data)
        {
            vehicles.Clear();
            for (int i = 0; i < data.Count; i++) vehicles.Add(data[i]);
            MarkDirtyRepaint();
        }

        /// <summary>UV → kutu içi nokta. Doku kutuyu doldurur (scale-and-crop): taşan kenar ortadan kırpılır.</summary>
        Vector2 ToLocal(Vector2 uv, Rect rect)
        {
            float boxAspect = rect.width / Mathf.Max(1f, rect.height);
            float u0 = 0f, uw = 1f, v0 = 0f, vh = 1f;
            if (boxAspect > textureAspect)
            {
                vh = textureAspect / boxAspect;
                v0 = (1f - vh) * 0.5f;
            }
            else
            {
                uw = boxAspect / textureAspect;
                u0 = (1f - uw) * 0.5f;
            }
            return new Vector2(rect.xMin + (uv.x - u0) / uw * rect.width, rect.yMin + (1f - (uv.y - v0) / vh) * rect.height);
        }

        void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width < 8f || rect.height < 8f) return;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            foreach (var (points, color) in lines)
            {
                if (points == null || points.Length < 2) continue;
                scratch.Clear();
                foreach (Vector2 uv in points) scratch.Add(ToLocal(uv, rect));
                // Koyu alt çizgi + renkli çizgi: haritanın üstünde okunur
                for (int pass = 0; pass < 2; pass++)
                {
                    painter.strokeColor = pass == 0 ? new Color(0.04f, 0.08f, 0.1f, 0.85f) : color;
                    painter.lineWidth = pass == 0 ? 5.5f : 3f;
                    painter.BeginPath();
                    painter.MoveTo(scratch[0]);
                    for (int i = 1; i < scratch.Count; i++) painter.LineTo(scratch[i]);
                    painter.Stroke();
                }
            }
            foreach (var (uv, color) in vehicles)
            {
                Vector2 p = ToLocal(uv, rect);
                if (!rect.Contains(p)) continue;
                for (int pass = 0; pass < 2; pass++)
                {
                    painter.fillColor = pass == 0 ? color : Color.white;
                    float radius = pass == 0 ? 4.5f : 2.6f;
                    painter.BeginPath();
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4f;
                        var q = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        if (i == 0) painter.MoveTo(q);
                        else painter.LineTo(q);
                    }
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }
    }
}
