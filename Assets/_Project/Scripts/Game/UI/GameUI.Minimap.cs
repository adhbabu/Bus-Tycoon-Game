using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    // Mini harita: harita dokusu, hat çizgileri, hatlarda giden otobüsler (model çizimleri), hatlı şehirler ve kameranın
    // gördüğü alan. Tıklayınca ya da sürükleyince kamera oraya gider; başlıktaki düğme küçültür.
    public sealed partial class GameUI
    {
        const int MinimapTextureWidth = 512;

        ChartTexture minimapRoutes;
        int minimapRoutesVersion = -1;
        bool minimapCollapsed, minimapDragging;
        readonly List<WorldRenderer.RouteShape> minimapShapes = new List<WorldRenderer.RouteShape>();
        readonly List<WorldRenderer.BusMarker> minimapBuses = new List<WorldRenderer.BusMarker>();
        readonly Vector3[] viewCorners = new Vector3[4];

        float MinimapMapWidth => (compact ? 250f : 330f) * u;
        float MinimapMapHeight => MinimapMapWidth * world.MapBoundsXZ.height / Mathf.Max(1f, world.MapBoundsXZ.width);
        float MinimapPad => 8f * u;
        float MinimapHeader => 30f * u;

        Rect MinimapRect
        {
            get
            {
                float width = MinimapMapWidth + 2f * MinimapPad;
                float height = MinimapHeader + (minimapCollapsed ? 0f : MinimapMapHeight + MinimapPad);
                return new Rect(ContentLeft + Margin, ContentBottom - height - Margin, width, height);
            }
        }

        void DrawMinimap()
        {
            Rect rect = MinimapRect;
            uiRects.Add(rect);
            GUI.Box(rect, GUIContent.none, s.Panel);

            // Başlık: ikon + HARİTA + küçült/büyüt
            float iconSize = 16f * u;
            UIIcons.Draw(new Rect(rect.x + MinimapPad + 2f * u, rect.y + (MinimapHeader - iconSize) / 2f + 1f * u, iconSize, iconSize), UIIcon.Routes);
            GUI.Label(new Rect(rect.x + MinimapPad + iconSize + 8f * u, rect.y + 7f * u, 160f * u, 18f * u), "HARİTA", s.StatLabel);
            var toggle = new Rect(rect.xMax - MinimapPad - 30f * u, rect.y + 4f * u, 30f * u, 22f * u);
            if (GUI.Button(toggle, minimapCollapsed ? "+" : "–", s.Tab)) Defer(() => minimapCollapsed = !minimapCollapsed);
            if (minimapCollapsed) return;

            var map = new Rect(rect.x + MinimapPad, rect.y + MinimapHeader, MinimapMapWidth, MinimapMapHeight);
            HandleMinimapInput(map);

            GUI.BeginGroup(map);
            var local = new Rect(0f, 0f, map.width, map.height);
            if (Event.current.type == EventType.Repaint)
            {
                if (world.MapTexture != null) GUI.DrawTexture(local, world.MapTexture);
                else DrawRect(local, world.SeaColor);

                UpdateMinimapRoutes();
                if (minimapRoutes != null) GUI.DrawTexture(local, minimapRoutes.Texture);

                // Hatlı şehirler ve merkez
                foreach (var marker in world.Markers)
                {
                    bool hq = marker.City == world.Headquarters;
                    if (!hq && !connectedCityIds.Contains(marker.City.Id)) continue;
                    Vector2 p = WorldToMinimap(marker.Position, local);
                    DrawDot(p, (hq ? 4.5f : 3f) * u, Color.white);
                    DrawDot(p, (hq ? 3.2f : 2f) * u, hq ? UIStyles.Gold : UIStyles.Green);
                }

                // Otobüsler: gittikleri yöne bakan model çizimleri
                world.GetBusMarkers(minimapBuses);
                float busHeight = (compact ? 10f : 11f) * u, busWidth = busHeight * UIBusArt.Aspect;
                foreach (var bus in minimapBuses)
                {
                    Vector2 p = WorldToMinimap(bus.Position, local);
                    UIBusArt.Draw(new Rect(p.x - busWidth / 2f, p.y - busHeight * 0.75f, busWidth, busHeight), bus.Model, bus.Forward.x < 0f);
                }

                DrawCameraView(local);
            }
            GUI.EndGroup();

            // İnce kenar çizgisi
            if (Event.current.type == EventType.Repaint)
            {
                Color edge = new Color(1f, 1f, 1f, 0.12f);
                DrawRect(new Rect(map.x, map.y, map.width, u), edge);
                DrawRect(new Rect(map.x, map.yMax - u, map.width, u), edge);
                DrawRect(new Rect(map.x, map.y, u, map.height), edge);
                DrawRect(new Rect(map.xMax - u, map.y, u, map.height), edge);
            }
        }

        /// <summary>Hat çizgileri dokuya yalnız hatlar ya da renkleri değişince yeniden çizilir.</summary>
        void UpdateMinimapRoutes()
        {
            if (minimapRoutes != null && minimapRoutesVersion == world.RoutesVersion) return;
            minimapRoutesVersion = world.RoutesVersion;
            Rect bounds = world.MapBoundsXZ;
            int height = Mathf.Max(8, Mathf.RoundToInt(MinimapTextureWidth * bounds.height / Mathf.Max(1f, bounds.width)));
            if (minimapRoutes == null || minimapRoutes.Height != height) minimapRoutes = new ChartTexture(MinimapTextureWidth, height);

            minimapRoutes.Clear(new Color32(0, 0, 0, 0));
            world.GetRouteShapes(minimapShapes);
            foreach (bool selectedPass in new[] { false, true }) // seçili hat en üstte
            {
                foreach (var shape in minimapShapes)
                {
                    if (shape.Selected != selectedPass) continue;
                    Color32 color = shape.Selected ? Color.Lerp(shape.Color, Color.white, 0.35f) : shape.Color;
                    color.a = 255;
                    int thickness = shape.Selected ? 5 : 3;
                    for (int i = 1; i < shape.Points.Length; i++)
                    {
                        Vector2 a = WorldToTexture(shape.Points[i - 1], bounds, height);
                        Vector2 b = WorldToTexture(shape.Points[i], bounds, height);
                        minimapRoutes.Line(a.x, a.y, b.x, b.y, color, thickness);
                    }
                }
            }
            minimapRoutes.Apply();
        }

        static Vector2 WorldToTexture(Vector3 world, Rect bounds, int height) =>
            new Vector2((world.x - bounds.xMin) / bounds.width * (MinimapTextureWidth - 1), (world.z - bounds.yMin) / bounds.height * (height - 1));

        Vector2 WorldToMinimap(Vector3 point, Rect local)
        {
            Rect bounds = world.MapBoundsXZ;
            return new Vector2(local.x + (point.x - bounds.xMin) / bounds.width * local.width,
                local.y + (1f - (point.z - bounds.yMin) / bounds.height) * local.height);
        }

        /// <summary>Kameranın yerde gördüğü alan (yamuk): ekran köşelerinin yer düzlemindeki izdüşümü.</summary>
        void DrawCameraView(Rect local)
        {
            var screen = new[] { new Vector2(0f, 0f), new Vector2(Screen.width, 0f), new Vector2(Screen.width, Screen.height), new Vector2(0f, Screen.height) };
            for (int i = 0; i < 4; i++)
                if (!mapCamera.TryGetGroundPoint(screen[i], out viewCorners[i])) return;
            Color color = new Color(1f, 1f, 1f, 0.85f);
            for (int i = 0; i < 4; i++)
                DrawLine(WorldToMinimap(viewCorners[i], local), WorldToMinimap(viewCorners[(i + 1) % 4], local), 1.5f * u, color);
        }

        /// <summary>İnce döndürülmüş dikdörtgen olarak çizgi (IMGUI'de çizgi primitifi yok).</summary>
        static void DrawLine(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            DrawRect(new Rect(a.x, a.y - width / 2f, length, width), color);
            GUI.matrix = saved;
        }

        /// <summary>Mini haritaya tıklayınca/sürükleyince kamera oraya kayar.</summary>
        void HandleMinimapInput(Rect map)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseUp) minimapDragging = false;
            bool start = e.type == EventType.MouseDown && map.Contains(e.mousePosition);
            if (!start && !(minimapDragging && e.type == EventType.MouseDrag)) return;

            minimapDragging = true;
            Rect bounds = world.MapBoundsXZ;
            float x = Mathf.Clamp01((e.mousePosition.x - map.x) / map.width);
            float y = Mathf.Clamp01(1f - (e.mousePosition.y - map.y) / map.height);
            mapCamera.FocusOn(new Vector3(bounds.xMin + x * bounds.width, 0f, bounds.yMin + y * bounds.height));
            e.Use();
        }
    }
}
