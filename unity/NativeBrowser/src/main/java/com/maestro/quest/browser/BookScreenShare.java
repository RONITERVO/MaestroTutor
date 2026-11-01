// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.content.Intent;
import org.json.JSONObject;

/**
 * A system approval is not a running capture. Keep an unused approval briefly so
 * the learner can resume the book and select this source again after the dialog.
 * Neither a permission result nor foregrounding the app starts a capture.
 */
final class BookScreenShare {
    interface Host {
        boolean active();
        void prompt();
        Capture capture(Intent consent);
    }
    interface Capture {
        String read();
        void stop();
    }
    interface Clock { long now(); }
    private final Host host;
    private final Clock clock;
    private String context = "", error;
    private Intent consent;
    private long requestedAt, grantedAt;
    private boolean prompting, closed;
    private Capture capture;
    BookScreenShare(Host host, Clock clock) { this.host=host; this.clock=clock; }
    synchronized void context(String value) {
        value = value == null ? "" : value;
        if (context.equals(value)) return;
        reset(); context=value;
    }
    synchronized void start() {
        stopCapture();
        if (closed || context.isEmpty() || !host.active()) { error="screen-share-unavailable"; return; }
        long now=clock.now();
        if (consent != null && now-grantedAt <= 60000) {
            Intent once=consent; consent=null; // Consumed even if service startup fails.
            try { capture=host.capture(once); error=null; }
            catch (RuntimeException failure) { error="screen-share-unavailable"; }
            return;
        }
        consent=null;
        error="screen-share-consent";
        if (prompting) return;
        prompting=true; requestedAt=now;
        try { host.prompt(); }
        catch (RuntimeException failure) { prompting=false; error="screen-share-unavailable"; }
    }
    synchronized void result(int result, Intent data) {
        if (closed || !prompting) return;
        prompting=false;
        if (context.isEmpty() || clock.now()-requestedAt > 120000 || result!=Activity.RESULT_OK || data==null) {
            consent=null; error="screen-share-denied"; return;
        }
        consent=new Intent(data); grantedAt=clock.now(); error="screen-share-consent";
    }
    synchronized void stopCapture() {
        if (capture!=null) { capture.stop(); capture=null; }
        // Pending consent contains no frames and cannot start anything by itself.
    }
    synchronized String read() {
        if (capture!=null) return capture.read();
        try { return new JSONObject().put("error", error==null ? "screen-share-unavailable" : error).toString(); }
        catch (org.json.JSONException impossible) { throw new IllegalStateException(impossible); }
    }
    private void reset() { stopCapture(); consent=null; prompting=false; error=null; }
    synchronized void close() { reset(); context=""; closed=true; }
}
