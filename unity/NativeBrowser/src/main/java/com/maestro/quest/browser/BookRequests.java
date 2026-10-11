// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.Manifest;
import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.webkit.PermissionRequest;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.MimeTypeMap;
import java.io.IOException;
import java.util.*;
import java.util.concurrent.*;

/** Main-thread ownership of permission/file callbacks across native dialogs and browser replacement. */
final class BookRequests implements AutoCloseable {
    interface Host {
        Activity activity();
        boolean active();
        Object document();
        void askMicrophone();
        void pickFiles(Intent intent);
        void checkFileGesture(ValueCallback<Boolean> result);
        void report(String message);
    }
    private final Host host;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final ExecutorService worker = Executors.newSingleThreadExecutor(r -> { Thread t = new Thread(r,"Maestro selected files"); t.setDaemon(true); return t; });
    private PermissionRequest microphone;
    private Object microphoneDocument;
    private boolean permissionDialog, closed;
    private ValueCallback<Uri[]> fileCallback;
    private Object fileDocument;
    private boolean multiple;
    private int selectionEpoch;
    private Uri[] readyFiles;
    private volatile SelectedFiles files;
    private Future<?> copying;
    private final Runnable fileTimeout;
    BookRequests(Host host) { this.host = host; fileTimeout = () -> { cancelFiles(); host.report("File selection timed out. Try selecting the file again."); }; }
    static boolean appOrigin(Uri uri) { return uri != null && "https".equals(uri.getScheme()) && "appassets.androidplatform.net".equals(uri.getHost()) && (uri.getPort() == -1 || uri.getPort() == 443) && uri.getUserInfo() == null; }
    void requestMicrophone(PermissionRequest request) {
        String[] resources = request.getResources();
        if (closed || !host.active() || !appOrigin(request.getOrigin()) || resources.length != 1 || !PermissionRequest.RESOURCE_AUDIO_CAPTURE.equals(resources[0])) { request.deny(); return; }
        if (host.activity().checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) { request.grant(new String[] { PermissionRequest.RESOURCE_AUDIO_CAPTURE }); return; }
        if (permissionDialog || fileCallback != null) { request.deny(); return; }
        microphone = request; microphoneDocument = host.document(); permissionDialog = true;
        try { host.askMicrophone(); }
        catch (RuntimeException failure) { permissionDialog = false; denyMicrophone(); host.report("Microphone permission could not open. Try again from the book."); }
    }
    void cancelMicrophone(PermissionRequest request) { if (microphone == request) { microphone = null; microphoneDocument = null; } }
    void microphoneResult() {
        permissionDialog = false;
        boolean granted = host.activity().checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED;
        PermissionRequest request = microphone; Object owner = microphoneDocument; microphone = null; microphoneDocument = null;
        if (request != null) {
            if (!closed && granted && host.active() && host.document() == owner) request.grant(new String[] { PermissionRequest.RESOURCE_AUDIO_CAPTURE });
            else request.deny();
        }
        if (!closed) host.report(granted ? (request == null || !host.active() ? "Microphone allowed. Resume audio with the bell, then try speaking again." : "Microphone allowed") : "Microphone not allowed. You can still type. Change microphone permission in Quest app settings to use your voice.");
    }
    private void denyMicrophone() { PermissionRequest request = microphone; microphone = null; microphoneDocument = null; if (request != null) request.deny(); }
    void suspended() { denyMicrophone(); }
    void resumed() {
        if (readyFiles != null && host.active()) {
            Uri[] result = readyFiles; readyFiles = null;
            if (!closed && host.document() == fileDocument) completeFiles(result);
            else { releaseFiles(result); cancelFiles(); }
        }
    }
    boolean chooseFiles(ValueCallback<Uri[]> callback, WebChromeClient.FileChooserParams parameters) {
        int mode = parameters.getMode();
        if (closed || !host.active() || fileCallback != null || permissionDialog || (mode != WebChromeClient.FileChooserParams.MODE_OPEN && mode != WebChromeClient.FileChooserParams.MODE_OPEN_MULTIPLE)) { callback.onReceiveValue(null); return true; }
        fileCallback = callback; fileDocument = host.document(); multiple = mode == WebChromeClient.FileChooserParams.MODE_OPEN_MULTIPLE;
        final int epoch = ++selectionEpoch;
        handler.postDelayed(fileTimeout,300000);
        final Intent intent = pickerIntent(parameters.getAcceptTypes(),multiple);
        host.checkFileGesture(allowed -> {
            if (closed || epoch != selectionEpoch) return;
            if (!Boolean.TRUE.equals(allowed) || !host.active() || host.document() != fileDocument) { cancelFiles(); return; }
            try { host.pickFiles(intent); }
            catch (RuntimeException failure) { cancelFiles(); host.report("The file picker is unavailable on this device."); }
        });
        return true;
    }
    static Intent pickerIntent(String[] accepted, boolean multiple) {
        LinkedHashSet<String> types = new LinkedHashSet<>();
        boolean unknownExtension = false;
        if (accepted != null) for (String values : accepted) for (String raw : values.split(",")) {
            String type = raw.trim().toLowerCase(Locale.ROOT);
            if (type.startsWith(".")) { type = type.equals(".vrm") ? null : MimeTypeMap.getSingleton().getMimeTypeFromExtension(type.substring(1)); if (type == null) unknownExtension = true; }
            if (type != null && type.matches("[a-z0-9.+-]+/[a-z0-9.+*-]+")) types.add(type);
        }
        if (unknownExtension) types.clear(); // Android cannot filter extensions such as NDJSON/VRM reliably.
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        intent.setType(types.size() == 1 ? types.iterator().next() : "*/*");
        if (types.size() > 1) intent.putExtra(Intent.EXTRA_MIME_TYPES,types.toArray(new String[0]));
        intent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE,multiple); return intent;
    }
    void fileResult(int code, Intent data) {
        if (fileCallback == null || closed || copying != null) return;
        if (code != Activity.RESULT_OK || data == null || host.document() != fileDocument) { cancelFiles(); return; }
        List<Uri> selected = new ArrayList<>(); ClipData clip = data.getClipData();
        if (clip != null) { for (int i = 0; i < clip.getItemCount() && i <= SelectedFiles.MAX_FILES; i++) selected.add(clip.getItemAt(i).getUri()); }
        else if (data.getData() != null) selected.add(data.getData());
        if (selected.isEmpty() || selected.size() > SelectedFiles.MAX_FILES || (!multiple && selected.size() > 1)) { cancelFiles(); host.report("Select up to eight files, or one file for a single-file input."); return; }
        final int epoch = selectionEpoch;
        copying = worker.submit(() -> {
            Uri[] result = null; String message = null;
            try { if (files == null) files = new SelectedFiles(host.activity()); result = files.copy(selected.toArray(new Uri[0])); }
            catch (IOException | RuntimeException failure) { message = failure instanceof SelectedFiles.SelectionException ? failure.getMessage() : "The selected file could not be read"; }
            final Uri[] outcome = result; final String failureMessage = message;
            handler.post(() -> {
                if (closed || epoch != selectionEpoch || host.document() != fileDocument) { releaseFiles(outcome); return; }
                copying = null;
                if (outcome == null) { cancelFiles(); host.report(failureMessage == null ? "File selection failed" : failureMessage); }
                else { readyFiles = outcome; resumed(); }
            });
        });
    }
    private void completeFiles(Uri[] value) { handler.removeCallbacks(fileTimeout); ValueCallback<Uri[]> callback = fileCallback; fileCallback = null; fileDocument = null; if (callback != null) callback.onReceiveValue(value); }
    private void cancelFiles() {
        selectionEpoch++; if (copying != null) copying.cancel(true); copying = null; if (files != null) files.cancelCopy();
        Uri[] pending = readyFiles; readyFiles = null; releaseFiles(pending);
        completeFiles(null);
    }
    private void releaseFiles(Uri[] values) { if (values != null && !worker.isShutdown()) worker.execute(() -> { if (files != null) files.release(values); }); }
    @Override public void close() {
        if (closed) return; closed = true; denyMicrophone(); cancelFiles();
        worker.execute(() -> { if (files != null) files.close(); }); worker.shutdown();
    }
}
