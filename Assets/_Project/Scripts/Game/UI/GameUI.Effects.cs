using System;
using System.Collections.Generic;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Game
{
    // Harita üzerindeki kısa süreli efektler: gün sonunda hatların üstünden yükselip kaybolan kazanç yazıları.
    // Yalnız ekranda görünen hatlar, en çok dört tanesi; metin yaratılırken bir kez biçimlenir (her kare değil).
    public sealed partial class GameUI
    {
        const string IncomeLabelsPrefKey = "fx_income";
        const float IncomeLabelSeconds = 1.4f;
        const int MaxIncomeLabels = 4;

        struct FloatingLabel
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Start;
        }

        readonly List<FloatingLabel> floatingLabels = new List<FloatingLabel>();
        readonly List<RouteState> incomeRoutes = new List<RouteState>();
        int incomeLabelsSetting = -1;
        float lastIncomeSpawn = -10f;

        /// <summary>Gün sonu kazanç yazıları (ayar kalıcıdır; varsayılan açık).</summary>
        bool IncomeLabelsEnabled
        {
            get
            {
                if (incomeLabelsSetting < 0) incomeLabelsSetting = PlayerPrefs.GetInt(IncomeLabelsPrefKey, 1);
                return incomeLabelsSetting == 1;
            }
            set
            {
                incomeLabelsSetting = value ? 1 : 0;
                PlayerPrefs.SetInt(IncomeLabelsPrefKey, incomeLabelsSetting);
                if (!value) floatingLabels.Clear();
            }
        }

        void SpawnIncomeLabels()
        {
            if (!IncomeLabelsEnabled || mode != Mode.Playing || StationUi || modal != Modal.None) return;
            float now = Time.unscaledTime;
            if (now - lastIncomeSpawn < 1.1f) return; // 4x hızda da üst üste binmesin
            lastIncomeSpawn = now;

            Camera cam = mapCamera.Camera;
            incomeRoutes.Clear();
            foreach (var route in session.Routes.Routes)
            {
                if (!route.HasHistory || Math.Abs(route.LastReport.Profit) < 1.0) continue;
                Vector3 sp = cam.WorldToScreenPoint(world.GetRouteMidpoint(route));
                if (sp.z <= 0f || sp.x < 0f || sp.x > W || sp.y < 0f || sp.y > H) continue;
                if (IsOverUI(new Vector2(sp.x, H - sp.y))) continue;
                incomeRoutes.Add(route);
            }
            incomeRoutes.Sort((a, b) => Math.Abs(b.LastReport.Profit).CompareTo(Math.Abs(a.LastReport.Profit)));
            for (int i = 0; i < incomeRoutes.Count && i < MaxIncomeLabels; i++)
            {
                double profit = incomeRoutes[i].LastReport.Profit;
                floatingLabels.Add(new FloatingLabel
                {
                    World = world.GetRouteMidpoint(incomeRoutes[i]),
                    Text = (profit >= 0 ? "+" : "") + Fmt.Compact(profit),
                    Color = profit >= 0 ? UIStyles.Green : UIStyles.Red,
                    Start = now
                });
            }
        }

        // ------------------------------------------------------------------ kare süresi göstergesi (?fps=1)

        int showFps = -1;
        float fpsAverage, fpsWorst, fpsWindowWorst, fpsWindowStart;
        int fpsFrame = -1;
        string fpsText = string.Empty;

        Texture2D vignette;

        /// <summary>
        /// Ekran kenarı karartması: köşeler hafifçe koyulaşır, göz haritanın ortasına çekilir (sinematik çerçeve).
        /// Arayüzün altında, şehir etiketlerinden önce çizilir; Düşük kalitede kapalı.
        /// </summary>
        void DrawVignette()
        {
            if (vignette == null) vignette = CreateVignette(64);
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, W, H), vignette, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        static Texture2D CreateVignette(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.85f + dy * dy * 1.15f);  // ekran geniş: yanlar biraz daha az
                float t = Mathf.Clamp01((r - 0.62f) / 0.78f);
                float alpha = t * t * (3f - 2f * t) * 0.34f;
                pixels[y * size + x] = new Color32(4, 10, 20, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Vignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// Gerçek cihazda akıcılığı ölçmek için: sol altta ortalama kare hızı, kare süresi ve son saniyenin en kötü
        /// karesi. Bağlantıya "?fps=1" eklenince görünür; metin dörtte bir saniyede bir güncellenir.
        /// </summary>
        void DrawFpsOverlay()
        {
            if (showFps < 0) showFps = LaunchOptions.Has("fps") ? 1 : 0;
            if (showFps == 0) return;
            if (fpsFrame != Time.frameCount)
            {
                fpsFrame = Time.frameCount;
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                fpsAverage = fpsAverage <= 0f ? dt : Mathf.Lerp(fpsAverage, dt, 0.05f);
                fpsWindowWorst = Mathf.Max(fpsWindowWorst, dt);
                if (Time.unscaledTime - fpsWindowStart > 1f)
                {
                    fpsWorst = fpsWindowWorst;
                    fpsWindowWorst = 0f;
                    fpsWindowStart = Time.unscaledTime;
                }
                if (Time.frameCount % 15 == 0)
                    fpsText = $"{1f / fpsAverage:0} FPS · {fpsAverage * 1000f:0.0} ms · en kötü {fpsWorst * 1000f:0} ms";
            }
            var rect = new Rect(SafeLeft + 8f * u, H - SafeBottom - 26f * u, 320f * u, 20f * u);
            GUI.Label(new Rect(rect.x + u, rect.y + u, rect.width, rect.height), fpsText, s.StatLabel);
            GUI.Label(rect, fpsText, s.StatLabel);
        }

        /// <summary>Repaint: yazı hızla yükselir, yavaşlar ve söner.</summary>
        void DrawFloatingLabels()
        {
            if (floatingLabels.Count == 0) return;
            float now = Time.unscaledTime;
            Camera cam = mapCamera.Camera;
            for (int i = floatingLabels.Count - 1; i >= 0; i--)
            {
                FloatingLabel label = floatingLabels[i];
                float t = (now - label.Start) / IncomeLabelSeconds;
                if (t >= 1f)
                {
                    floatingLabels.RemoveAt(i);
                    continue;
                }
                Vector3 sp = cam.WorldToScreenPoint(label.World);
                if (sp.z <= 0f) continue;
                float rise = (1f - (1f - t) * (1f - t)) * 34f * u;
                float alpha = t < 0.12f ? t / 0.12f : 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                var rect = new Rect(sp.x - 90f * u, H - sp.y - 30f * u - rise, 180f * u, 24f * u);
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(rect.x + 1.5f * u, rect.y + 1.5f * u, rect.width, rect.height), label.Text, s.MapLabelBoldShadow);
                Color content = GUI.contentColor;
                GUI.contentColor = label.Color;
                GUI.Label(rect, label.Text, s.MapLabelBold);
                GUI.contentColor = content;
                GUI.color = previous;
            }
        }
    }
}
