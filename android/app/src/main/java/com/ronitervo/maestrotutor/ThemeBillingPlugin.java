// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import android.app.Activity;
import android.util.Log;

import androidx.annotation.NonNull;

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
 * Capacitor plugin that exposes ThemeBillingManager to JavaScript.
 *
 * <p>The native implementation remains backward compatible with the existing
 * theme store while also surfacing generic purchase records for managed
 * consumable products.
 */
@CapacitorPlugin(name = "ThemeBilling")
public class ThemeBillingPlugin extends Plugin {

    private static final String TAG = "ThemeBillingPlugin";

    private ThemeBillingManager billingManager;

    @Override
    public void load() {
        billingManager = new ThemeBillingManager(getContext());

        billingManager.setOnPurchasesUpdatedCallback((ownedProductIds, purchases) -> {
            JSObject data = new JSObject();
            JSArray ownedIds = new JSArray();
            for (String productId : ownedProductIds) {
                ownedIds.put(productId);
            }
            data.put("ownedProductIds", ownedIds);
            data.put("purchases", purchasesToJson(purchases));
            notifyListeners("purchasesUpdated", data);
        });

        billingManager.setOnProductDetailsCallback(productDetailsList -> {
            JSObject data = new JSObject();
            data.put("products", productDetailsToJson(productDetailsList));
            notifyListeners("productDetailsAvailable", data);
        });

        billingManager.setOnBillingErrorCallback((responseCode, debugMessage) -> {
            JSObject data = new JSObject();
            data.put("responseCode", responseCode);
            data.put("debugMessage", debugMessage != null ? debugMessage : "");
            notifyListeners("billingError", data);
        });

        billingManager.startConnection();
    }

    @Override
    protected void handleOnDestroy() {
        if (billingManager != null) {
            billingManager.endConnection();
        }
        super.handleOnDestroy();
    }

    @PluginMethod
    public void startConnection(PluginCall call) {
        billingManager.startConnection();
        call.resolve();
    }

    @PluginMethod
    public void getProductDetails(PluginCall call) {
        billingManager.queryProductDetails(readProductIds(call));
        call.resolve();
    }

    @PluginMethod
    public void purchaseTheme(PluginCall call) {
        String productId = call.getString("productId");
        if (productId == null || productId.isEmpty()) {
            call.reject("productId is required");
            return;
        }

        Activity activity = getActivity();
        if (activity == null) {
            call.reject("No foreground activity available");
            return;
        }

        billingManager.launchBillingFlow(activity, productId);
        call.resolve();
    }

    @PluginMethod
    public void restorePurchases(PluginCall call) {
        billingManager.restorePurchases();
        call.resolve();
    }

    @PluginMethod
    public void consumePurchase(PluginCall call) {
        String purchaseToken = call.getString("purchaseToken");
        if (purchaseToken == null || purchaseToken.isEmpty()) {
            call.reject("purchaseToken is required");
            return;
        }

        billingManager.consumePurchase(purchaseToken, (success, responseCode, debugMessage) -> {
            if (success) {
                call.resolve();
                return;
            }
            call.reject(
                    debugMessage != null && !debugMessage.isEmpty() ? debugMessage : "Consume failed",
                    String.valueOf(responseCode)
            );
        });
    }

    @PluginMethod
    public void isThemeOwned(PluginCall call) {
        String productId = call.getString("productId");
        if (productId == null || productId.isEmpty()) {
            call.reject("productId is required");
            return;
        }

        JSObject result = new JSObject();
        result.put("owned", billingManager.isThemeOwned(productId));
        call.resolve(result);
    }

    @PluginMethod
    public void getOwnedThemes(PluginCall call) {
        call.resolve(buildOwnedPurchasesResult());
    }

    @PluginMethod
    public void getOwnedPurchases(PluginCall call) {
        call.resolve(buildOwnedPurchasesResult());
    }

    @NonNull
    private JSObject buildOwnedPurchasesResult() {
        JSArray ownedIds = new JSArray();
        for (String productId : billingManager.getOwnedProductIds()) {
            ownedIds.put(productId);
        }

        JSObject result = new JSObject();
        result.put("ownedProductIds", ownedIds);
        result.put("purchases", purchasesToJson(billingManager.getLatestPurchases()));
        return result;
    }

    @NonNull
    private List<String> readProductIds(PluginCall call) {
        JSArray rawProductIds = call.getArray("productIds");
        if (rawProductIds == null || rawProductIds.length() == 0) {
            return ThemeProducts.ALL_PRODUCT_IDS;
        }

        List<String> productIds = new ArrayList<>();
        for (int index = 0; index < rawProductIds.length(); index++) {
            String productId = rawProductIds.optString(index, "");
            if (productId != null && !productId.isEmpty()) {
                productIds.add(productId);
            }
        }

        if (productIds.isEmpty()) {
            return ThemeProducts.ALL_PRODUCT_IDS;
        }

        return productIds;
    }

    private JSArray productDetailsToJson(List<ProductDetails> products) {
        JSArray array = new JSArray();
        if (products == null) return array;

        for (ProductDetails productDetails : products) {
            JSObject object = new JSObject();
            object.put("productId", productDetails.getProductId());
            object.put("title", productDetails.getTitle());
            object.put("description", productDetails.getDescription());

            ProductDetails.OneTimePurchaseOfferDetails offerDetails =
                    productDetails.getOneTimePurchaseOfferDetails();
            if (offerDetails != null) {
                object.put("formattedPrice", offerDetails.getFormattedPrice());
                object.put("priceAmountMicros", offerDetails.getPriceAmountMicros());
                object.put("priceCurrencyCode", offerDetails.getPriceCurrencyCode());
            }

            try {
                array.put(object);
            } catch (Exception exception) {
                Log.w(TAG, "Failed to add product to JSON array", exception);
            }
        }

        return array;
    }

    private JSArray purchasesToJson(List<Purchase> purchases) {
        JSArray array = new JSArray();
        if (purchases == null) return array;

        for (Purchase purchase : purchases) {
            List<String> productIds = purchase.getProducts();
            if (productIds == null || productIds.isEmpty()) {
                continue;
            }

            for (String productId : productIds) {
                JSObject object = new JSObject();
                object.put("productId", productId);
                object.put("purchaseToken", purchase.getPurchaseToken());
                object.put("packageName", getContext() != null ? getContext().getPackageName() : "");
                object.put("orderId", purchase.getOrderId());
                object.put("purchaseTime", purchase.getPurchaseTime());
                object.put("purchaseState", purchaseStateToString(purchase.getPurchaseState()));
                object.put("acknowledged", purchase.isAcknowledged());

                try {
                    array.put(object);
                } catch (Exception exception) {
                    Log.w(TAG, "Failed to add purchase to JSON array", exception);
                }
            }
        }

        return array;
    }

    @NonNull
    private String purchaseStateToString(int purchaseState) {
        if (purchaseState == Purchase.PurchaseState.PURCHASED) {
            return "purchased";
        }
        if (purchaseState == Purchase.PurchaseState.PENDING) {
            return "pending";
        }
        return "unspecified";
    }
}
