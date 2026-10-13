using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Echo.Services.Iap
{
    /// <summary>
    /// Unity IAP 5 with the three non-consumables from tech spec 15. Requires UnityServices initialised first.
    /// Flow: connect, fetch products, fetch existing purchases; a purchase arrives as a pending order, which is
    /// granted and then confirmed. Purchases that finish outside a Purchase call (deferred, restored, or
    /// interrupted by an app kill) reach the game through <see cref="Entitled"/>.
    /// </summary>
    public sealed class UnityIapService : IIapService
    {
        readonly HashSet<string> _owned = new HashSet<string>();
        readonly StoreController _store;
        bool _ready;
        Action<bool> _pendingPurchase;
        string _pendingProduct;

        public string Status { get; private set; } = "connecting";

        public event Action<string> Entitled;

        public UnityIapService()
        {
            _store = UnityIAPServices.StoreController();
            _store.OnStoreDisconnected += failure => Note($"store disconnected: {failure.Message}");
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += failure => Note($"product fetch failed ({failure.FailedFetchProducts?.Count ?? 0} products): {failure.FailureReason}");
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += failure => Note($"purchase fetch failed: {failure.Message}");
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            Connect();
        }

        async void Connect()
        {
            try
            {
                await _store.Connect();
            }
            catch (Exception e)
            {
                Note($"connect failed: {e.Message}");
                return;
            }
            Status = "connected, fetching products";
            var products = new List<ProductDefinition>();
            foreach (string id in ProductIds.All) products.Add(new ProductDefinition(id, ProductType.NonConsumable));
            _store.FetchProducts(products);
        }

        public bool Owns(string productId) => _owned.Contains(productId);

        void Note(string status)
        {
            Status = status;
            Debug.LogWarning("[IAP] " + status);
        }

        public string LocalizedPrice(string productId) =>
            _ready ? _store.GetProductById(productId)?.metadata?.localizedPriceString : null;

        public void Purchase(string productId, Action<bool> onComplete)
        {
            Product product = _ready ? _store.GetProductById(productId) : null;
            if (product == null || _pendingPurchase != null)
            {
                onComplete?.Invoke(false);
                return;
            }
            _pendingPurchase = onComplete;
            _pendingProduct = productId;
            _store.PurchaseProduct(product);
        }

        public void Restore(Action<IReadOnlyList<string>> onComplete)
        {
            if (!_ready)
            {
                Debug.LogWarning("[IAP] restore failed: store not initialised");
                onComplete?.Invoke(new List<string>(_owned));
                return;
            }
            _store.RestoreTransactions((ok, message) =>
            {
                if (!ok) Debug.LogWarning($"[IAP] restore failed: {message}");
                // Restored products are delivered as orders; re-fetching catches any the restore surfaced.
                _store.FetchPurchases();
                onComplete?.Invoke(new List<string>(_owned));
            });
        }

        void OnProductsFetched(List<Product> products)
        {
            _ready = true;
            // Products missing here are not set up (or not active) in the store console under these exact ids.
            var missing = new List<string>();
            foreach (string id in ProductIds.All)
                if (products.Find(p => p.definition.id == id && p.availableToPurchase) == null) missing.Add(id);
            Status = $"connected, {ProductIds.All.Length - missing.Count} of {ProductIds.All.Length} products"
                     + (missing.Count > 0 ? " (missing: " + string.Join(", ", missing) + ")" : "");
            Debug.Log("[IAP] " + Status);
            _store.FetchPurchases();
        }

        void OnPurchasesFetched(Orders orders)
        {
            foreach (ConfirmedOrder order in orders.ConfirmedOrders) GrantAll(order);
            // Paid for but never confirmed (e.g. the app was killed mid-purchase): grant, then confirm.
            foreach (PendingOrder order in orders.PendingOrders)
            {
                GrantAll(order);
                _store.ConfirmPurchase(order);
            }
        }

        void OnPurchasePending(PendingOrder order)
        {
            bool wasPending = false;
            foreach (CartItem item in order.CartOrdered.Items())
            {
                string id = item.Product?.definition?.id;
                if (id == null) continue;
                Grant(id);
                if (id == _pendingProduct) wasPending = true;
            }
            _store.ConfirmPurchase(order);
            if (wasPending) CompletePending(true);
        }

        void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogWarning($"[IAP] confirmation failed: {failed.FailureReason} {failed.Details}");
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            Debug.LogWarning($"[IAP] purchase failed: {order.FailureReason} {order.Details}");
            CompletePending(false);
        }

        // Ask to Buy and similar: nothing is owned yet. If approved later it arrives as a pending order.
        void OnPurchaseDeferred(DeferredOrder order) => CompletePending(false);

        void GrantAll(Order order)
        {
            foreach (CartItem item in order.CartOrdered.Items())
            {
                string id = item.Product?.definition?.id;
                if (id != null) Grant(id);
            }
        }

        void Grant(string id)
        {
            _owned.Add(id);
            Entitled?.Invoke(id);
        }

        void CompletePending(bool ok)
        {
            var callback = _pendingPurchase;
            _pendingPurchase = null;
            _pendingProduct = null;
            callback?.Invoke(ok);
        }
    }
}
