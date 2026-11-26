using System;
using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Gerçek paralı küçük dükkan. Asıl iş bir arka uçta: App Store / Google Play derlemesinde Unity IAP
    /// (<see cref="UnityIapShopBackend"/>), test için <c>?dukkan=test</c> ile anında tamamlanan sahte satın alma
    /// (<see cref="TestShopBackend"/>). Arka uç yoksa (web) dükkan sayfası açılır ama satın alma düğmeleri kapalıdır.
    /// <para>Sahiplik cihazda (PlayerPrefs) tutulur, kayıt dosyasında değil: bir kez alınan paketler bütün kayıtlarda
    /// geçerlidir ve mağazadan geri yüklenir. Tekrar alınabilen ürünler (sermaye, lisans hakkı) önce "bekleyen" olarak
    /// yazılır, oyun açıkken (yarış dışında) kayda işlenir; aynı işlem iki kez gelirse bir kez verilir.</para>
    /// </summary>
    public static class ShopService
    {
        public interface IBackend
        {
            /// <summary>Ürünler mağazadan geldi (fiyatlar hazır).</summary>
            bool Ready { get; }
            /// <summary>Mağazanın yerel fiyat yazısı (bilinmiyorsa null).</summary>
            string Price(string productId);
            void Purchase(string productId);
            void Restore();
            /// <summary>Ürün verilmeli: (ürün, işlem kimliği, geri yükleme mi). Arka uç satın almayı bundan sonra onaylar.</summary>
            event Action<string, string, bool> Purchased;
            /// <summary>Satın alma olmadı: (ürün, açıklama; vazgeçmede null).</summary>
            event Action<string, string> Failed;
            event Action<bool, string> RestoreFinished;
            event Action Changed;
        }

        const string OwnedKey = "dukkan_sahip";
        const string PendingCapitalKey = "dukkan_bekleyen_sermaye";
        const string PendingLicenseKey = "dukkan_bekleyen_lisans";
        const string HandledKey = "dukkan_islemler";
        const int HandledLimit = 40;

        static IBackend backend;
        static bool loaded;
        static readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<string> handled = new List<string>();
        static int pendingCapital, pendingLicenses;
        static Action<bool, string> restoreDone;

        /// <summary>Dükkanın durumu değişti (fiyat, sahiplik, satın alma sürüyor…): sayfa yeniden çizilir.</summary>
        public static event Action Changed;
        /// <summary>Yeni satın alınan ürün verildi (geri yüklemede değil): bildirim için.</summary>
        public static event Action<string> Granted;
        /// <summary>Satın alma olmadı (vazgeçmede açıklama null).</summary>
        public static event Action<string, string> PurchaseFailed;

        /// <summary>Bu cihazda satın alma yapılabilir (mağaza derlemesi ya da test dükkanı).</summary>
        public static bool Available => backend != null;
        public static bool Ready => backend != null && backend.Ready;
        /// <summary>Sürmekte olan satın almanın ürünü (yoksa null).</summary>
        public static string BusyProduct { get; private set; }
        public static bool Restoring => restoreDone != null;
        public static int PendingCapital { get { Load(); return pendingCapital; } }
        public static int PendingLicenses { get { Load(); return pendingLicenses; } }
        /// <summary>Durumun özeti (sayfanın yeniden çizilip çizilmeyeceği için).</summary>
        public static int Version { get; private set; }

        public static void SetBackend(IBackend value)
        {
            Load();
            if (backend != null)
            {
                backend.Purchased -= OnPurchased;
                backend.Failed -= OnFailed;
                backend.RestoreFinished -= OnRestoreFinished;
                backend.Changed -= OnBackendChanged;
            }
            backend = value;
            BusyProduct = null;
            if (backend != null)
            {
                backend.Purchased += OnPurchased;
                backend.Failed += OnFailed;
                backend.RestoreFinished += OnRestoreFinished;
                backend.Changed += OnBackendChanged;
            }
            Bump();
        }

        /// <summary>Kayıtlı sahiplikleri yükler (bir kez): bir kez alınan paketler <see cref="Entitlements"/>'a işlenir.</summary>
        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            foreach (string id in Split(PlayerPrefs.GetString(OwnedKey, string.Empty)))
                if (owned.Add(id)) Entitlements.Grant(id);
            handled.AddRange(Split(PlayerPrefs.GetString(HandledKey, string.Empty)));
            pendingCapital = Math.Max(0, PlayerPrefs.GetInt(PendingCapitalKey, 0));
            pendingLicenses = Math.Max(0, PlayerPrefs.GetInt(PendingLicenseKey, 0));
        }

        public static bool Owns(string productId)
        {
            Load();
            return !string.IsNullOrEmpty(productId) && owned.Contains(productId);
        }

        /// <summary>Gösterilecek fiyat: mağazanınki, yoksa katalogdaki.</summary>
        public static string PriceText(string productId) =>
            backend?.Price(productId) ?? ShopCatalog.Get(productId)?.FallbackPrice ?? string.Empty;

        public static bool Purchase(string productId)
        {
            ShopCatalog.Product product = ShopCatalog.Get(productId);
            if (backend == null || product == null || BusyProduct != null) return false;
            if (!product.Consumable && Owns(productId)) return false;
            BusyProduct = productId;
            Bump();
            backend.Purchase(productId);
            return true;
        }

        /// <summary>Daha önce alınan paketleri mağazadan geri yükler (Apple'ın istediği düğme).</summary>
        public static bool Restore(Action<bool, string> done)
        {
            if (backend == null || restoreDone != null) return false;
            restoreDone = done ?? ((_, __) => { });
            Bump();
            backend.Restore();
            return true;
        }

        /// <summary>Bekleyen sermaye paketlerini alır (oyun açıkken kasaya eklenir).</summary>
        public static int TakePendingCapital()
        {
            Load();
            int count = pendingCapital;
            if (count == 0) return 0;
            pendingCapital = 0;
            PlayerPrefs.SetInt(PendingCapitalKey, 0);
            PlayerPrefs.Save();
            Bump();
            return count;
        }

        /// <summary>Bekleyen şehir lisansı haklarını alır (oyun açıkken kayda eklenir).</summary>
        public static int TakePendingLicenses()
        {
            Load();
            int count = pendingLicenses;
            if (count == 0) return 0;
            pendingLicenses = 0;
            PlayerPrefs.SetInt(PendingLicenseKey, 0);
            PlayerPrefs.Save();
            Bump();
            return count;
        }

        static void OnPurchased(string productId, string transactionId, bool restored)
        {
            if (productId == BusyProduct) BusyProduct = null;
            ShopCatalog.Product product = ShopCatalog.Get(productId);
            if (product == null)
            {
                Debug.LogWarning("[Dükkan] Bilinmeyen ürün: " + productId);
                Bump();
                return;
            }
            bool fresh;
            if (!product.Consumable)
            {
                fresh = owned.Add(productId);
                if (fresh)
                {
                    PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
                    Entitlements.Grant(productId);
                }
            }
            else
            {
                // Aynı işlem (ör. onaylanmadan kapanan uygulamada yeniden gelen) bir kez verilir
                fresh = string.IsNullOrEmpty(transactionId) || !handled.Contains(transactionId);
                if (fresh)
                {
                    if (productId == ShopCatalog.Capital) PlayerPrefs.SetInt(PendingCapitalKey, ++pendingCapital);
                    else if (productId == ShopCatalog.CityLicense) PlayerPrefs.SetInt(PendingLicenseKey, ++pendingLicenses);
                    if (!string.IsNullOrEmpty(transactionId))
                    {
                        handled.Add(transactionId);
                        if (handled.Count > HandledLimit) handled.RemoveAt(0);
                        PlayerPrefs.SetString(HandledKey, string.Join(",", handled));
                    }
                }
            }
            PlayerPrefs.Save(); // mağaza onayından önce diske: uygulama kapansa da ürün kaybolmaz
            Bump();
            if (fresh && !restored) Granted?.Invoke(productId);
        }

        static void OnFailed(string productId, string message)
        {
            if (productId == null || productId == BusyProduct) BusyProduct = null;
            Bump();
            PurchaseFailed?.Invoke(productId, message);
        }

        static void OnRestoreFinished(bool success, string message)
        {
            Action<bool, string> done = restoreDone;
            restoreDone = null;
            Bump();
            done?.Invoke(success, message);
        }

        static void OnBackendChanged() => Bump();

        static void Bump()
        {
            Version++;
            Changed?.Invoke();
        }

        static IEnumerable<string> Split(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            foreach (string part in text.Split(','))
                if (!string.IsNullOrEmpty(part)) yield return part;
        }
    }

    /// <summary>Sahte dükkan (geliştirme ve test, <c>?dukkan=test</c>): satın alma anında tamamlanır, ödeme alınmaz.</summary>
    public sealed class TestShopBackend : ShopService.IBackend
    {
        int serial;

        public bool Ready => true;
        public string Price(string productId) => ShopCatalog.Get(productId)?.FallbackPrice;

        public event Action<string, string, bool> Purchased;
        public event Action<string, string> Failed;
        public event Action<bool, string> RestoreFinished;
        public event Action Changed;

        public void Purchase(string productId)
        {
            if (ShopCatalog.Get(productId) == null)
            {
                Failed?.Invoke(productId, "Ürün bulunamadı.");
                return;
            }
            Purchased?.Invoke(productId, "test-" + (++serial) + "-" + DateTime.UtcNow.Ticks, false);
        }

        public void Restore() => RestoreFinished?.Invoke(true, null);

        /// <summary>Kullanılmayan olayların derleyici uyarısı olmasın.</summary>
        internal void Touch() => Changed?.Invoke();
    }
}
