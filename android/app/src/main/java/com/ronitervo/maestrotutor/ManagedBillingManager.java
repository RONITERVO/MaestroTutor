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

import java.lang.ref.WeakReference;
import java.util.ArrayList;
import java.util.Collections;
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

    public interface OnPurchasesQueryCallback {
        void onPurchasesQueryComplete(BillingResult billingResult, List<Purchase> purchases);
    }

    private static final class PendingPurchaseRequest {
        private final WeakReference<Activity> activity;
        private final String productId;
        private final String obfuscatedAccountId;

        private PendingPurchaseRequest(
                @NonNull Activity activity,
                @NonNull String productId,
                @NonNull String obfuscatedAccountId
        ) {
            this.activity = new WeakReference<>(activity);
            this.productId = productId;
            this.obfuscatedAccountId = obfuscatedAccountId;
        }
    }

    private final Context applicationContext;
    private final Map<String, ProductDetails> productDetailsCache = new LinkedHashMap<>();
    private final Map<String, Purchase> purchaseCache = new LinkedHashMap<>();
    private final Set<String> queuedProductIds = new LinkedHashSet<>();
    private final List<OnPurchasesQueryCallback> purchasesQueryCallbacks = new ArrayList<>();

    @Nullable private BillingClient billingClient;
    @Nullable private PendingPurchaseRequest pendingPurchaseRequest;
    private boolean isConnecting;
    private boolean purchasesQueryInFlight;
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
                pendingPurchaseRequest = null;
                completePurchasesQueryCallbacks(billingResult, Collections.emptyList());
                notifyError(billingResult);
            }

            @Override
            public void onBillingServiceDisconnected() {
                Log.w(TAG, "BillingClient disconnected; reconnecting on the next operation.");
                boolean hasAwaitingOperation = pendingPurchaseRequest != null
                        || !purchasesQueryCallbacks.isEmpty()
                        || !queuedProductIds.isEmpty();
                isConnecting = false;
                purchasesQueryInFlight = false;
                restoreWhenConnected = true;
                billingClient = null;
                if (hasAwaitingOperation) startConnection();
            }
        });
    }

    public void endConnection() {
        isConnecting = false;
        purchasesQueryInFlight = false;
        pendingPurchaseRequest = null;
        purchasesQueryCallbacks.clear();
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
        if (!isReady()) {
            pendingPurchaseRequest = new PendingPurchaseRequest(
                    activity,
                    productId,
                    obfuscatedAccountId
            );
            startConnection();
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
    public void restorePurchases(@Nullable OnPurchasesQueryCallback callback) {
        if (callback != null) purchasesQueryCallbacks.add(callback);
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
            notifyError(billingResult);
        } else {
            Log.w(TAG, "Purchase update failed: " + billingResult.getDebugMessage());
            notifyError(billingResult);
        }
    };

    private void flushQueuedWork() {
        if (restoreWhenConnected) queryPurchases();
        if (!queuedProductIds.isEmpty()) queryQueuedProductDetails();

        PendingPurchaseRequest request = pendingPurchaseRequest;
        pendingPurchaseRequest = null;
        if (request == null) return;

        Activity activity = request.activity.get();
        if (activity == null || activity.isFinishing() || activity.isDestroyed()) {
            notifyError(BillingResult.newBuilder()
                    .setResponseCode(BillingClient.BillingResponseCode.DEVELOPER_ERROR)
                    .setDebugMessage("No foreground activity is available to launch Google Play Billing.")
                    .build());
            return;
        }
        launchBillingFlow(activity, request.productId, request.obfuscatedAccountId);
    }

    private void queryQueuedProductDetails() {
        if (!isReady() || queuedProductIds.isEmpty()) return;
        BillingClient client = billingClient;
        if (client == null || !client.isReady()) return;
        List<String> productIds = new ArrayList<>(queuedProductIds);
        queuedProductIds.clear();

        List<QueryProductDetailsParams.Product> products = new ArrayList<>();
        for (String productId : productIds) {
            products.add(QueryProductDetailsParams.Product.newBuilder()
                    .setProductId(productId)
                    .setProductType(BillingClient.ProductType.INAPP)
                    .build());
        }

        client.queryProductDetailsAsync(
                QueryProductDetailsParams.newBuilder().setProductList(products).build(),
                (billingResult, queryResult) -> {
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                        Log.w(TAG, "Product query failed: " + billingResult.getDebugMessage());
                        queuedProductIds.addAll(productIds);
                        notifyError(billingResult);
                        if (billingResult.getResponseCode()
                                == BillingClient.BillingResponseCode.SERVICE_DISCONNECTED) {
                            startConnection();
                        }
                        return;
                    }

                    List<ProductDetails> fetched = queryResult == null
                            ? Collections.emptyList()
                            : queryResult.getProductDetailsList();
                    for (ProductDetails product : fetched) {
                        productDetailsCache.put(product.getProductId(), product);
                    }

                    List<UnfetchedProduct> unfetched = queryResult == null
                            ? Collections.emptyList()
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
        BillingClient client = billingClient;
        if (client == null || !client.isReady()) {
            pendingPurchaseRequest = new PendingPurchaseRequest(
                    activity,
                    productId,
                    obfuscatedAccountId
            );
            startConnection();
            return;
        }

        List<QueryProductDetailsParams.Product> products = Collections.singletonList(
                QueryProductDetailsParams.Product.newBuilder()
                        .setProductId(productId)
                        .setProductType(BillingClient.ProductType.INAPP)
                        .build()
        );
        client.queryProductDetailsAsync(
                QueryProductDetailsParams.newBuilder().setProductList(products).build(),
                (billingResult, queryResult) -> {
                    List<ProductDetails> fetched = queryResult == null
                            ? Collections.emptyList()
                            : queryResult.getProductDetailsList();
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK
                            || fetched.isEmpty()) {
                        if (billingResult.getResponseCode()
                                == BillingClient.BillingResponseCode.SERVICE_DISCONNECTED) {
                            pendingPurchaseRequest = new PendingPurchaseRequest(
                                    activity,
                                    productId,
                                    obfuscatedAccountId
                            );
                            startConnection();
                            return;
                        }
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
        BillingClient client = billingClient;
        if (client == null || !client.isReady()) {
            notifyError(disconnectedResult());
            return;
        }

        BillingFlowParams flowParams = BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(Collections.singletonList(
                        BillingFlowParams.ProductDetailsParams.newBuilder()
                                .setProductDetails(productDetails)
                                .build()
                ))
                .setObfuscatedAccountId(obfuscatedAccountId)
                .build();
        BillingResult result = client.launchBillingFlow(activity, flowParams);
        if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
            notifyError(result);
        }
    }

    private void queryPurchases() {
        if (!isReady() || purchasesQueryInFlight) return;
        BillingClient client = billingClient;
        if (client == null || !client.isReady()) return;
        restoreWhenConnected = false;
        purchasesQueryInFlight = true;
        client.queryPurchasesAsync(
                QueryPurchasesParams.newBuilder()
                        .setProductType(BillingClient.ProductType.INAPP)
                        .build(),
                (billingResult, purchases) -> {
                    purchasesQueryInFlight = false;
                    if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                        Log.w(TAG, "Purchase restore failed: " + billingResult.getDebugMessage());
                        restoreWhenConnected = true;
                        completePurchasesQueryCallbacks(billingResult, Collections.emptyList());
                        notifyError(billingResult);
                        return;
                    }
                    purchaseCache.clear();
                    if (purchases != null) mergePurchases(purchases);
                    restoreWhenConnected = false;
                    notifyPurchasesUpdated();
                    completePurchasesQueryCallbacks(billingResult, getCachedPurchases());
                }
        );
    }

    private void completePurchasesQueryCallbacks(
            @NonNull BillingResult billingResult,
            @NonNull List<Purchase> purchases
    ) {
        if (purchasesQueryCallbacks.isEmpty()) return;
        List<OnPurchasesQueryCallback> callbacks = new ArrayList<>(purchasesQueryCallbacks);
        purchasesQueryCallbacks.clear();
        for (OnPurchasesQueryCallback callback : callbacks) {
            callback.onPurchasesQueryComplete(billingResult, purchases);
        }
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
