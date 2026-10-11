// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import org.json.JSONObject;
import org.json.JSONTokener;
import java.util.UUID;

/** One in-flight UI-thread evaluation. Navigation invalidates queued work and replies. */
final class BookSpeechMailbox {
    private String document;
    private long poll;
    private boolean pending;
    private String value;
    BookSpeechMailbox() { invalidate(); }
    synchronized void invalidate() {
        document = UUID.randomUUID().toString().replace("-", ""); poll = 0; pending = false;
        value = "{\"document\":\"" + document + "\",\"poll\":0,\"payload\":null}";
    }
    synchronized String document() { return document; }
    synchronized boolean owns(String owner) { return document.equals(owner); }
    synchronized boolean begin(String owner) { if (!owns(owner) || pending) return false; pending = true; return true; }
    synchronized String read() { return value; }
    synchronized void complete(String owner, String encoded) {
        if (!owns(owner) || !pending) return;
        pending = false;
        try {
            if (encoded == null || encoded.length() > 110000) throw new IllegalArgumentException();
            Object decoded = new JSONTokener(encoded).nextValue();
            JSONObject payload = decoded instanceof String ? new JSONObject((String)decoded) : null;
            value = new JSONObject().put("document", document).put("poll", ++poll)
                .put("payload", payload == null ? JSONObject.NULL : payload).toString();
        } catch (Exception invalid) {
            value = "{\"document\":\"" + document + "\",\"poll\":" + (++poll) + ",\"payload\":null}";
        }
    }
    static String script(String status) {
        try {
            if (status == null || status.length() > 2048) return null;
            JSONObject value = new JSONObject(status);
            if (value.optInt("version") != 2) return null;
            return "window.maestroBook && window.maestroBook.speechExchange ? JSON.stringify(window.maestroBook.speechExchange("
                + value.toString() + ")) : ''";
        } catch (Exception invalid) { return null; }
    }
}
