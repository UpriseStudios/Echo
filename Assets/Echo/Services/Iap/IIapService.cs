using System;
using System.Collections.Generic;

namespace Echo.Services.Iap
{
    public interface IIapService
    {
        bool Owns(string productId);
        void Purchase(string productId, Action<bool> onComplete);
        void Restore(Action<IReadOnlyList<string>> onComplete);

        /// <summary>Store-formatted price, or null before the store has loaded. An addition to tech spec 15.</summary>
        string LocalizedPrice(string productId);

        /// <summary>
        /// Raised for every product the player is entitled to, including restored and deferred purchases
        /// that arrive outside a Purchase call. An addition to tech spec 15.
        /// </summary>
        event Action<string> Entitled;

        /// <summary>One line for the development status readout, e.g. "connected, 12 of 12 products".</summary>
        string Status { get; }
    }
}
