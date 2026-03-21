// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.PurchasesUpdatedListener;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Manages the full Google Play Billing lifecycle for color theme in-app purchases.
 *
 * <p>Responsibilities:
 * <ul>
 *   <li>Initialize and maintain a {@link BillingClient} connection (with reconnection).</li>
 *   <li>Query available theme {@link ProductDetails} from Google Play.</li>
 *   <li>Launch the billing flow when the user wants to buy a theme.</li>
 *   <li>Handle {@link PurchasesUpdatedListener} callbacks (new purchases + errors).</li>
 *   <li>Acknowledge purchases so Google Play does not auto-refund after 3 days.</li>
 *   <li>Restore owned purchases on every app launch via {@link #restorePurchases()}.</li>
 *   <li>Cache ownership in {@link SharedPreferences} as a fast read-only hint; the cache
 *       is always overwritten by Google Play truth on restore.</li>
 * </ul>
 *
 * <p>Usage: Instantiate in your Capacitor plugin, call {@link #startConnection()} once,
 * then use the remaining public methods as needed.
 */
public class ThemeBillingManager {

    private static final String TAG = "ThemeBillingManager";
    private static final String PREFS_NAME = "theme_purchases";

    // ------------------------------------------------------------------ //
    //  Callbacks                                                           //
    // ------------------------------------------------------------------ //

    /** Called whenever the set of owned themes changes (purchase or restore). */
    public interface OnPurchasesUpdatedCallback {
        void onPurchasesUpdated(List<String> ownedProductIds);
    }

    /** Called with the product details list after a successful query. */
    public interface OnProductDetailsCallback {
        void onProductDetails(List<ProductDetails> productDetails);
    }

    /** Called after a purchase or restore attempt with the result. */
    public interface OnBillingErrorCallback {
        void onBillingError(int responseCode, String debugMessage);
    }

    // ------------------------------------------------------------------ //
    //  Fields                                                              //
    // ------------------------------------------------------------------ //

    private final Context applicationContext;
    private BillingClient billingClient;
    private boolean isConnecting;

    /** Cached product details indexed by productId. */
    private final Map<String, ProductDetails> productDetailsCache = new HashMap<>();

    @Nullable private OnPurchasesUpdatedCallback purchasesUpdatedCallback;
    @Nullable private OnProductDetailsCallback productDetailsCallback;
    @Nullable private OnBillingErrorCallback billingErrorCallback;

    // ------------------------------------------------------------------ //
    //  Constructor                                                          //
    // ------------------------------------------------------------------ //

    public ThemeBillingManager(@NonNull Context context) {
        this.applicationContext = context.getApplicationContext();
    }

    // ------------------------------------------------------------------ //
    //  Callback setters                                                     //
    // ------------------------------------------------------------------ //

    public void setOnPurchasesUpdatedCallback(@Nullable OnPurchasesUpdatedCallback cb) {
        this.purchasesUpdatedCallback = cb;
    }

    public void setOnProductDetailsCallback(@Nullable OnProductDetailsCallback cb) {
        this.productDetailsCallback = cb;
    }

    public void setOnBillingErrorCallback(@Nullable OnBillingErrorCallback cb) {
        this.billingErrorCallback = cb;
    }

    // ------------------------------------------------------------------ //
    //  Connection lifecycle                                                 //
    // ------------------------------------------------------------------ //

    /**
     * Builds the {@link BillingClient} and starts a connection to Google Play.
     * On a successful connection, automatically restores existing purchases.
     * Safe to call multiple times; if a client is already connected it no-ops.
     */
    public void startConnection() {
        if (isConnecting || (billingClient != null && billingClient.isReady())) {
            return;
        }

        billingClient = BillingClient.newBuilder(applicationContext)
                .setListener(purchasesUpdatedListener)
                .enablePendingPurchases(
                        PendingPurchasesParams.newBuilder()
                                .enableOneTimeProducts()
                                .build()
                )
                .build();

        isConnecting = true;
        billingClient.startConnection(new BillingClientStateListener() {
            @Override
            public void onBillingSetupFinished(@NonNull BillingResult billingResult) {
                isConnecting = false;
                if (billingResult.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                    Log.d(TAG, "BillingClient connected.");
                    restorePurchases();
                    queryProductDetails();
                } else {
                    Log.w(TAG, "BillingClient setup failed: " + billingResult.getDebugMessage());
                    notifyError(billingResult);
                }
            }

            @Override
            public void onBillingServiceDisconnected() {
                Log.w(TAG, "BillingClient disconnected. Will reconnect on next operation.");
                isConnecting = false;
                // Do NOT retry infinitely; reconnect lazily on next method call.
                billingClient = null;
            }
        });
    }

    /** Releases the billing client. Call from your plugin's {@code handleOnDestroy()}. */
    public void endConnection() {
        isConnecting = false;
        if (billingClient != null) {
            billingClient.endConnection();
            billingClient = null;
        }
    }

    // ------------------------------------------------------------------ //
    //  Product details                                                      //
    // ------------------------------------------------------------------ //

    /**
     * Queries Google Play for {@link ProductDetails} of all theme products.
     * Results are delivered to {@link #productDetailsCallback} and cached internally.
     */
    public void queryProductDetails() {
        if (!ensureConnected()) return;

        List<QueryProductDetailsParams.Product> productList = new ArrayList<>();
        for (String productId : ThemeProducts.ALL_PRODUCT_IDS) {
            productList.add(
                    QueryProductDetailsParams.Product.newBuilder()
                            .setProductId(productId)
                            .setProductType(BillingClient.ProductType.INAPP)
                            .build()
            );
        }

        QueryProductDetailsParams params = QueryProductDetailsParams.newBuilder()
                .setProductList(productList)
                .build();

        billingClient.queryProductDetailsAsync(params, (billingResult, productDetailsList) -> {
            if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                Log.w(TAG, "queryProductDetails failed: " + billingResult.getDebugMessage());
                notifyError(billingResult);
                return;
            }
            productDetailsCache.clear();
            if (productDetailsList != null) {
                for (ProductDetails pd : productDetailsList) {
                    productDetailsCache.put(pd.getProductId(), pd);
                }
            }
            if (productDetailsCallback != null) {
                productDetailsCallback.onProductDetails(new ArrayList<>(productDetailsCache.values()));
            }
        });
    }

    // ------------------------------------------------------------------ //
    //  Purchase flow                                                        //
    // ------------------------------------------------------------------ //

    /**
     * Launches the Google Play purchase sheet for the given {@code productId}.
     *
     * @param activity   The foreground activity (required by Play Billing).
     * @param productId  One of the IDs defined in {@link ThemeProducts}.
     */
    public void launchBillingFlow(@NonNull Activity activity, @NonNull String productId) {
        if (!ensureConnected()) {
            notifyError(buildBillingResult(
                    BillingClient.BillingResponseCode.SERVICE_DISCONNECTED,
                    "BillingClient is not ready yet. Retry after setup finishes."
            ));
            return;
        }

        ProductDetails productDetails = productDetailsCache.get(productId);
        if (productDetails == null) {
            // Details not cached yet — re-query and retry.
            queryProductDetailsAndThen(activity, productId);
            return;
        }

        BillingFlowParams flowParams = BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(
                        List.of(BillingFlowParams.ProductDetailsParams.newBuilder()
                                .setProductDetails(productDetails)
                                .build())
                ).build();

        BillingResult result = billingClient.launchBillingFlow(activity, flowParams);
        if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
            Log.w(TAG, "launchBillingFlow failed: " + result.getDebugMessage());
            notifyError(result);
        }
    }

    /** Helper: query product details then immediately launch the billing flow. */
    private void queryProductDetailsAndThen(@NonNull Activity activity, @NonNull String productId) {
        if (!ensureConnected()) {
            notifyError(buildBillingResult(
                    BillingClient.BillingResponseCode.SERVICE_DISCONNECTED,
                    "BillingClient is not ready yet. Retry after setup finishes."
            ));
            return;
        }

        List<QueryProductDetailsParams.Product> productList = List.of(
                QueryProductDetailsParams.Product.newBuilder()
                        .setProductId(productId)
                        .setProductType(BillingClient.ProductType.INAPP)
                        .build()
        );
        billingClient.queryProductDetailsAsync(
                QueryProductDetailsParams.newBuilder().setProductList(productList).build(),
                (billingResult, productDetailsList) -> {
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK
                            || productDetailsList == null || productDetailsList.isEmpty()) {
                        notifyError(billingResult);
                        return;
                    }
                    ProductDetails pd = productDetailsList.get(0);
                    productDetailsCache.put(pd.getProductId(), pd);
                    launchBillingFlow(activity, productId);
                }
        );
    }

    // ------------------------------------------------------------------ //
    //  Purchases updated listener                                           //
    // ------------------------------------------------------------------ //

    private final PurchasesUpdatedListener purchasesUpdatedListener = (billingResult, purchases) -> {
        int code = billingResult.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase purchase : purchases) {
                handlePurchase(purchase);
            }
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) {
            Log.d(TAG, "User cancelled purchase.");
            notifyPurchasesUpdated();
        } else {
            Log.w(TAG, "Purchase update error " + code + ": " + billingResult.getDebugMessage());
            notifyError(billingResult);
        }
    };

    // ------------------------------------------------------------------ //
    //  Handle / acknowledge purchase                                        //
    // ------------------------------------------------------------------ //

    /**
     * Grants and acknowledges a purchase.
     * Must be called for every purchase in {@link Purchase.PurchaseState#PURCHASED} state
     * to prevent Google Play from automatically refunding after 3 days.
     */
    private void handlePurchase(@NonNull Purchase purchase) {
        if (purchase.getPurchaseState() != Purchase.PurchaseState.PURCHASED) {
            // PENDING or UNSPECIFIED — do not unlock yet.
            Log.d(TAG, "Purchase pending for: " + purchase.getProducts());
            notifyPurchasesUpdated();
            return;
        }

        // Unlock theme locally.
        for (String productId : purchase.getProducts()) {
            saveOwnedTheme(productId, true);
        }

        // Acknowledge if not already acknowledged.
        if (!purchase.isAcknowledged()) {
            AcknowledgePurchaseParams ackParams = AcknowledgePurchaseParams.newBuilder()
                    .setPurchaseToken(purchase.getPurchaseToken())
                    .build();
            billingClient.acknowledgePurchase(ackParams, ackResult -> {
                if (ackResult.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                    Log.d(TAG, "Purchase acknowledged: " + purchase.getProducts());
                } else {
                    Log.w(TAG, "Acknowledge failed: " + ackResult.getDebugMessage());
                }
            });
        }

        // Notify listeners about the updated set.
        notifyPurchasesUpdated();
    }

    // ------------------------------------------------------------------ //
    //  Restore purchases                                                    //
    // ------------------------------------------------------------------ //

    /**
     * Queries Google Play for all currently owned (non-consumed) in-app purchases and
     * rebuilds the local {@link SharedPreferences} cache. Always call this on app launch
     * so the cache reflects the authoritative Google Play state.
     */
    public void restorePurchases() {
        if (!ensureConnected()) return;

        QueryPurchasesParams params = QueryPurchasesParams.newBuilder()
                .setProductType(BillingClient.ProductType.INAPP)
                .build();

        billingClient.queryPurchasesAsync(params, (billingResult, purchases) -> {
            if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                Log.w(TAG, "queryPurchasesAsync failed: " + billingResult.getDebugMessage());
                notifyError(billingResult);
                return;
            }

            // Reset all theme flags before re-applying from Play truth.
            clearAllOwnedThemes();

            if (purchases != null) {
                for (Purchase purchase : purchases) {
                    handlePurchase(purchase);
                }
            }

            // Notify even if list is empty (so UI can show "buy" buttons).
            notifyPurchasesUpdated();
        });
    }

    // ------------------------------------------------------------------ //
    //  SharedPreferences helpers (cache only — never sole source of truth)  //
    // ------------------------------------------------------------------ //

    /**
     * Returns {@code true} if the local cache indicates the theme is owned.
     * <strong>Do not rely on this alone</strong> — always call {@link #restorePurchases()}
     * on app launch to keep the cache in sync with Google Play.
     */
    public boolean isThemeOwned(@NonNull String productId) {
        return getPrefs().getBoolean(productId, false);
    }

    private void saveOwnedTheme(@NonNull String productId, boolean owned) {
        getPrefs().edit().putBoolean(productId, owned).apply();
    }

    private void clearAllOwnedThemes() {
        SharedPreferences.Editor editor = getPrefs().edit();
        for (String productId : ThemeProducts.ALL_PRODUCT_IDS) {
            editor.remove(productId);
        }
        editor.apply();
    }

    private SharedPreferences getPrefs() {
        return applicationContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    // ------------------------------------------------------------------ //
    //  Internal helpers                                                     //
    // ------------------------------------------------------------------ //

    /**
     * Ensures the billing client is connected. If disconnected, starts the connection
     * and returns {@code false} so the caller can retry after the callback fires.
     */
    private boolean ensureConnected() {
        if (billingClient != null && billingClient.isReady()) {
            return true;
        }

        if (isConnecting) {
            Log.d(TAG, "BillingClient connection already in progress.");
            return false;
        }

        Log.d(TAG, "BillingClient not ready. Starting connection.");
        startConnection();
        return false;
    }

    private void notifyPurchasesUpdated() {
        if (purchasesUpdatedCallback != null) {
            List<String> ownedIds = new ArrayList<>();
            for (String productId : ThemeProducts.ALL_PRODUCT_IDS) {
                if (isThemeOwned(productId)) {
                    ownedIds.add(productId);
                }
            }
            purchasesUpdatedCallback.onPurchasesUpdated(ownedIds);
        }
    }

    private void notifyError(@NonNull BillingResult result) {
        if (billingErrorCallback != null) {
            billingErrorCallback.onBillingError(result.getResponseCode(), result.getDebugMessage());
        }
    }

    @NonNull
    private BillingResult buildBillingResult(int responseCode, @NonNull String debugMessage) {
        return BillingResult.newBuilder()
                .setResponseCode(responseCode)
                .setDebugMessage(debugMessage)
                .build();
    }
}
