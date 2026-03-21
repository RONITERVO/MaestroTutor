// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import android.app.Activity;
import android.util.Log;

import com.android.billingclient.api.ProductDetails;
import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import java.util.List;

/**
 * Capacitor plugin that exposes {@link ThemeBillingManager} to the JavaScript layer.
 *
 * <h3>Exposed methods (callable from TypeScript via {@code Capacitor.Plugins.ThemeBilling}):</h3>
 * <ul>
 *   <li>{@code startConnection()} – Initialise the billing client and restore purchases.</li>
 *   <li>{@code getProductDetails()} – Return available theme product details (id, title, price).</li>
 *   <li>{@code purchaseTheme({ productId })} – Launch the billing flow for a theme.</li>
 *   <li>{@code restorePurchases()} – Re-query Google Play and refresh the local cache.</li>
 *   <li>{@code isThemeOwned({ productId })} – Check the local cache for a specific theme.</li>
 *   <li>{@code getOwnedThemes()} – Return all locally cached owned theme IDs.</li>
 * </ul>
 *
 * <h3>Emitted events (received via {@code addListener} in TypeScript):</h3>
 * <ul>
 *   <li>{@code purchasesUpdated} – Fired whenever owned themes change; payload: {@code { ownedProductIds: string[] }}.</li>
 *   <li>{@code billingError} – Fired on billing errors; payload: {@code { responseCode: number, debugMessage: string }}.</li>
 *   <li>{@code productDetailsAvailable} – Fired when product details are ready.</li>
 * </ul>
 */
@CapacitorPlugin(name = "ThemeBilling")
public class ThemeBillingPlugin extends Plugin {

    private static final String TAG = "ThemeBillingPlugin";

    private ThemeBillingManager billingManager;

    // ------------------------------------------------------------------ //
    //  Plugin lifecycle                                                     //
    // ------------------------------------------------------------------ //

    @Override
    public void load() {
        billingManager = new ThemeBillingManager(getContext());

        billingManager.setOnPurchasesUpdatedCallback(ownedProductIds -> {
            JSObject data = new JSObject();
            JSArray arr = new JSArray();
            for (String id : ownedProductIds) {
                arr.put(id);
            }
            data.put("ownedProductIds", arr);
            notifyListeners("purchasesUpdated", data);
        });

        billingManager.setOnProductDetailsCallback(productDetailsList -> {
            JSObject data = new JSObject();
            JSArray arr = productDetailsToJson(productDetailsList);
            data.put("products", arr);
            notifyListeners("productDetailsAvailable", data);
        });

        billingManager.setOnBillingErrorCallback((responseCode, debugMessage) -> {
            JSObject data = new JSObject();
            data.put("responseCode", responseCode);
            data.put("debugMessage", debugMessage != null ? debugMessage : "");
            notifyListeners("billingError", data);
        });

        // Eagerly connect so purchases are restored as soon as the plugin loads.
        billingManager.startConnection();
    }

    @Override
    protected void handleOnDestroy() {
        if (billingManager != null) {
            billingManager.endConnection();
        }
        super.handleOnDestroy();
    }

    // ------------------------------------------------------------------ //
    //  Plugin methods                                                       //
    // ------------------------------------------------------------------ //

    /**
     * Initialises the billing client. Safe to call multiple times.
     * Automatically triggers purchase restoration on successful connection.
     */
    @PluginMethod
    public void startConnection(PluginCall call) {
        billingManager.startConnection();
        call.resolve();
    }

    /**
     * Returns cached product details as a JSON array.
     * If the cache is empty, triggers an async query; results arrive via the
     * {@code productDetailsAvailable} event.
     */
    @PluginMethod
    public void getProductDetails(PluginCall call) {
        billingManager.queryProductDetails();
        call.resolve();
    }

    /**
     * Launches the Google Play purchase sheet for the given theme product.
     *
     * <p>Expected call data: {@code { productId: string }}
     */
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

    /**
     * Re-queries Google Play for all owned purchases and rebuilds the local cache.
     * Results arrive via the {@code purchasesUpdated} event.
     */
    @PluginMethod
    public void restorePurchases(PluginCall call) {
        billingManager.restorePurchases();
        call.resolve();
    }

    /**
     * Checks whether a specific theme is owned according to the local cache.
     *
     * <p>Expected call data: {@code { productId: string }}
     * <p>Returns: {@code { owned: boolean }}
     */
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

    /**
     * Returns all theme product IDs that are owned according to the local cache.
     *
     * <p>Returns: {@code { ownedProductIds: string[] }}
     */
    @PluginMethod
    public void getOwnedThemes(PluginCall call) {
        JSArray arr = new JSArray();
        for (String productId : ThemeProducts.ALL_PRODUCT_IDS) {
            if (billingManager.isThemeOwned(productId)) {
                arr.put(productId);
            }
        }
        JSObject result = new JSObject();
        result.put("ownedProductIds", arr);
        call.resolve(result);
    }

    // ------------------------------------------------------------------ //
    //  Private helpers                                                      //
    // ------------------------------------------------------------------ //

    private JSArray productDetailsToJson(List<ProductDetails> products) {
        JSArray arr = new JSArray();
        if (products == null) return arr;
        for (ProductDetails pd : products) {
            JSObject obj = new JSObject();
            obj.put("productId", pd.getProductId());
            obj.put("title", pd.getTitle());
            obj.put("description", pd.getDescription());

            // One-time purchase offer details
            ProductDetails.OneTimePurchaseOfferDetails offerDetails =
                    pd.getOneTimePurchaseOfferDetails();
            if (offerDetails != null) {
                obj.put("formattedPrice", offerDetails.getFormattedPrice());
                obj.put("priceAmountMicros", offerDetails.getPriceAmountMicros());
                obj.put("priceCurrencyCode", offerDetails.getPriceCurrencyCode());
            }
            try {
                arr.put(obj);
            } catch (Exception e) {
                Log.w(TAG, "Failed to add product to JSON array", e);
            }
        }
        return arr;
    }
}
