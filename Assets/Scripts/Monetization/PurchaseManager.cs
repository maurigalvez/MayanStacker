using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_PURCHASING_IAP
using UnityEngine.Purchasing;
#endif

/// <summary>
/// Google Play billing for the game's one product: the non-consumable "Remove Ads".
///
/// Lifecycle, using Unity IAP 5's two-step flow:
///   1. On boot, connect to the store, fetch the product (for its localized price) and fetch
///      existing purchases. That fetch is the restore: a reinstall or a new device gets its
///      purchase back here without the player doing anything, and a purchase Play no longer
///      reports (a refund) is revoked.
///   2. <see cref="BuyRemoveAds"/> opens Play's purchase sheet. The result arrives as a
///      PendingOrder; the entitlement is granted and saved to disk *before* ConfirmPurchase,
///      so a crash in between means Play re-delivers the order instead of the player losing it.
///   3. <see cref="RestorePurchases"/> is the Settings button for players who expect one. It
///      re-queries Play, the same as step 1.
///
/// The UnityEngine.Purchasing types only exist once the In-App Purchasing package is
/// installed, so everything that touches them sits behind UNITY_PURCHASING_IAP. That symbol
/// is kept in step with the package by UnityPurchasingDefine in the Editor folder; until the
/// package is present this compiles to a stub that reports the store as unavailable.
/// </summary>
public class PurchaseManager : MonoBehaviour
{
    public enum Outcome
    {
        Purchased,
        Restored,
        NothingToRestore,
        Pending,
        Cancelled,
        Failed,
        StoreUnavailable,
    }

    public static PurchaseManager Instance { get; private set; }

    /// <summary>Connection, product or ownership state changed — repaint any UI showing it.</summary>
    public event Action StateChanged;

    /// <summary>The result of something the player started (buy or restore).</summary>
    public event Action<Outcome> PlayerActionFinished;

    /// <summary>True once the store is connected and the Remove Ads product can be bought.</summary>
    public bool CanPurchase { get; private set; }

    /// <summary>Play's localized price, e.g. "$2.99" or "R$ 9,90". Empty until fetched.</summary>
    public string RemoveAdsPrice { get; private set; } = string.Empty;

    /// <summary>True between the player tapping Buy/Restore and the result arriving.</summary>
    public bool Busy { get; private set; }

    // The placement that opened the offer for the purchase in flight, for analytics.
    private string purchaseSource;

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("PurchaseManager");
        go.AddComponent<PurchaseManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DependencyRegistry.Register<PurchaseManager>(this);

        InitializeStore();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        DependencyRegistry.Unregister<PurchaseManager>(this);
    }

    #endregion

    private void Finish(Outcome outcome)
    {
        Busy = false;
        string source = purchaseSource;
        purchaseSource = null;
        GameAnalytics.Purchase(RemoveAds.ProductId, outcome.ToString(), source);
        PlayerActionFinished?.Invoke(outcome);
        StateChanged?.Invoke();
    }

#if UNITY_PURCHASING_IAP

    private StoreController store;
    private bool connected;
    private bool connecting;
    private bool restoreRequested;

    private static List<ProductDefinition> Catalog => new List<ProductDefinition>
    {
        new ProductDefinition(RemoveAds.ProductId, ProductType.NonConsumable),
    };

    private void InitializeStore()
    {
        store = UnityIAPServices.StoreController();

        // Every event is subscribed before Connect: an order left unconfirmed by a previous
        // session can be delivered the moment the connection opens.
        store.OnStoreConnected += OnStoreConnected;
        store.OnStoreDisconnected += OnStoreDisconnected;
        store.OnProductsFetched += OnProductsFetched;
        store.OnProductsFetchFailed += OnProductsFetchFailed;
        store.OnPurchasesFetched += OnPurchasesFetched;
        store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        store.OnPurchasePending += OnPurchasePending;
        store.OnPurchaseConfirmed += OnPurchaseConfirmed;
        store.OnPurchaseFailed += OnPurchaseFailed;
        store.OnPurchaseDeferred += OnPurchaseDeferred;

        Connect();
    }

    /// <summary>
    /// Retries the connection if it dropped or never opened — Settings calls this when it
    /// opens, so a player who launched offline can still buy once they're back online.
    /// </summary>
    public void EnsureConnected()
    {
        if (!connected) Connect();
    }

    private async void Connect()
    {
        if (connecting || connected) return;
        connecting = true;
        try
        {
            await store.Connect();
        }
        catch (Exception e)
        {
            // Connect reports ordinary failures through OnStoreDisconnected; this only
            // catches the unexpected, so it can't take the game down with it.
            Debug.LogWarning($"[PurchaseManager] Store connect threw: {e.Message}");
        }
        finally
        {
            connecting = false;
        }
    }

    private void OnStoreConnected()
    {
        connected = true;
        Debug.Log("[PurchaseManager] Store connected.");
        store.FetchProducts(Catalog);
        store.FetchPurchases();
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        connected = false;
        CanPurchase = false;
        Debug.LogWarning($"[PurchaseManager] Store unavailable: {failure.Message}");

        if (Busy) Finish(Outcome.StoreUnavailable);
        else StateChanged?.Invoke();
    }

    private void OnProductsFetched(List<Product> products)
    {
        Product product = store.GetProductById(RemoveAds.ProductId);
        CanPurchase = product != null && product.availableToPurchase;
        RemoveAdsPrice = product != null && product.metadata != null
            ? product.metadata.localizedPriceString
            : string.Empty;

        if (!CanPurchase)
        {
            Debug.LogWarning($"[PurchaseManager] '{RemoveAds.ProductId}' isn't available from the store. " +
                             "Check it exists and is active in Play Console, and that this build came from a Play track.");
        }
        StateChanged?.Invoke();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        CanPurchase = false;
        Debug.LogWarning($"[PurchaseManager] Product fetch failed: {failure.FailureReason}");
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Play's authoritative list of what this account owns. Pending orders in it are also
    /// re-delivered through OnPurchasePending, which grants and confirms them.
    /// </summary>
    private void OnPurchasesFetched(Orders orders)
    {
        bool owned = Contains(orders.ConfirmedOrders) || Contains(orders.PendingOrders);

        if (owned) RemoveAds.Grant();
        else RemoveAds.Revoke();

        if (restoreRequested)
        {
            restoreRequested = false;
            Finish(owned ? Outcome.Restored : Outcome.NothingToRestore);
        }
        else
        {
            StateChanged?.Invoke();
        }
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        // Keep whatever is cached: a failed query says nothing about ownership.
        Debug.LogWarning($"[PurchaseManager] Purchase fetch failed: {failure.FailureReason} — {failure.Message}");

        if (restoreRequested)
        {
            restoreRequested = false;
            Finish(Outcome.StoreUnavailable);
        }
    }

    /// <param name="source">The placement that opened the offer (RemoveAdsOffer.Source*).</param>
    public void BuyRemoveAds(string source = null)
    {
        if (Busy) return;
        purchaseSource = source;
        if (RemoveAds.Owned) { StateChanged?.Invoke(); return; }

        Product product = connected ? store.GetProductById(RemoveAds.ProductId) : null;
        if (product == null || !product.availableToPurchase)
        {
            EnsureConnected();
            Finish(Outcome.StoreUnavailable);
            return;
        }

        Busy = true;
        GameAnalytics.Purchase(RemoveAds.ProductId, "started", source);
        StateChanged?.Invoke();
        store.PurchaseProduct(product);
    }

    public void RestorePurchases()
    {
        if (Busy) return;
        if (!connected)
        {
            EnsureConnected();
            Finish(Outcome.StoreUnavailable);
            return;
        }

        Busy = true;
        restoreRequested = true;
        GameAnalytics.Purchase(RemoveAds.ProductId, "restore_started");
        StateChanged?.Invoke();

        // Not RestoreTransactions: on Google Play that is a no-op that reports success
        // without asking Play anything. FetchPurchases is the real query, and its answer
        // lands in OnPurchasesFetched / OnPurchasesFetchFailed.
        store.FetchPurchases();
    }

    private void OnPurchasePending(PendingOrder order)
    {
        if (!Contains(order))
        {
            // Nothing else is sold. Leave it unconfirmed rather than acknowledge something
            // we never granted — Play refunds unacknowledged purchases after three days.
            Debug.LogWarning($"[PurchaseManager] Pending order for an unknown product (transaction {order.Info.TransactionID}) — not confirming.");
            return;
        }

        // Grant is idempotent, which covers the same order arriving twice (a restart before
        // confirmation, or a restore of something already owned).
        RemoveAds.Grant();
        store.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        switch (order)
        {
            case ConfirmedOrder _:
                Debug.Log("[PurchaseManager] Remove Ads purchase confirmed.");
                if (Busy && !restoreRequested) Finish(Outcome.Purchased);
                else StateChanged?.Invoke();
                break;

            case FailedOrder failed:
                // The entitlement is already saved; Play re-delivers the order next launch
                // and confirmation is retried then.
                Debug.LogWarning($"[PurchaseManager] Confirmation failed: {failed.FailureReason} — {failed.Details}");
                if (Busy && !restoreRequested) Finish(Outcome.Purchased);
                break;
        }
    }

    private void OnPurchaseFailed(FailedOrder failed)
    {
        Debug.LogWarning($"[PurchaseManager] Purchase failed: {failed.FailureReason} — {failed.Details}");

        switch (failed.FailureReason)
        {
            case PurchaseFailureReason.UserCancelled:
                Finish(Outcome.Cancelled);
                break;

            case PurchaseFailureReason.DuplicateTransaction:
                // Play says this account already owns it — pull the purchase back in.
                Busy = false;
                RestorePurchases();
                break;

            default:
                Finish(Outcome.Failed);
                break;
        }
    }

    /// <summary>
    /// A purchase waiting on payment (cash or bank transfer in some markets). Nothing is
    /// granted until Play delivers it as a PendingOrder.
    /// </summary>
    private void OnPurchaseDeferred(DeferredOrder order)
    {
        Debug.Log("[PurchaseManager] Purchase deferred — waiting for payment.");
        Finish(Outcome.Pending);
    }

    private static bool Contains(IReadOnlyList<ConfirmedOrder> orders)
    {
        foreach (var order in orders) if (Contains(order)) return true;
        return false;
    }

    private static bool Contains(IReadOnlyList<PendingOrder> orders)
    {
        foreach (var order in orders) if (Contains(order)) return true;
        return false;
    }

    private static bool Contains(Order order)
    {
        if (order?.CartOrdered == null) return false;
        foreach (CartItem item in order.CartOrdered.Items())
        {
            if (item.Product?.definition?.id == RemoveAds.ProductId) return true;
        }
        return false;
    }

#else

    private void InitializeStore()
    {
        Debug.Log("[PurchaseManager] In-App Purchasing package not installed — purchases are disabled.");
    }

    public void EnsureConnected() { }

    public void BuyRemoveAds(string source = null)
    {
        purchaseSource = source;
        Finish(Outcome.StoreUnavailable);
    }

    public void RestorePurchases() => Finish(Outcome.StoreUnavailable);

#endif
}
