using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    public enum QualityLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Görüntü kalitesi: cihaza göre otomatik (telefon → Orta, tablet ve bilgisayar → Yüksek, ekran kartı olmadan
    /// yazılımla çizen tarayıcı → Düşük) ya da menüden seçilir (PlayerPrefs; tarayıcıda da saklanır). Etkiledikleri:
    /// <list type="bullet">
    /// <item>Harita gölgelendiricisi (BusTycoon/MapSurface): Orta'da canlı deniz ve yakın arazi dokusu, Yüksek'te bulut gölgeleri</item>
    /// <item>Güneş gölgeleri: Yüksek yumuşak, Orta keskin, Düşük kapalı</item>
    /// <item>Ufuk pusu (sis), anizotropik süzme, ekran kenarı karartması</item>
    /// <item>Web: Düşük'te tuval 1 CSS pikseli çözünürlükte çizilir (BusTycoonQuality.jslib) — en akıcı</item>
    /// </list>
    /// Kenar yumuşatma (4x MSAA) derlemede açılır (StoreBuild): WebGL'de çizim bağlamıyla birlikte sabitlenir.
    /// <c>?kalite=dusuk|orta|yuksek</c> açılış seçeneği kaydı değiştirmeden zorlar (testler, ekran görüntüleri).
    /// </summary>
    public static class GraphicsQuality
    {
        const string PrefKey = "gfx_quality";
        const int Automatic = -1;

        static readonly int DetailId = Shader.PropertyToID("_BT_Detail");
        static readonly int WavesId = Shader.PropertyToID("_BT_Waves");
        static readonly int CloudsId = Shader.PropertyToID("_BT_Clouds");

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BusTycoon_ApplyQuality(int level, int stored);
#endif

        static Light sun;
        static QualityLevel? forced;

        /// <summary>Şu an uygulanan seviye.</summary>
        public static QualityLevel Current { get; private set; } = QualityLevel.High;
        /// <summary>Oyuncu bir seviye seçmediyse (cihaza göre otomatik).</summary>
        public static bool IsAuto => !forced.HasValue && Stored == Automatic;
        /// <summary>Ufuk pusu, ekran kenarı karartması gibi ucuz atmosfer efektleri (Düşük'te kapalı).</summary>
        public static bool Atmosphere => Current != QualityLevel.Low;
        public static event Action Changed;

        static int Stored
        {
            get
            {
                int value = PlayerPrefs.GetInt(PrefKey, Automatic);
                return value >= 0 && value <= 2 ? value : Automatic;
            }
        }

        /// <summary>Açılışta bir kez: güneş ışığı (gölgeleri seviyeye göre) ve açılış seçeneği.</summary>
        public static void Init(Light sunLight)
        {
            sun = sunLight;
            forced = Parse(LaunchOptions.Get("kalite") ?? LaunchOptions.Get("quality"));
            Apply();
        }

        /// <summary>Menüden seçim; null = otomatik. Açılış seçeneğini de geçersiz kılar.</summary>
        public static void Select(QualityLevel? level)
        {
            forced = null;
            PlayerPrefs.SetInt(PrefKey, level.HasValue ? (int)level.Value : Automatic);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Cihaza göre önerilen seviye.</summary>
        public static QualityLevel Detect()
        {
            string device = (SystemInfo.graphicsDeviceName ?? string.Empty).ToLowerInvariant();
            if (device.Contains("swiftshader") || device.Contains("llvmpipe") || device.Contains("software") ||
                device.Contains("basic render"))
                return QualityLevel.Low;
            return LooksLikePhone() ? QualityLevel.Medium : QualityLevel.High;
        }

        /// <summary>Dokunmatik ve kısa kenarı 500 noktadan kısa: telefon (GameUI ile aynı ölçüt).</summary>
        static bool LooksLikePhone()
        {
            bool mobile = Application.isMobilePlatform || TouchInput.IsTouchPrimaryBrowser;
            if (!mobile) return false;
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            return shortSide / GameUI.PixelsPerPoint() < 500f;
        }

        internal static QualityLevel? Parse(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dusuk": case "düşük": case "low": case "0": return QualityLevel.Low;
                case "orta": case "medium": case "1": return QualityLevel.Medium;
                case "yuksek": case "yüksek": case "high": case "2": return QualityLevel.High;
                default: return null;
            }
        }

        public static string Label(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return "Düşük";
                case QualityLevel.Medium: return "Orta";
                default: return "Yüksek";
            }
        }

        static void Apply()
        {
            int stored = Stored;
            QualityLevel level = forced ?? (stored >= 0 ? (QualityLevel)stored : Detect());
            Current = level;

            Shader.SetGlobalFloat(DetailId, level >= QualityLevel.Medium ? 1f : 0f);
            Shader.SetGlobalFloat(WavesId, level == QualityLevel.High ? 1f : level == QualityLevel.Medium ? 0.8f : 0f);
            Shader.SetGlobalFloat(CloudsId, level == QualityLevel.High ? 1f : 0f);

            QualitySettings.anisotropicFiltering = level == QualityLevel.Low ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
            QualitySettings.shadows = level == QualityLevel.High ? ShadowQuality.All
                : level == QualityLevel.Medium ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            QualitySettings.shadowResolution = level == QualityLevel.High ? ShadowResolution.High : ShadowResolution.Medium;
            if (sun != null)
                sun.shadows = level == QualityLevel.High ? LightShadows.Soft
                    : level == QualityLevel.Medium ? LightShadows.Hard : LightShadows.None;

#if UNITY_WEBGL && !UNITY_EDITOR
            // Açılış seçeneğiyle zorlanan seviye tarayıcıya kaydedilmez (-2)
            BusTycoon_ApplyQuality((int)level, forced.HasValue ? -2 : stored);
#endif
            Changed?.Invoke();
        }
    }
}
