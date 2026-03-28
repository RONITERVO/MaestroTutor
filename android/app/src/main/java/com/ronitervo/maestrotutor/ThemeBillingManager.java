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
import com.android.billingclient.api.ConsumeParams;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.PurchasesUpdatedListener;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * Google Play Billing manager used by the Capacitor ThemeBilling plugin.
 *
 * <p>This manager preserves the existing theme unlock behavior while also
 * supporting consumable managed-credit products:
 * <ul>
 *   <li>Theme SKUs remain permanent non-consumables and are acknowledged.</li>
 *   <li>Non-theme SKUs are treated as consumables and remain visible until
 *       JavaScript verifies them server-side and calls {@link #consumePurchase}.</li>
 * </ul>
 */
public class ThemeBillingManager {

    private static final String TAG = "ThemeBillingManager";
    private static final String PREFS_NAME = "theme_purchases";

    public interface OnPurchasesUpdatedCallback {
        void onPurchasesUpdated(List<String> ownedProductIds, List<Purchase> purchases);
    }

    public interface OnProductDetailsCallback {
        void onProductDetails(List<ProductDetails> productDetails);
    }

    public interface OnBillingErrorCallback {
        void onBillingError(int responseCode, String debugMessage);
    }

    public interface ConsumeCallback {
        void onConsumeFinished(boolean success, int responseCode, String debugMessage);
    }

    private final Context applicationContext;
    private BillingClient billingClient;
    private boolean isConnecting;

    private final Map<String, ProductDetails> productDetailsCache = new HashMap<>();
    private final Map<String, Purchase> latestPurchasesByToken = new LinkedHashMap<>();
    private final Set<String> pendingProductDetailsIds = new LinkedHashSet<>();

    @Nullable private OnPurchasesUpdatedCallback purchasesUpdatedCallback;
    @Nullable private OnProductDetailsCallback productDetailsCallback;
    @Nullable private OnBillingErrorCallback billingErrorCallback;

    public ThemeBillingManager(@NonNull Context context) {
        this.applicationContext = context.getApplicationContext();
    }

    public void setOnPurchasesUpdatedCallback(@Nullable OnPurchasesUpdatedCallback cb) {
        this.purchasesUpdatedCallback = cb;
    }

    public void setOnProductDetailsCallback(@Nullable OnProductDetailsCallback cb) {
        this.productDetailsCallback = cb;
    }

    public void setOnBillingErrorCallback(@Nullable OnBillingErrorCallback cb) {
        this.billingErrorCallback = cb;
    }

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
                    if (pendingProductDetailsIds.isEmpty()) {
                        queryProductDetails();
                    } else {
                        List<String> queuedProductIds = new ArrayList<>(pendingProductDetailsIds);
                        pendingProductDetailsIds.clear();
                        queryProductDetails(queuedProductIds);
                    }
                } else {
                    Log.w(TAG, "BillingClient setup failed: " + billingResult.getDebugMessage());
                    notifyError(billingResult);
                }
            }

            @Override
            public void onBillingServiceDisconnected() {
                Log.w(TAG, "BillingClient disconnected. Will reconnect on next operation.");
                isConnecting = false;
                billingClient = null;
            }
        });
    }

    public void endConnection() {
        isConnecting = false;
        if (billingClient != null) {
            billingClient.endConnection();
            billingClient = null;
        }
    }

    public void queryProductDetails() {
        queryProductDetails(ThemeProducts.ALL_PRODUCT_IDS);
    }

    public void queryProductDetails(@NonNull List<String> productIds) {
        if (!ensureConnected()) {
            pendingProductDetailsIds.addAll(productIds);
            return;
        }
        if (productIds.isEmpty()) {
            if (productDetailsCallback != null) {
                productDetailsCallback.onProductDetails(new ArrayList<>());
            }
            return;
        }

        List<QueryProductDetailsParams.Product> productList = new ArrayList<>();
        for (String productId : productIds) {
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
                for (ProductDetails productDetails : productDetailsList) {
                    productDetailsCache.put(productDetails.getProductId(), productDetails);
                }
            }

            if (productDetailsCallback != null) {
                productDetailsCallback.onProductDetails(new ArrayList<>(productDetailsCache.values()));
            }
        });
    }

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
            queryProductDetailsAndThen(activity, productId);
            return;
        }

        BillingFlowParams flowParams = BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(
                        List.of(BillingFlowParams.ProductDetailsParams.newBuilder()
                                .setProductDetails(productDetails)
                                .build())
                )
                .build();

        BillingResult result = billingClient.launchBillingFlow(activity, flowParams);
        if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
            Log.w(TAG, "launchBillingFlow failed: " + result.getDebugMessage());
            notifyError(result);
        }
    }

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
                            || productDetailsList == null
                            || productDetailsList.isEmpty()) {
                        notifyError(billingResult);
                        return;
                    }

                    ProductDetails productDetails = productDetailsList.get(0);
                    productDetailsCache.put(productDetails.getProductId(), productDetails);
                    launchBillingFlow(activity, productId);
                }
        );
    }

    private final PurchasesUpdatedListener purchasesUpdatedListener = (billingResult, purchases) -> {
        int code = billingResult.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase purchase : purchases) {
                handlePurchase(purchase, true);
            }
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) {
            Log.d(TAG, "User cancelled purchase.");
            notifyPurchasesUpdated();
        } else {
            Log.w(TAG, "Purchase update error " + code + ": " + billingResult.getDebugMessage());
            notifyError(billingResult);
        }
    };

    private void handlePurchase(@NonNull Purchase purchase, boolean notify) {
        latestPurchasesByToken.put(purchase.getPurchaseToken(), purchase);

        if (purchase.getPurchaseState() != Purchase.PurchaseState.PURCHASED) {
            Log.d(TAG, "Purchase pending for: " + purchase.getProducts());
            if (notify) {
                notifyPurchasesUpdated();
            }
            return;
        }

        boolean hasThemePurchase = false;
        for (String productId : purchase.getProducts()) {
            if (isThemeProduct(productId)) {
                hasThemePurchase = true;
                saveOwnedTheme(productId, true);
            }
        }

        if (hasThemePurchase && !purchase.isAcknowledged()) {
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

        if (notify) {
            notifyPurchasesUpdated();
        }
    }

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

            clearAllOwnedThemes();
            latestPurchasesByToken.clear();

            if (purchases != null) {
                for (Purchase purchase : purchases) {
                    handlePurchase(purchase, false);
                }
            }

            notifyPurchasesUpdated();
        });
    }

    public boolean isThemeOwned(@NonNull String productId) {
        return getPrefs().getBoolean(productId, false);
    }

    @NonNull
    public List<String> getOwnedProductIds() {
        List<String> ownedIds = new ArrayList<>();
        for (String productId : ThemeProducts.ALL_PRODUCT_IDS) {
            if (isThemeOwned(productId)) {
                ownedIds.add(productId);
            }
        }
        return ownedIds;
    }

    @NonNull
    public List<Purchase> getLatestPurchases() {
        return new ArrayList<>(latestPurchasesByToken.values());
    }

    public void consumePurchase(@NonNull String purchaseToken, @Nullable ConsumeCallback callback) {
        if (!ensureConnected()) {
            BillingResult result = buildBillingResult(
                    BillingClient.BillingResponseCode.SERVICE_DISCONNECTED,
                    "BillingClient is not ready yet. Retry after setup finishes."
            );
            notifyError(result);
            if (callback != null) {
                callback.onConsumeFinished(false, result.getResponseCode(), result.getDebugMessage());
            }
            return;
        }

        ConsumeParams params = ConsumeParams.newBuilder()
                .setPurchaseToken(purchaseToken)
                .build();

        billingClient.consumeAsync(params, (billingResult, outToken) -> {
            boolean success = billingResult.getResponseCode() == BillingClient.BillingResponseCode.OK;
            if (success) {
                latestPurchasesByToken.remove(outToken);
                notifyPurchasesUpdated();
            } else {
                notifyError(billingResult);
            }

            if (callback != null) {
                callback.onConsumeFinished(success, billingResult.getResponseCode(), billingResult.getDebugMessage());
            }
        });
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
            purchasesUpdatedCallback.onPurchasesUpdated(getOwnedProductIds(), getLatestPurchases());
        }
    }

    private void notifyError(@NonNull BillingResult result) {
        if (billingErrorCallback != null) {
            billingErrorCallback.onBillingError(result.getResponseCode(), result.getDebugMessage());
        }
    }

    private boolean isThemeProduct(@NonNull String productId) {
        return ThemeProducts.ALL_PRODUCT_IDS.contains(productId);
    }

    @NonNull
    private BillingResult buildBillingResult(int responseCode, @NonNull String debugMessage) {
        return BillingResult.newBuilder()
                .setResponseCode(responseCode)
                .setDebugMessage(debugMessage)
                .build();
    }
}
