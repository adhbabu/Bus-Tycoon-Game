#if BT_IAP
using System;
using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;
using UnityEngine.Purchasing;

namespace BusTycoon.Game
{
    /// <summary>
    /// App Store / Google Play satın almaları (Unity IAP 5, <see cref="PurchasingController"/>). Ürünler kodda tanımlıdır
    /// (<see cref="ShopCatalog"/>); kimlikler iki mağazada aynı açılır (store/DUKKAN.md). Satın alma önce oyuna verilir
    /// (ShopService diske yazar), sonra mağazada onaylanır: uygulama arada kapanırsa mağaza bir sonraki açılışta
    /// yeniden gönderir, ShopService aynı işlemi iki kez vermez. Bir kez alınan paketler açılışta mağazadan okunur.
    /// <para>Bu sınıf yalnız <c>BT_IAP</c> tanımlıyken derlenir (com.unity.purchasing paketi kuruluysa; BusTycoon.Game
    /// asmdef'teki versionDefines). Yalnız Android ve iOS'ta kullanılır.</para>
    /// </summary>
    public sealed class UnityIapShopBackend : ShopService.IBackend
    {
        PurchasingController controller;
        string storeName;
        bool ready;

        public bool Ready => ready;

        public event Action<string, string, bool> Purchased;
        public event Action<string, string> Failed;
        public event Action<bool, string> RestoreFinished;
        public event Action Changed;

        /// <summary>Mağazaya bağlanır ve ürünleri ister (sonuç olaylarla gelir).</summary>
        public async void Start()
        {
            try
            {
                storeName = UnityIAPServices.GetDefaultStore();
                controller = new PurchasingController(storeName);
                controller.OnPurchasePending += OnPurchasePending;
                controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                controller.OnPurchaseFailed += OnPurchaseFailed;
                controller.OnProductsFetched += OnProductsFetched;
                controller.OnProductsFetchFailed += OnProductsFetchFailed;
                controller.OnPurchasesFetched += OnPurchasesFetched;
                controller.OnStoreDisconnected += OnStoreDisconnected;
                await controller.Connect(storeName);

                var definitions = new List<ProductDefinition>();
                foreach (var product in ShopCatalog.Products)
                    definitions.Add(new ProductDefinition(product.Id, product.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
                controller.FetchProducts(definitions, storeName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dükkan] Mağazaya bağlanılamadı: " + e.Message);
            }
        }

        public string Price(string productId)
        {
            if (!ready || controller == null) return null;
            Product product = controller.GetProductById(productId, storeName);
            string text = product?.metadata?.localizedPriceString;
            return string.IsNullOrEmpty(text) ? null : text;
        }

        public void Purchase(string productId)
        {
            Product product = ready && controller != null ? controller.GetProductById(productId, storeName) : null;
            if (product == null || !product.availableToPurchase)
            {
                Failed?.Invoke(productId, "Ürün şu an mağazada yok. Biraz sonra yeniden dene.");
                return;
            }
            controller.PurchaseProduct(product);
        }

        public void Restore()
        {
            if (controller == null)
            {
                RestoreFinished?.Invoke(false, "Mağazaya bağlanılamadı.");
                return;
            }
            controller.RestoreTransactions((store, success, error) =>
            {
                if (success) controller.FetchPurchases(storeName);
                RestoreFinished?.Invoke(success, error);
            }, storeName);
        }

        void OnProductsFetched(string store, List<Product> products)
        {
            ready = true;
            Changed?.Invoke();
            controller.FetchPurchases(storeName); // bir kez alınan paketler ve onaylanmamış satın almalar
        }

        void OnProductsFetchFailed(string store, ProductFetchFailed failure)
        {
            Debug.LogWarning("[Dükkan] Ürünler alınamadı: " + failure?.FailureReason);
        }

        void OnPurchasePending(string store, PendingOrder order)
        {
            // Önce oyuna ver (ShopService diske yazar), sonra mağazada onayla
            foreach (CartItem item in order.CartOrdered.Items())
                Purchased?.Invoke(item.Product.definition.id, order.Info.TransactionID, false);
            controller.ConfirmPurchase(order);
        }

        void OnPurchaseConfirmed(string store, Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogWarning("[Dükkan] Satın alma onaylanamadı: " + failed.FailureReason + " " + failed.Details);
        }

        void OnPurchaseFailed(string store, FailedOrder order)
        {
            string productId = null;
            foreach (CartItem item in order.CartOrdered.Items()) productId = item.Product.definition.id;
            bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
            Failed?.Invoke(productId, cancelled ? null : "Satın alma tamamlanmadı (" + order.FailureReason + ").");
        }

        void OnPurchasesFetched(string store, Orders orders)
        {
            // Bir kez alınan paketler (yeni cihaz, yeniden kurulum): sessizce geri yüklenir
            foreach (ConfirmedOrder order in orders.ConfirmedOrders)
                foreach (CartItem item in order.CartOrdered.Items())
                {
                    ShopCatalog.Product product = ShopCatalog.Get(item.Product.definition.id);
                    if (product != null && !product.Consumable) Purchased?.Invoke(product.Id, order.Info.TransactionID, true);
                }
        }

        void OnStoreDisconnected(string store, StoreConnectionFailureDescription description)
        {
            ready = false;
            Changed?.Invoke();
            Debug.LogWarning("[Dükkan] Mağaza bağlantısı koptu: " + description?.message);
        }
    }
}
#endif
