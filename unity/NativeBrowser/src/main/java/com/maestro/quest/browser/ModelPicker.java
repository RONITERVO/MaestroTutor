// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.provider.DocumentsContract;
import org.json.JSONObject;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Native-only picker used by solid room tools. Never exposed to page JavaScript. */
@SuppressWarnings("deprecation")
public final class ModelPicker extends Fragment {
    static final int REQUEST = 4810;
    private static ModelPicker current;
    private static volatile String result = "";
    private final Handler main = new Handler(Looper.getMainLooper());
    private final ExecutorService worker = Executors.newSingleThreadExecutor(r -> { Thread t = new Thread(r,"Maestro model copy"); t.setDaemon(true); return t; });
    private volatile SelectedFiles files;
    private volatile boolean closed;
    private boolean started, copying;
    private final Runnable timeout = () -> finish("", "", "Model selection timed out. Try importing again.");

    public static void Start(Activity activity) {
        activity.runOnUiThread(() -> {
            if (current != null || activity.isFinishing() || activity.isDestroyed()) return;
            result = ""; current = new ModelPicker();
            try { activity.getFragmentManager().beginTransaction().add(current,"MaestroModelPicker").commit(); }
            catch (RuntimeException error) { current.close(); current = null; publish("", "", "The document picker is unavailable. Try again after resuming Maestro."); }
        });
    }
    public static String ReadResult() { return result; }
    public static void Release() {
        new Handler(Looper.getMainLooper()).post(() -> {
            if (current != null) { ModelPicker previous = current; current = null; previous.close(); if (previous.isAdded()) previous.getFragmentManager().beginTransaction().remove(previous).commitAllowingStateLoss(); }
            result = "";
        });
    }
    static Intent selectionIntent() {
        // VRM providers disagree about MIME type; validate bytes after selection.
        // Quest's Downloads shortcut can omit USB-copied GLB/VRM files even when
        // indexed. Start in the actual storage folder through the normal picker.
        // This is only a location hint: the user still selects and grants one file.
        Uri downloads = DocumentsContract.buildDocumentUri("com.android.externalstorage.documents", "primary:" + Environment.DIRECTORY_DOWNLOADS);
        return new Intent(Intent.ACTION_OPEN_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType("*/*")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION).putExtra(DocumentsContract.EXTRA_INITIAL_URI, downloads);
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (current != this) { getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss(); return; }
        if (started) return; started = true;
        main.postDelayed(timeout,300000);
        try { startActivityForResult(selectionIntent(), REQUEST); }
        catch (RuntimeException error) { finish("", "", "No document picker is available. Add your model files with the headset's Files app first."); }
    }
    @Override public void onActivityResult(int request, int code, Intent data) {
        if (request != REQUEST || closed || copying || current != this || !result.isEmpty()) return;
        if (code != Activity.RESULT_OK || data == null || data.getData() == null || data.getClipData() != null && data.getClipData().getItemCount() > 1) { finish("", "", "Model selection cancelled."); return; }
        Uri source = data.getData(); Activity activity = getActivity(); if (activity == null) { finish("", "", "Resume Maestro and import again."); return; }
        copying = true; worker.execute(() -> {
            try {
                SelectedFiles cache = new SelectedFiles(activity); files = cache;
                if (closed) { cache.close(); return; }
                Uri selected = cache.copy(new Uri[] { source })[0];
                SelectedFiles.Entry entry = SelectedFiles.ENTRIES.get(selected.getLastPathSegment());
                if (entry == null) throw new IllegalStateException();
                main.post(() -> finish(entry.file.getAbsolutePath(), entry.name, ""));
            } catch (Exception error) {
                String message = error instanceof SelectedFiles.SelectionException ? error.getMessage() : "The selected model could not be copied. Try a local GLB or VRM file.";
                main.post(() -> finish("", "", message));
            }
        });
    }
    private void finish(String path, String name, String error) { if (closed || current != this || !result.isEmpty()) return; main.removeCallbacks(timeout); publish(path, name, error); }
    private static void publish(String path, String name, String error) {
        try { result = new JSONObject().put("path",path).put("name",name).put("error",error).toString(); }
        catch (Exception ignored) { result = "{\"error\":\"Model selection failed.\"}"; }
    }
    private void close() {
        if (closed) return; closed = true; main.removeCallbacks(timeout);
        if (files != null) files.cancelCopy();
        worker.execute(() -> { if (files != null) files.close(); }); worker.shutdown();
    }
    @Override public void onDestroy() { if (current == this) { current = null; publish("", "", "Model selection was interrupted. Try again."); } close(); super.onDestroy(); }
}
