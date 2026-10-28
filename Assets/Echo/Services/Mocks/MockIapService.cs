using System;
using System.Collections.Generic;
using Echo.Services.Iap;
using UnityEngine;

namespace Echo.Services.Mocks
{
    public sealed class MockIapService : IIapService
    {
        readonly HashSet<string> _owned = new HashSet<string>();
        public bool SimulateFailure;

        public event Action<string> Entitled;

        public bool Owns(string productId) => _owned.Contains(productId);

        public void Purchase(string productId, Action<bool> onComplete)
        {
            bool ok = !SimulateFailure && MockPolicy.Grants;
            Debug.Log($"[MockIap] purchase '{productId}' -> {(ok ? "ok" : "failed")}");
            if (ok && _owned.Add(productId)) Entitled?.Invoke(productId);
            onComplete?.Invoke(ok);
        }

        public void Restore(Action<IReadOnlyList<string>> onComplete)
        {
            var owned = new List<string>(_owned);
            foreach (string id in owned) Entitled?.Invoke(id);
            onComplete?.Invoke(owned);
        }

        public string Status => MockPolicy.Grants ? "mock store (no real SDK running)" : "store unavailable (mock refuses on release builds)";

        public string LocalizedPrice(string productId) =>
            !MockPolicy.Grants ? null
            : productId == ProductIds.SeasonOfEcho ? "£4.99"
            : System.Array.IndexOf(ProductIds.Packs, productId) >= 0 || productId == ProductIds.PackChristmas ? "£1.99"
            : null;
    }
}
