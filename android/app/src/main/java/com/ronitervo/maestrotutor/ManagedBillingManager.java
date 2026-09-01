// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import android.app.Activity;
import android.content.Context;
import android.util.Log;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

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
import com.android.billingclient.api.UnfetchedProduct;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * Thin Google Play Billing transport for backend-managed products.
 *
 * <p>This class deliberately has no product catalogue, entitlement store,
 * acknowledgement, consumption, or grant logic. The web layer supplies the
 * product IDs configured by the backend, and the backend verifies each token,
 * grants the durable benefit idempotently, then consumes it. Keeping those
 * irreversible decisions out of the client prevents local state from becoming
 * an authority and lets future managed products reuse the same bridge.</p>
 */
public final class ManagedBillingManager {

    private static final String TAG = "ManagedBillingManager";

    public interface OnPurchasesUpdatedCallback {
        void onPurchasesUpdated(List<Purchase> purchases);
    }

    public interface OnProductDetailsCallback {
        void onProductDetails(List<ProductDetails> productDetails);
    }

    public interface OnBillingErrorCallback {
        void onBillingError(int responseCode, String debugMessage);
    }

    private final Context applicationContext;
    private final Map<String, ProductDetails> productDetailsCache = new LinkedHashMap<>();
    private final Map<String, Purchase> purchaseCache = new LinkedHashMap<>();
    private final Set<String> queuedProductIds = new LinkedHashSet<>();

    @Nullable private BillingClient billingClient;
    private boolean isConnecting;
    private boolean restoreWhenConnected = true;

    @Nullable private OnPurchasesUpdatedCallback purchasesUpdatedCallback;
    @Nullable private OnProductDetailsCallback productDetailsCallback;
    @Nullable private OnBillingErrorCallback billingErrorCallback;

    public ManagedBillingManager(@NonNull Context context) {
        applicationContext = context.getApplicationContext();
    }

    public void setOnPurchasesUpdatedCallback(@Nullable OnPurchasesUpdatedCallback callback) {
        purchasesUpdatedCallback = callback;
    }

    public void setOnProductDetailsCallback(@Nullable OnProductDetailsCallback callback) {
        productDetailsCallback = callback;
    }

    public void setOnBillingErrorCallback(@Nullable OnBillingErrorCallback callback) {
        billingErrorCallback = callback;
    }

    /** Starts the Play connection. Safe to call repeatedly. */
    public void startConnection() {
        if (isReady()) {
            flushQueuedWork();
            return;
        }
        if (isConnecting) return;

        if (billingClient != null) {
            billingClient.endConnection();
            billingClient = null;
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
                    flushQueuedWork();
                    return;
                }
                Log.w(TAG, "BillingClient setup failed: " + billingResult.getDebugMessage());
                notifyError(billingResult);
            }

            @Override
            public void onBillingServiceDisconnected() {
                Log.w(TAG, "BillingClient disconnected; reconnecting on the next operation.");
                isConnecting = false;
                restoreWhenConnected = true;
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

    /** Queries only the product IDs supplied by the backend-configured client. */
    public void queryProductDetails(@NonNull List<String> productIds) {
        queuedProductIds.addAll(normalizeProductIds(productIds));
        if (!ensureConnected()) return;
        queryQueuedProductDetails();
    }

    public void launchBillingFlow(
            @NonNull Activity activity,
            @NonNull String productId,
            @NonNull String obfuscatedAccountId
    ) {
        if (!ensureConnected()) {
            notifyError(disconnectedResult());
            return;
        }

        ProductDetails productDetails = productDetailsCache.get(productId);
        if (productDetails == null) {
            queryProductThenLaunch(activity, productId, obfuscatedAccountId);
            return;
        }
        launchBillingFlow(activity, productDetails, obfuscatedAccountId);
    }

    /** Reconciles unconsumed purchases. Results are passed to the backend unchanged. */
    public void restorePurchases() {
        restoreWhenConnected = true;
        if (!ensureConnected()) return;
        queryPurchases();
    }

    @NonNull
    public List<Purchase> getCachedPurchases() {
        return new ArrayList<>(purchaseCache.values());
    }

    private final PurchasesUpdatedListener purchasesUpdatedListener = (billingResult, purchases) -> {
        int code = billingResult.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            mergePurchases(purchases);
            notifyPurchasesUpdated();
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) {
            Log.d(TAG, "User cancelled purchase.");
        } else {
            Log.w(TAG, "Purchase update failed: " + billingResult.getDebugMessage());
            notifyError(billingResult);
        }
    };

    private void flushQueuedWork() {
        if (restoreWhenConnected) queryPurchases();
        if (!queuedProductIds.isEmpty()) queryQueuedProductDetails();
    }

    private void queryQueuedProductDetails() {
        if (!isReady() || queuedProductIds.isEmpty()) return;
        List<String> productIds = new ArrayList<>(queuedProductIds);
        queuedProductIds.clear();

        List<QueryProductDetailsParams.Product> products = new ArrayList<>();
        for (String productId : productIds) {
            products.add(QueryProductDetailsParams.Product.newBuilder()
                    .setProductId(productId)
                    .setProductType(BillingClient.ProductType.INAPP)
                    .build());
        }

        billingClient.queryProductDetailsAsync(
                QueryProductDetailsParams.newBuilder().setProductList(products).build(),
                (billingResult, queryResult) -> {
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                        Log.w(TAG, "Product query failed: " + billingResult.getDebugMessage());
                        notifyError(billingResult);
                        return;
                    }

                    List<ProductDetails> fetched = queryResult == null
                            ? List.of()
                            : queryResult.getProductDetailsList();
                    for (ProductDetails product : fetched) {
                        productDetailsCache.put(product.getProductId(), product);
                    }

                    List<UnfetchedProduct> unfetched = queryResult == null
                            ? List.of()
                            : queryResult.getUnfetchedProductList();
                    for (UnfetchedProduct product : unfetched) {
                        Log.w(TAG, "Product unavailable: " + product.getProductId()
                                + " (status " + product.getStatusCode() + ")");
                    }

                    if (productDetailsCallback != null) {
                        List<ProductDetails> requested = new ArrayList<>();
                        for (String productId : productIds) {
                            ProductDetails detail = productDetailsCache.get(productId);
                            if (detail != null) requested.add(detail);
                        }
                        productDetailsCallback.onProductDetails(requested);
                    }
                }
        );
    }

    private void queryProductThenLaunch(
            @NonNull Activity activity,
            @NonNull String productId,
            @NonNull String obfuscatedAccountId
    ) {
        List<QueryProductDetailsParams.Product> products = List.of(
                QueryProductDetailsParams.Product.newBuilder()
                        .setProductId(productId)
                        .setProductType(BillingClient.ProductType.INAPP)
                        .build()
        );
        billingClient.queryProductDetailsAsync(
                QueryProductDetailsParams.newBuilder().setProductList(products).build(),
                (billingResult, queryResult) -> {
                    List<ProductDetails> fetched = queryResult == null
                            ? List.of()
                            : queryResult.getProductDetailsList();
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK
                            || fetched.isEmpty()) {
                        notifyError(billingResult.getResponseCode() == BillingClient.BillingResponseCode.OK
                                ? BillingResult.newBuilder()
                                        .setResponseCode(BillingClient.BillingResponseCode.ITEM_UNAVAILABLE)
                                        .setDebugMessage("Product is not available in Google Play.")
                                        .build()
                                : billingResult);
                        return;
                    }
                    ProductDetails detail = fetched.get(0);
                    productDetailsCache.put(detail.getProductId(), detail);
                    launchBillingFlow(activity, detail, obfuscatedAccountId);
                }
        );
    }

    private void launchBillingFlow(
            @NonNull Activity activity,
            @NonNull ProductDetails productDetails,
            @NonNull String obfuscatedAccountId
    ) {
        BillingFlowParams flowParams = BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(List.of(
                        BillingFlowParams.ProductDetailsParams.newBuilder()
                                .setProductDetails(productDetails)
                                .build()
                ))
                .setObfuscatedAccountId(obfuscatedAccountId)
                .build();
        BillingResult result = billingClient.launchBillingFlow(activity, flowParams);
        if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
            notifyError(result);
        }
    }

    private void queryPurchases() {
        if (!isReady()) return;
        restoreWhenConnected = false;
        billingClient.queryPurchasesAsync(
                QueryPurchasesParams.newBuilder()
                        .setProductType(BillingClient.ProductType.INAPP)
                        .build(),
                (billingResult, purchases) -> {
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                        Log.w(TAG, "Purchase restore failed: " + billingResult.getDebugMessage());
                        notifyError(billingResult);
                        return;
                    }
                    purchaseCache.clear();
                    if (purchases != null) mergePurchases(purchases);
                    notifyPurchasesUpdated();
                }
        );
    }

    private void mergePurchases(@NonNull List<Purchase> purchases) {
        for (Purchase purchase : purchases) {
            purchaseCache.put(purchase.getPurchaseToken(), purchase);
        }
    }

    private void notifyPurchasesUpdated() {
        if (purchasesUpdatedCallback != null) {
            purchasesUpdatedCallback.onPurchasesUpdated(getCachedPurchases());
        }
    }

    private void notifyError(@NonNull BillingResult result) {
        if (billingErrorCallback != null) {
            billingErrorCallback.onBillingError(result.getResponseCode(), result.getDebugMessage());
        }
    }

    private boolean ensureConnected() {
        if (isReady()) return true;
        startConnection();
        return false;
    }

    private boolean isReady() {
        return billingClient != null && billingClient.isReady();
    }

    @NonNull
    private BillingResult disconnectedResult() {
        return BillingResult.newBuilder()
                .setResponseCode(BillingClient.BillingResponseCode.SERVICE_DISCONNECTED)
                .setDebugMessage("Google Play Billing is connecting. Try again shortly.")
                .build();
    }

    @NonNull
    private List<String> normalizeProductIds(@NonNull List<String> productIds) {
        Set<String> normalized = new LinkedHashSet<>();
        for (String productId : productIds) {
            if (productId != null && !productId.trim().isEmpty()) {
                normalized.add(productId.trim());
            }
        }
        return new ArrayList<>(normalized);
    }
}
