// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.content.pm.ApplicationInfo;
import android.net.Uri;
import android.net.http.SslError;
import android.os.Handler;
import android.os.Looper;
import android.view.ViewGroup;
import android.webkit.CookieManager;
import android.webkit.PermissionRequest;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.SslErrorHandler;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import androidx.webkit.WebViewAssetLoader;
import com.tlab.webkit.chromium.OffscreenBrowser;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;
import org.json.JSONTokener;
import java.io.ByteArrayInputStream;
import java.util.Collections;

/** Only native code invokes these methods. No JavaScript interface is installed. */
public final class BookWebView extends OffscreenBrowser {
    private static final String HOST = "appassets.androidplatform.net";
    private static final String START = "https://" + HOST + "/index.html?surface=quest-book";
    private WebView web;
    private volatile String snapshot = "";
    private volatile String error = "";
    private volatile String externalLink = "";
    private volatile boolean disposed;
    private boolean suspended;
    private int lifecycleEpoch;
    private final Handler lifecycleHandler = new Handler(Looper.getMainLooper());

    private static boolean isAppOrigin(Uri uri) {
        return uri != null && "https".equals(uri.getScheme()) && HOST.equals(uri.getHost()) && (uri.getPort() == -1 || uri.getPort() == 443);
    }

    private static WebResourceResponse denied() {
        return new WebResourceResponse("text/plain", "UTF-8", 403, "Forbidden", Collections.emptyMap(), new ByteArrayInputStream(new byte[0]));
    }

    @SuppressLint("SetJavaScriptEnabled")
    public void InitBook(int viewWidth, int viewHeight, int texWidth, int texHeight, int screenWidth, boolean vulkan, int captureMode) {
        if (viewWidth < 2 || viewWidth > 4096 || viewHeight < 1 || viewHeight > 4096 || texWidth < 2 || texWidth > 4096 || texHeight < 1 || texHeight > 4096 || captureMode < 0 || captureMode >= CaptureMode.values().length) throw new IllegalArgumentException("Invalid book resolution");
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            if (disposed || web != null) return;
            activity.getFragmentManager().beginTransaction().add(this, "MaestroBookBrowser").commitAllowingStateLoss();
            initParam(viewWidth, viewHeight, texWidth, texHeight, screenWidth, 0, vulkan, CaptureMode.values()[captureMode]);
            init();
            createWebView(activity);
        });
    }

    private void createWebView(Activity activity) {
            // USB-only inspection is available for development APKs, never store builds.
            WebView.setWebContentsDebuggingEnabled((activity.getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0);
            web = new WebView(activity);
            mView = web;
            // Preserve the original app's root-relative fonts, workers and assets.
            WebViewAssetLoader.AssetsPathHandler bundled = new WebViewAssetLoader.AssetsPathHandler(activity);
            WebViewAssetLoader assets = new WebViewAssetLoader.Builder()
                .addPathHandler("/", path -> bundled.handle("maestro-web/" + path)).build();
            WebSettings settings = web.getSettings();
            settings.setJavaScriptEnabled(true);
            settings.setDomStorageEnabled(true);
            // This attached WebView lives outside the headset's Android window.
            // Keep its tiles rasterized for the hardware-buffer capture surface.
            settings.setOffscreenPreRaster(true);
            settings.setAllowFileAccess(false);
            settings.setAllowContentAccess(false);
            settings.setAllowFileAccessFromFileURLs(false);
            settings.setAllowUniversalAccessFromFileURLs(false);
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
            settings.setSafeBrowsingEnabled(true);
            settings.setCacheMode(WebSettings.LOAD_DEFAULT);
            settings.setDefaultTextEncodingName("UTF-8");
            settings.setSupportMultipleWindows(false);
            settings.setSupportZoom(false);
            settings.setMediaPlaybackRequiresUserGesture(true);
            CookieManager.getInstance().setAcceptThirdPartyCookies(web, false);
            web.setWebViewClient(new WebViewClient() {
                @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                    Uri uri = request.getUrl();
                    if (isAppOrigin(uri)) {
                        WebResourceResponse asset = assets.shouldInterceptRequest(uri);
                        return asset == null ? denied() : asset;
                    }
                    if (request.isForMainFrame() || !("https".equals(uri.getScheme()) || "data".equals(uri.getScheme()) || "blob".equals(uri.getScheme()))) return denied();
                    return null;
                }
                @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                    if (isAppOrigin(request.getUrl())) return false;
                    // No web-provided Android intents, file URLs or JS URLs are launched.
                    if (request.isForMainFrame() && request.hasGesture() && "https".equals(request.getUrl().getScheme())) externalLink = request.getUrl().toString();
                    return true;
                }
                @Override public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError failure) { handler.cancel(); }
                @Override public void onPageFinished(WebView view, String url) {
                    if (suspended) suspendWebView();
                }
                @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
                    error = "The book browser stopped. Reopen the book to recover your saved conversation.";
                    mInitialized = false;
                    if (view.getParent() instanceof ViewGroup) ((ViewGroup)view.getParent()).removeView(view);
                    view.destroy(); web = null; mView = null;
                    snapshot = "";
                    return true;
                }
            });
            web.setWebChromeClient(new WebChromeClient() {
                @Override public void onPermissionRequest(PermissionRequest request) {
                    boolean microphoneOnly = request.getResources().length == 1 && PermissionRequest.RESOURCE_AUDIO_CAPTURE.equals(request.getResources()[0]);
                    if (!suspended && isAppOrigin(request.getOrigin()) && microphoneOnly && activity.checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
                        request.grant(new String[] { PermissionRequest.RESOURCE_AUDIO_CAPTURE });
                    } else request.deny();
                }
            });
            web.setBackgroundColor(0xffFFF0D2);
            mCaptureLayout.addView(web, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            web.loadUrl(START);
            mInitialized = true;
    }

    private void destroyWebView() {
        if (web == null) return;
        web.stopLoading();
        if (web.getParent() instanceof ViewGroup) ((ViewGroup)web.getParent()).removeView(web);
        web.destroy(); web = null; mView = null; snapshot = "";
    }

    public void RequestSnapshot() {
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (web == null || !isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) return;
            web.evaluateJavascript("window.maestroBook ? JSON.stringify(window.maestroBook.snapshot()) : ''", result -> {
                try {
                    Object decoded = new JSONTokener(result).nextValue();
                    if (decoded instanceof String && ((String)decoded).length() <= 4096) snapshot = (String)decoded;
                } catch (Exception ignored) { snapshot = ""; }
            });
        });
    }

    public String ReadSnapshot() { return snapshot; }
    public String ReadError() { return error; }
    public String TakeExternalLink() { String result = externalLink; externalLink = ""; return result; }

    public void ExecuteBookCommand(String json) {
        if (json == null || json.length() > 4096) return;
        try {
            JSONObject command = new JSONObject(json);
            if (command.optInt("version") != 1) return;
            String type = command.optString("type");
            if (!(type.equals("history.step") || type.equals("history.latest") || type.equals("bookmark.jump") || type.equals("artifact.select") || type.equals("artifact.latest") || type.equals("layout.set") || type.equals("session.resume"))) return;
            String encoded = command.toString();
            UnityPlayer.currentActivity.runOnUiThread(() -> {
                if (web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript("window.maestroBook && window.maestroBook.command(" + encoded + ")", null);
            });
        } catch (Exception ignored) { /* Invalid commands have no effect. */ }
    }

    public void SetSuspended(boolean value) {
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (disposed) return;
            suspended = value;
            lifecycleEpoch++;
            lifecycleHandler.removeCallbacksAndMessages(null);
            if (value) { suspendWebView(); return; }
            if (web == null) createWebView(UnityPlayer.currentActivity);
            web.resumeTimers(); web.onResume();
            web.evaluateJavascript("window.maestroBook && window.maestroBook.lifecycle(false)", null);
        });
    }

    private void suspendWebView() {
        if (web == null || disposed) return;
        final WebView current = web;
        final int epoch = ++lifecycleEpoch;
        lifecycleHandler.removeCallbacksAndMessages(null);
        // Timers stay available until microphone, paid transports and playback
        // owners acknowledge shutdown. A stalled page cannot retain capture.
        lifecycleHandler.postDelayed(() -> {
            if (!suspended || disposed || epoch != lifecycleEpoch || current != web) return;
            error = "The book was reopened after an interrupted session. Saved conversations are retained.";
            destroyWebView();
        }, 1500);
        current.evaluateJavascript("window.maestroBook && window.maestroBook.lifecycle(true)", ignored -> pollShutdown(current, epoch));
    }

    private void pollShutdown(WebView current, int epoch) {
        if (!suspended || disposed || epoch != lifecycleEpoch || current != web) return;
        current.evaluateJavascript("window.maestroBook ? JSON.stringify(window.maestroBook.lifecycleState()) : ''", result -> {
            if (!suspended || disposed || epoch != lifecycleEpoch || current != web) return;
            boolean ready = false;
            try {
                Object decoded = new JSONTokener(result).nextValue();
                if (decoded instanceof String) {
                    JSONObject state = new JSONObject((String)decoded);
                    ready = state.optBoolean("suspended") && state.optBoolean("settled") && !state.optBoolean("active", true);
                }
            } catch (Exception ignored) { }
            if (ready) {
                lifecycleHandler.removeCallbacksAndMessages(null);
                current.onPause(); current.pauseTimers();
            } else lifecycleHandler.postDelayed(() -> pollShutdown(current, epoch), 50);
        });
    }

    @Override public void Dispose() {
        if (disposed) return;
        disposed = true;
        ReleaseSharedTexture();
        abortCaptureThread();
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            lifecycleEpoch++; lifecycleHandler.removeCallbacksAndMessages(null);
            destroyWebView();
            if (mRootLayout != null && mRootLayout.getParent() instanceof ViewGroup) ((ViewGroup)mRootLayout.getParent()).removeView(mRootLayout);
            if (mGlSurfaceView != null && mViewToBufferRenderer != null) mGlSurfaceView.queueEvent(() -> mViewToBufferRenderer.destroy());
            mView = null;
            activity.getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            mInitialized = false; mDisposed = true;
        });
    }
}
