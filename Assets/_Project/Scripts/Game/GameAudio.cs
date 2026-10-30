using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>Oyun sesi; <see cref="None"/> sessiz (örneğin sık tekrarlanan bildirimler).</summary>
    public enum Sfx { None = -1, Click, Coin, Horn, Notify, Warning, Success, Error }

    /// <summary>
    /// Kısa oyun sesleri: Resources/BusTycoon/Audio altındaki sentezlenmiş WAV'lar (telifsiz), tek AudioSource ile.
    /// Aynı ses art arda çalmaz (sese göre bekleme süresi); aynı anda gelen olay sesleri üst üste binmez (ilki çalar).
    /// Açık/kapalı ayarı "sfx_on" olarak saklanır, menüden değiştirilir.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        const string PrefKey = "sfx_on";
        const string ResourceFolder = "BusTycoon/Audio/";
        const float EventGap = 0.15f;

        //                                       Click   Coin    Horn    Notify  Warning Success Error
        static readonly string[] Names =     { "click", "coin", "horn", "notify", "warning", "success", "error" };
        static readonly float[] Volumes =    { 0.35f,   0.7f,   0.6f,   0.55f,   0.6f,     0.65f,     0.55f };
        static readonly float[] Cooldowns =  { 0.05f,   0.2f,   0.8f,   0.35f,   3f,       0.6f,      1.2f };

        public static GameAudio Instance { get; private set; }

        AudioSource source;
        AudioClip[] clips;
        readonly float[] lastPlayed = new float[Names.Length];
        float lastEvent = -10f;
        bool soundOn = true;

        /// <summary>Ses efektleri açık mı (ayar kalıcıdır).</summary>
        public static bool Enabled
        {
            get => Instance != null && Instance.soundOn;
            set
            {
                if (Instance == null) return;
                Instance.soundOn = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public void Init()
        {
            Instance = this;
            soundOn = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            clips = new AudioClip[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>(ResourceFolder + Names[i]);
                lastPlayed[i] = -10f;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(Sfx sfx)
        {
            if (Instance != null) Instance.PlayNow(sfx);
        }

        void PlayNow(Sfx sfx)
        {
            int i = (int)sfx;
            if (sfx == Sfx.None || !soundOn || clips == null || clips[i] == null) return;
            float now = Time.unscaledTime;
            if (now - lastPlayed[i] < Cooldowns[i]) return;
            if (sfx != Sfx.Click)
            {
                if (now - lastEvent < EventGap) return;
                lastEvent = now;
            }
            lastPlayed[i] = now;
            source.PlayOneShot(clips[i], Volumes[i]);
        }
    }
}
