// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import android.app.Activity;

import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import java.util.ArrayList;
import java.util.List;

/**
 * Capacitor transport for backend-managed Google Play products.
 *
 * <p>The plugin exposes catalogue lookup, purchase launch, and raw purchase
 * records. It never grants access, acknowledges, or consumes a purchase; those
 * decisions belong to the authenticated backend.</p>
 */
@CapacitorPlugin(name = "ManagedBilling")
public final class ManagedBillingPlugin extends Plugin {

    private ManagedBillingManager billingManager;

    @Override
    public void load() {
        billingManager = new ManagedBillingManager(getContext());
        billingManager.setOnPurchasesUpdatedCallback(purchases ->
                notifyListeners("purchasesUpdated", purchasesPayload(purchases)));
        billingManager.setOnProductDetailsCallback(products -> {
            JSObject payload = new JSObject();
            payload.put("products", productDetailsToJson(products));
            notifyListeners("productDetailsAvailable", payload);
        });
        billingManager.setOnBillingErrorCallback((responseCode, debugMessage) -> {
            JSObject payload = new JSObject();
            payload.put("responseCode", responseCode);
            payload.put("debugMessage", debugMessage == null ? "" : debugMessage);
            payload.put("canceled", responseCode == BillingClient.BillingResponseCode.USER_CANCELED);
            notifyListeners("billingError", payload);
        });
        billingManager.startConnection();
    }

    @Override
    protected void handleOnDestroy() {
        if (billingManager != null) billingManager.endConnection();
        super.handleOnDestroy();
    }

    @PluginMethod
    public void startConnection(PluginCall call) {
        billingManager.startConnection();
        call.resolve();
    }

    @PluginMethod
    public void getProductDetails(PluginCall call) {
        JSArray productIds = call.getArray("productIds");
        if (productIds == null || productIds.length() == 0) {
            call.reject("productIds is required");
            return;
        }

        List<String> ids = new ArrayList<>();
        for (int index = 0; index < productIds.length(); index += 1) {
            String value = productIds.optString(index, "").trim();
            if (!value.isEmpty()) ids.add(value);
        }
        if (ids.isEmpty()) {
            call.reject("productIds must contain at least one product ID");
            return;
        }
        billingManager.queryProductDetails(ids);
        call.resolve();
    }

    @PluginMethod
    public void purchaseProduct(PluginCall call) {
        String productId = call.getString("productId", "").trim();
        String obfuscatedAccountId = call.getString("obfuscatedAccountId", "").trim();
        if (productId.isEmpty()) {
            call.reject("productId is required");
            return;
        }
        if (obfuscatedAccountId.isEmpty()) {
            call.reject("obfuscatedAccountId is required");
            return;
        }

        Activity activity = getActivity();
        if (activity == null) {
            call.reject("No foreground activity available");
            return;
        }
        billingManager.launchBillingFlow(activity, productId, obfuscatedAccountId);
        call.resolve();
    }

    @PluginMethod
    public void restorePurchases(PluginCall call) {
        billingManager.restorePurchases((billingResult, purchases) -> {
            if (billingResult.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                call.resolve();
            } else {
                call.reject(billingResult.getDebugMessage());
            }
        });
    }

    @PluginMethod
    public void getUnconsumedPurchases(PluginCall call) {
        billingManager.restorePurchases((billingResult, purchases) -> {
            if (billingResult.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                call.resolve(purchasesPayload(purchases));
            } else {
                call.reject(billingResult.getDebugMessage());
            }
        });
    }

    private JSObject purchasesPayload(List<Purchase> purchases) {
        JSObject payload = new JSObject();
        JSArray records = new JSArray();

        if (purchases != null) {
            for (Purchase purchase : purchases) {
                String state = purchaseState(purchase);
                for (String productId : purchase.getProducts()) {
                    JSObject record = new JSObject();
                    record.put("productId", productId);
                    record.put("purchaseToken", purchase.getPurchaseToken());
                    record.put("packageName", purchase.getPackageName());
                    record.put("orderId", purchase.getOrderId());
                    record.put("purchaseTime", purchase.getPurchaseTime());
                    record.put("purchaseState", state);
                    record.put("acknowledged", purchase.isAcknowledged());
                    records.put(record);
                }
            }
        }

        payload.put("purchases", records);
        return payload;
    }

    private JSArray productDetailsToJson(List<ProductDetails> products) {
        JSArray records = new JSArray();
        if (products == null) return records;

        for (ProductDetails product : products) {
            JSObject record = new JSObject();
            record.put("productId", product.getProductId());
            record.put("title", product.getTitle());
            record.put("description", product.getDescription());
            ProductDetails.OneTimePurchaseOfferDetails offer =
                    product.getOneTimePurchaseOfferDetails();
            if (offer != null) {
                record.put("formattedPrice", offer.getFormattedPrice());
                record.put("priceAmountMicros", offer.getPriceAmountMicros());
                record.put("priceCurrencyCode", offer.getPriceCurrencyCode());
            }
            records.put(record);
        }
        return records;
    }

    private String purchaseState(Purchase purchase) {
        if (purchase.getPurchaseState() == Purchase.PurchaseState.PURCHASED) return "purchased";
        if (purchase.getPurchaseState() == Purchase.PurchaseState.PENDING) return "pending";
        return "unspecified";
    }
}
