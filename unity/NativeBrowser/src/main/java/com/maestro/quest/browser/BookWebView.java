// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Intent;
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
import android.webkit.ValueCallback;
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
    private final BrowserSnapshotStore roomSnapshots = new BrowserSnapshotStore(32768);
    private final BrowserSnapshotStore snapshots = new BrowserSnapshotStore();
    private final BookSpeechMailbox speech = new BookSpeechMailbox();
    private volatile String error = "";
    private volatile String externalLink = "";
    private volatile boolean disposed;
    private boolean suspended;
    private int lifecycleEpoch;
    private BookRequests requests, permissionOwner, pickerOwner;
    private BookExports exports;
    private static final int MICROPHONE_REQUEST = 4701, FILE_REQUEST = 4702;
    private final Handler lifecycleHandler = new Handler(Looper.getMainLooper());

    private static boolean isAppOrigin(Uri uri) {
        return BookRequests.appOrigin(uri);
    }

    private void resetRequests() {
        externalLink = "";
        if (requests != null) requests.close();
        final WebView owner = web;
        resetExports();
        requests = new BookRequests(new BookRequests.Host() {
            public Activity activity() { return UnityPlayer.currentActivity; }
            public boolean active() { return !disposed && !suspended && web == owner && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl())); }
            public Object document() { return web; }
            public void askMicrophone() {
                if (permissionOwner != null || pickerOwner != null) throw new IllegalStateException("A native dialog is already open");
                permissionOwner = requests;
                try { requestPermissions(new String[] { Manifest.permission.RECORD_AUDIO },MICROPHONE_REQUEST); }
                catch (RuntimeException failure) { permissionOwner = null; throw failure; }
            }
            public void pickFiles(Intent intent) {
                if (pickerOwner != null || permissionOwner != null) throw new IllegalStateException("A native dialog is already open");
                pickerOwner = requests;
                try { startActivityForResult(intent,FILE_REQUEST); }
                catch (RuntimeException failure) { pickerOwner = null; throw failure; }
            }
            public void checkFileGesture(ValueCallback<Boolean> result) {
                if (!active()) { result.onReceiveValue(false); return; }
                owner.evaluateJavascript("Boolean(window.maestroBook && window.maestroBook.takeFileSelection && window.maestroBook.takeFileSelection())", value -> result.onReceiveValue("true".equals(value)));
            }
            public void report(String message) { error = message; }
        });
    }

    private void resetExports() {
        if (exports != null) exports.close();
        final WebView owner = web;
        exports = new BookExports(new BookExports.Host() {
            public boolean active() { return !disposed && !suspended && web == owner && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl())); }
            public void evaluate(String script, ValueCallback<String> result) { if (active()) owner.evaluateJavascript(script,result); }
        }, new BookExportStorage(UnityPlayer.currentActivity.getContentResolver()));
    }

    @Override public void onRequestPermissionsResult(int code,String[] permissions,int[] results) {
        super.onRequestPermissionsResult(code,permissions,results);
        if (code == MICROPHONE_REQUEST) { BookRequests owner = permissionOwner; permissionOwner = null; if (owner != null) owner.microphoneResult(); }
    }

    @Override public void onActivityResult(int code,int result,Intent data) {
        super.onActivityResult(code,result,data);
        if (code == FILE_REQUEST) { BookRequests owner = pickerOwner; pickerOwner = null; if (owner != null) owner.fileResult(result,data); }
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
                @Override public void onPageStarted(WebView view,String url,android.graphics.Bitmap icon) { speech.invalidate(); snapshots.invalidate(); roomSnapshots.invalidate(); resetRequests(); }
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
                    if (!disposed && !suspended && view == web && request.isForMainFrame() && request.hasGesture() && "https".equals(request.getUrl().getScheme())) externalLink = request.getUrl().toString();
                    return true;
                }
                @Override public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError failure) { handler.cancel(); }
                @Override public void onPageFinished(WebView view, String url) {
                    if (suspended) suspendWebView();
                }
                @Override public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail) {
                    speech.invalidate();
                    error = "The book browser stopped. Reopen the book to recover your saved conversation.";
                    mInitialized = false;
                    if (requests != null) { requests.close(); requests = null; }
                    if (exports != null) { exports.close(); exports = null; }
                    if (view.getParent() instanceof ViewGroup) ((ViewGroup)view.getParent()).removeView(view);
                    view.destroy(); web = null; mView = null;
                    snapshots.invalidate(); roomSnapshots.invalidate();
                    return true;
                }
            });
            web.setWebChromeClient(new WebChromeClient() {
                @Override public void onPermissionRequest(PermissionRequest request) {
                    if (requests != null) requests.requestMicrophone(request); else request.deny();
                }
                @Override public void onPermissionRequestCanceled(PermissionRequest request) { if (requests != null) requests.cancelMicrophone(request); }
                @Override public boolean onShowFileChooser(WebView view,ValueCallback<Uri[]> callback,FileChooserParams parameters) {
                    if (requests == null) { callback.onReceiveValue(null); return true; }
                    return requests.chooseFiles(callback,parameters);
                }
            });
            web.setBackgroundColor(0xffFFF0D2);
            mCaptureLayout.addView(web, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            web.loadUrl(START);
            mInitialized = true;
    }

    private void destroyWebView() {
        speech.invalidate();
        if (exports != null) { exports.close(); exports = null; }
        if (requests != null) { requests.close(); requests = null; }
        if (web == null) return;
        web.stopLoading();
        if (web.getParent() instanceof ViewGroup) ((ViewGroup)web.getParent()).removeView(web);
        web.destroy(); web = null; mView = null; snapshots.invalidate(); roomSnapshots.invalidate();
    }

    public void RequestRoomAgentSnapshot() {
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (disposed || suspended || web == null || !isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) return;
            final WebView current = web;
            final long request = roomSnapshots.begin();
            if (request < 0) return;
            web.evaluateJavascript("window.maestroBook && window.maestroBook.roomSnapshot ? JSON.stringify(window.maestroBook.roomSnapshot()) : ''", result -> {
                if (disposed || suspended || current != web) return;
                try {
                    Object decoded = new JSONTokener(result).nextValue();
                    if (decoded instanceof String) roomSnapshots.publish(request, (String)decoded);
                } catch (Exception ignored) { roomSnapshots.publish(request, ""); }
            });
        });
    }

    public String ReadRoomAgentSnapshot() { return roomSnapshots.read(); }

    public void RequestSnapshot() {
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (disposed || suspended || web == null || !isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) return;
            final WebView current = web;
            if (exports != null) exports.poll();
            final long request = snapshots.begin();
            if (request < 0) return;
            web.evaluateJavascript("window.maestroBook ? JSON.stringify(window.maestroBook.snapshot()) : ''", result -> {
                if (disposed || suspended || current != web) return;
                try {
                    Object decoded = new JSONTokener(result).nextValue();
                    if (decoded instanceof String) snapshots.publish(request, (String)decoded);
                } catch (Exception ignored) { snapshots.publish(request, ""); }
            });
        });
    }

    public String ReadSnapshot() { return snapshots.read(); }
    public String ReadSpeechExchange() { return speech.read(); }
    public void RequestSpeechExchange(String document, String status) {
        final String script = BookSpeechMailbox.script(status);
        if (script == null || !speech.begin(document)) return;
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (!speech.owns(document)) return;
            if (disposed || suspended || web == null || !isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) {
                speech.complete(document, null); return;
            }
            final WebView owner = web;
            owner.evaluateJavascript(script, result -> {
                if (disposed || suspended || web != owner) return;
                speech.complete(document, result);
            });
        });
    }
    public String ReadError() { return error; }
    public void ClearError() { error = ""; }
    public String TakeExternalLink() { String result = externalLink; externalLink = ""; return result; }

    public void ExecuteBookCommand(String json) {
        if (json == null || json.length() > 4096) return;
        try {
            JSONObject command = new JSONObject(json);
            if (command.optInt("version") != 1) return;
            String type = command.optString("type");
            if (!(type.equals("history.step") || type.equals("history.latest") || type.equals("bookmark.jump") || type.equals("artifact.select") || type.equals("artifact.latest") || type.equals("layout.set") || type.equals("session.resume") || type.equals("workspace.open"))) return;
            String encoded = command.toString();
            UnityPlayer.currentActivity.runOnUiThread(() -> {
                if (web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript("window.maestroBook && window.maestroBook.command(" + encoded + ")", null);
            });
        } catch (Exception ignored) { /* Invalid commands have no effect. */ }
    }

    public void PublishRoomAgentState(String json) {
        String script = LibraryBookMessages.publishScript(json, "roomState");
        if (script == null) return;
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (!disposed && !suspended && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript(script, null);
        });
    }

    public void PublishCameraState(String json) {
        String script = LibraryBookMessages.publishScript(json, "cameraState");
        if (script == null) return;
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (!disposed && !suspended && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript(script, null);
        });
    }

    public void PublishRoomCapture(String json) {
        String script = LibraryBookMessages.publishScript(json, "roomCapture");
        if (script == null) return;
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (!disposed && !suspended && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript(script, null);
        });
    }

    public void PublishIntegrityResult(String json) {
        String script = LibraryBookMessages.publishScript(json, "integrityResult");
        if (script == null) return;
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (!disposed && !suspended && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript(script, null);
        });
    }

    public void PublishLibraryState(String json) {
        String script = LibraryBookMessages.publishScript(json);
        if (script == null) return;
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (!disposed && !suspended && web != null && isAppOrigin(Uri.parse(web.getUrl() == null ? "" : web.getUrl()))) web.evaluateJavascript(script, null);
        });
    }

    public void SetSuspended(boolean value) {
        speech.invalidate();
        snapshots.suspend(value); roomSnapshots.suspend(value);
        if (value) externalLink = "";
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            if (disposed) return;
            suspended = value;
            if (value) externalLink = "";
            lifecycleEpoch++;
            lifecycleHandler.removeCallbacksAndMessages(null);
            if (value) { suspendWebView(); return; }
            if (web == null) createWebView(UnityPlayer.currentActivity);
            web.resumeTimers(); web.onResume();
            web.evaluateJavascript("window.maestroBook && window.maestroBook.lifecycle(false)", null);
            if (requests != null) requests.resumed();
            resetExports();
        });
    }

    private void suspendWebView() {
        if (exports != null) { exports.close(); exports = null; }
        if (requests != null) requests.suspended();
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

    // Called only by the development-only Unity Operator tool. Never exposed to page JavaScript.
    public String RenderingDiagnostics(String mode) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null || (activity.getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) == 0
                || disposed || !(mViewToBufferRenderer instanceof com.tlab.viewtobuffer.ViewToHWBRenderer))
            return "{\"available\":false}";
        if ("newFrames".equals(mode)) mViewToBufferRenderer.setCopyOnNewFrame(true);
        else if ("continuous".equals(mode)) mViewToBufferRenderer.setCopyOnNewFrame(false);
        else if (!"observe".equals(mode)) return "{\"available\":false,\"error\":\"Unknown comparison mode\"}";
        long[] counts = mViewToBufferRenderer.frameCopyStatistics();
        try {
            JSONObject result = new JSONObject();
            result.put("available", true); result.put("atNanos", System.nanoTime());
            result.put("draws", counts[0]); result.put("receivedFrames", counts[1]); result.put("copies", counts[2]);
            result.put("copyOnNewFrame", counts[3] == 1); result.put("contentExists", counts[4] == 1);
            return result.toString();
        } catch (org.json.JSONException impossible) { throw new IllegalStateException(impossible); }
    }

    @Override public void Dispose() {
        if (disposed) return;
        disposed = true;
        speech.invalidate();
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
