// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.os.Handler;
import android.os.Looper;
import android.webkit.ValueCallback;
import org.json.JSONObject;
import org.json.JSONTokener;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** UI-owned polling; all storage work stays on one document-owned worker. */
final class BookExports implements AutoCloseable {
    interface Host { boolean active(); void evaluate(String script, ValueCallback<String> result); }
    private final Host host;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final ExecutorService worker;
    private final BookExportSession session;
    private boolean closed, polling;
    private long epoch;
    private final Runnable timeout = this::expire;
    private void expire() { if (!closed) worker.execute(session::expire); }
    BookExports(Host host, BookExportSession.Storage storage) { this(host,storage,Executors.newSingleThreadExecutor()); }
    BookExports(Host host, BookExportSession.Storage storage, ExecutorService worker) { this.host = host; this.worker = worker; session = new BookExportSession(storage); }
    void poll() {
        if (closed || polling || !host.active()) return;
        polling = true;
        final long token = ++epoch;
        Runnable queryTimeout = () -> { if (!closed && epoch == token) { epoch++; polling = false; } };
        main.postDelayed(queryTimeout,10000);
        host.evaluate("window.maestroBook && window.maestroBook.fileExportPoll ? JSON.stringify(window.maestroBook.fileExportPoll()) : null", value -> {
            if (closed || epoch != token) return;
            main.removeCallbacks(queryTimeout);
            if (!host.active()) { polling = false; return; }
            String raw = null;
            try { Object decoded = new JSONTokener(value).nextValue(); if (decoded instanceof String && !decoded.equals("null")) raw = (String)decoded; } catch (Exception ignored) { }
            if (raw == null) { polling = false; return; }
            final String request = raw;
            main.removeCallbacks(timeout); main.postDelayed(timeout,60000);
            worker.execute(() -> {
                JSONObject result = session.handle(request);
                main.post(() -> {
                    if (closed || epoch != token) return;
                    if (!host.active()) { polling = false; return; }
                    if (result == null) { polling = false; return; }
                    // Do not depend on an acknowledgement callback from a stalled renderer.
                    // A repeated poll replays the cached response without writing twice.
                    host.evaluate("window.maestroBook && window.maestroBook.fileExportResult(" + result.toString() + ")", ignored -> { });
                    polling = false;
                    if (!closed && host.active()) main.postDelayed(this::poll,25);
                });
            });
        });
    }
    @Override public void close() {
        if (closed) return;
        closed = true; epoch++; main.removeCallbacksAndMessages(null);
        worker.execute(session::close); worker.shutdown();
    }
}
