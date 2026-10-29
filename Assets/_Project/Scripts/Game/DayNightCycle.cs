using BusTycoon.Core;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Gece-gündüz döngüsü: güneş doğudan doğup batıya batar (yükseklik, yön, renk ve şiddet), ortam ışığı değişir;
    /// gece harita kararır, şehirlerin ışıkları yanar, otobüsler farlarını açar (<see cref="WorldRenderer.SetDaylight"/>).
    /// Ekonomideki gün birkaç saniye sürdüğü için görsel döngünün kendi süresi vardır: 1x hızda ~90 sn; oyun
    /// hızlandıkça (karekökle) hızlanır, oyun duraklayınca durur. Duraklatma menüsünden kapatılabilir (öğle sabitlenir).
    /// Test ve ekran görüntüsü için adres seçeneği: ?saat=gece, ?saat=gunduz ya da ?saat=18 (saat).
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        const float SecondsPerCycleAt1x = 90f;
        const string PrefsKey = "bustycoon.daynight";

        static readonly Color NoonSun = new Color(1f, 0.97f, 0.92f);
        static readonly Color LowSun = new Color(1f, 0.62f, 0.38f);
        static readonly Color MoonLight = new Color(0.55f, 0.66f, 1f);
        static readonly Color DayAmbient = new Color(0.5f, 0.53f, 0.58f);
        static readonly Color NightAmbient = new Color(0.17f, 0.21f, 0.34f);
        static readonly Color DuskAmbient = new Color(0.55f, 0.42f, 0.38f);

        public static DayNightCycle Instance { get; private set; }

        public enum Phase { Sunrise, Day, Sunset, Night }

        GameSession session;
        Light sun;
        WorldRenderer world;
        float timeOfDay = 0.3f;     // 0 = gece yarısı, 0.25 = gün doğumu, 0.5 = öğle, 0.75 = gün batımı
        float? fixedTime;

        public bool Enabled { get; private set; } = true;
        /// <summary>Günün saati (0..1); kapalıyken öğle.</summary>
        public float TimeOfDay01 => fixedTime ?? (Enabled ? timeOfDay : 0.5f);
        /// <summary>0 gündüz … 1 gece yarısı.</summary>
        public float Night01 { get; private set; }

        public Phase CurrentPhase
        {
            get
            {
                float t = TimeOfDay01;
                if (t >= 0.21f && t < 0.3f) return Phase.Sunrise;
                if (t >= 0.3f && t < 0.7f) return Phase.Day;
                if (t >= 0.7f && t < 0.79f) return Phase.Sunset;
                return Phase.Night;
            }
        }

        public void Init(GameSession gameSession, Light sunLight, WorldRenderer worldRenderer)
        {
            Instance = this;
            session = gameSession;
            sun = sunLight;
            world = worldRenderer;
            Enabled = PlayerPrefs.GetInt(PrefsKey, 1) == 1;
            fixedTime = ParseFixedTime(LaunchOptions.Get("saat"));
            Apply();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            PlayerPrefs.SetInt(PrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Saati sabitler (vitrin: öğle); null döngüyü serbest bırakır.</summary>
        public void ForceTime(float? timeOfDay01) => fixedTime = timeOfDay01;

        internal static float? ParseFixedTime(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            switch (value.Trim().ToLowerInvariant())
            {
                case "gece": case "night": return 0.02f;
                case "gunduz": case "gündüz": case "day": return 0.5f;
                case "aksam": case "akşam": case "sunset": return 0.74f;
                case "sabah": case "sunrise": return 0.26f;
            }
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hours)
                ? Mathf.Repeat(hours / 24f, 1f)
                : (float?)null;
        }

        void Update()
        {
            int speed = session != null && session.IsRunning && session.Clock != null ? session.Clock.Speed : 0;
            if (speed > 0)
                timeOfDay = Mathf.Repeat(timeOfDay + Time.unscaledDeltaTime * Mathf.Sqrt(speed) / SecondsPerCycleAt1x, 1f);
            Apply();
        }

        void Apply()
        {
            float t = TimeOfDay01;
            float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);         // −1 gece yarısı … 1 öğle
            float day = Smooth(-0.12f, 0.2f, elevation);                         // 0 gece … 1 gündüz
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(elevation) / 0.28f);       // gün doğumu / batımında 1
            Night01 = 1f - day;

            if (sun != null)
            {
                // Güneş: doğudan (sabah) batıya (akşam); öğle konumu eski sabit ışıkla aynı (62°, −35°).
                float arc = Mathf.Clamp01((t - 0.25f) / 0.5f);
                Quaternion sunRotation = Quaternion.Euler(Mathf.Lerp(6f, 62f, Mathf.Clamp01(elevation)), Mathf.Lerp(-110f, 40f, arc), 0f);
                Quaternion moonRotation = Quaternion.Euler(48f, 25f, 0f);
                sun.transform.rotation = Quaternion.Slerp(moonRotation, sunRotation, day);
                Color sunColor = Color.Lerp(LowSun, NoonSun, Mathf.Clamp01(elevation / 0.45f));
                sun.color = Color.Lerp(MoonLight, sunColor, day);
                sun.intensity = Mathf.Lerp(0.3f, 1.15f, day);
                sun.shadowStrength = Mathf.Lerp(0.3f, 0.6f, day);
            }

            Color ambient = Color.Lerp(NightAmbient, DayAmbient, day);
            RenderSettings.ambientLight = Color.Lerp(ambient, DuskAmbient, dusk * 0.35f);
            if (world != null) world.SetDaylight(Night01, dusk * (1f - Night01 * 0.5f));
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
