using System;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Reklamlar: <b>ödüllü</b> (izlenince bekleme süresi biter: hat kurulumu, araç teslimi) ve <b>geçiş</b> (seviye atlama
    /// gibi olaylardan sonra, en sık 3 dakikada bir). Asıl iş bir sağlayıcıda: Android mağaza derlemesinde AdMob; test için
    /// <c>?reklam=test</c> ile 3 saniyelik sahte reklam (<see cref="TestAdProvider"/>). Sağlayıcı yoksa (web) reklam yok:
    /// "Reklam izle" düğmeleri gizlenir, oyuncu bekler.
    /// </summary>
    public static class AdService
    {
        public interface IProvider
        {
            bool RewardedReady { get; }
            /// <summary>Ödüllü reklamı gösterir; bitince <paramref name="done"/>(ödül kazanıldı mı).</summary>
            void ShowRewarded(Action<bool> done);
            bool InterstitialReady { get; }
            void ShowInterstitial(Action done);
        }

        /// <summary>Geçiş reklamları arasında en az bu kadar süre (gerçek saniye).</summary>
        public const float InterstitialCooldownSeconds = 180f;

        static IProvider provider;
        static float lastInterstitialAt = float.NegativeInfinity;

        public static void SetProvider(IProvider value) => provider = value;
        public static IProvider Provider => provider;

        /// <summary>Şu an bir reklam ekranda.</summary>
        public static bool Showing { get; private set; }

        /// <summary>Bu cihazda reklam var mı ("Reklam izle" düğmeleri gösterilir; web'de yok).</summary>
        public static bool Enabled => provider != null;

        /// <summary>Ödüllü reklam şu an gösterilebilir mi (yüklenmiş ve başka reklam yok).</summary>
        public static bool RewardedAvailable => provider != null && !Showing && provider.RewardedReady;

        /// <summary>Ödüllü reklam; izlenirse <paramref name="onRewarded"/> çalışır (yarıda kapatılırsa çalışmaz).</summary>
        public static bool ShowRewarded(Action onRewarded)
        {
            if (!RewardedAvailable) return false;
            Showing = true;
            provider.ShowRewarded(rewarded =>
            {
                Showing = false;
                if (rewarded) onRewarded?.Invoke();
            });
            return true;
        }

        /// <summary>Olay sonrası geçiş reklamı: hazırsa ve son reklamdan bu yana 3 dakika geçtiyse gösterilir.</summary>
        public static bool TryShowInterstitial()
        {
            if (provider == null || Showing || !provider.InterstitialReady) return false;
            float now = Time.unscaledTime;
            if (now - lastInterstitialAt < InterstitialCooldownSeconds) return false;
            lastInterstitialAt = now;
            Showing = true;
            provider.ShowInterstitial(() => Showing = false);
            return true;
        }
    }

    /// <summary>Sahte reklam (geliştirme ve test): 3 saniye sayar, sonra ödül verir. Ekrandaki örtüyü GameUI çizer.</summary>
    public sealed class TestAdProvider : AdService.IProvider
    {
        public const float Seconds = 3f;
        Action<bool> pendingRewarded;
        Action pendingInterstitial;

        /// <summary>Gösterilen reklamın kalan süresi (0: reklam yok).</summary>
        public float Remaining { get; private set; }
        public bool IsRewarded => pendingRewarded != null;

        public bool RewardedReady => true;
        public bool InterstitialReady => true;

        public void ShowRewarded(Action<bool> done)
        {
            pendingRewarded = done;
            Remaining = Seconds;
        }

        public void ShowInterstitial(Action done)
        {
            pendingInterstitial = done;
            Remaining = Seconds;
        }

        /// <summary>Her kare (GameUI): süre dolunca reklam kapanır.</summary>
        public void Tick(float deltaTime)
        {
            if (Remaining <= 0f) return;
            Remaining -= deltaTime;
            if (Remaining > 0f) return;
            Remaining = 0f;
            Action<bool> rewarded = pendingRewarded;
            Action interstitial = pendingInterstitial;
            pendingRewarded = null;
            pendingInterstitial = null;
            rewarded?.Invoke(true);
            interstitial?.Invoke();
        }
    }
}
