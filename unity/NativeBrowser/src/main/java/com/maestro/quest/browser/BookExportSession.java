// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.util.Base64;
import org.json.JSONObject;
import java.io.IOException;
import java.util.HashSet;
import java.util.Set;

/** Sequential worker-owned export transaction. No request can name a filesystem path. */
final class BookExportSession implements AutoCloseable {
    static final int CHUNK_BYTES = 24576;
    static final long MAX_BYTES = 256L * 1024 * 1024;
    interface Sink { void write(byte[] value) throws IOException; String finish() throws IOException; void abort() throws IOException; }
    interface Storage { Sink open(String name, String mime) throws IOException; }
    private final Storage storage;
    private final Set<String> used = new HashSet<>();
    private Sink sink;
    private String active, lastRequest;
    private JSONObject lastResponse;
    private long bytes;
    private int next;
    private boolean closed;
    BookExportSession(Storage storage) { this.storage = storage; }

    static boolean validName(String name) {
        return name.length() > 0 && name.length() <= 128 && !name.matches(".*[\\\\/:*?\"<>|\\x00-\\x1f\\x7f-\\x9f].*") && name.matches("(?i).+\\.(ndjson|jsonl|json|txt)");
    }
    private static boolean fields(JSONObject value, String... names) {
        if (integer(value,"version",1) != 1 || value.length() != names.length + 1) return false;
        for (String name : names) if (!value.has(name)) return false;
        return true;
    }
    private static long integer(JSONObject value, String key, long maximum) {
        Object raw = value.opt(key);
        if (!(raw instanceof Integer || raw instanceof Long)) return -1;
        long number = ((Number)raw).longValue();
        return number >= 0 && number <= maximum ? number : -1;
    }
    private static String string(JSONObject value, String key) { Object raw = value.opt(key); return raw instanceof String ? (String)raw : ""; }
    private static JSONObject response(String id, int sequence, boolean ok, long bytes, String location, String error) {
        JSONObject result = new JSONObject();
        try {
            result.put("version",1).put("id", id).put("sequence", sequence).put("ok", ok).put("bytes", bytes);
            if (location != null) result.put("location", location);
            if (error != null) result.put("error", error);
        } catch (org.json.JSONException impossible) { throw new IllegalStateException(impossible); }
        return result;
    }
    JSONObject handle(String raw) {
        if (closed || raw == null || raw.length() > 34000) return null;
        if (raw.equals(lastRequest)) return lastResponse;
        JSONObject request;
        try { request = new JSONObject(raw); } catch (Exception invalid) { return null; }
        String id = string(request,"id"), operation = string(request,"operation");
        long seq = integer(request,"sequence", Integer.MAX_VALUE);
        if (!id.matches("[a-f0-9]{32}") || seq < 0) return null;
        // A stale callback cannot cancel another transaction.
        if (active != null && !active.equals(id)) return response(id,(int)seq,false,0,null,"Another export is active.");
        String location = null;
        try {
            if (operation.equals("open")) {
                String name = string(request,"name"), mime = string(request,"mime");
                if (active != null || seq != 0 || !fields(request,"id","sequence","operation","name","mime") || !validName(name) || !(mime.equals("application/x-ndjson") || mime.equals("application/json") || mime.equals("text/plain"))) throw new IOException("Invalid export start.");
                if (used.contains(id) || used.size() >= 64) throw new IOException("Reopen the book before starting another export.");
                used.add(id); bytes = 0;
                sink = storage.open(name,mime); active = id; next = 1;
            } else {
                if (sink == null || !id.equals(active) || seq != next || integer(request,"offset",MAX_BYTES) < 0) throw new IOException("Export sequence is no longer valid.");
                boolean chunk = operation.equals("chunk");
                if (!(chunk ? fields(request,"id","sequence","operation","offset","data") : fields(request,"id","sequence","operation","offset"))) throw new IOException("Invalid export fields.");
                // Abort accepts the sender's uncertain offset, but never publishes data.
                if (operation.equals("abort")) { discard(); }
                else {
                    if (integer(request,"offset",MAX_BYTES) != bytes) throw new IOException("Export byte count differs.");
                    if (chunk) {
                        String encoded = string(request,"data");
                        if (encoded.isEmpty() || encoded.length() > CHUNK_BYTES * 4 / 3 || !encoded.matches("[A-Za-z0-9+/]*={0,2}")) throw new IOException("Invalid export chunk.");
                        byte[] decoded;
                        try { decoded = Base64.decode(encoded, Base64.NO_WRAP); } catch (IllegalArgumentException invalid) { throw new IOException("Invalid export encoding."); }
                        if (decoded.length > CHUNK_BYTES || !encoded.equals(Base64.encodeToString(decoded,Base64.NO_WRAP)) || bytes + decoded.length > MAX_BYTES) throw new IOException("Export exceeds its size limit.");
                        sink.write(decoded); bytes += decoded.length;
                    } else if (operation.equals("finish")) {
                        location = sink.finish(); sink = null; active = null;
                    } else throw new IOException("Unknown export operation.");
                }
                next++;
            }
            lastResponse = response(id,(int)seq,true,bytes,location,null);
        } catch (Exception failure) {
            try { discard(); } catch (IOException ignored) { /* Pending entries remain hidden until Android expires them. */ }
            lastResponse = response(id,(int)seq,false,bytes,null,"Export failed; no complete file was confirmed. Check Downloads/Maestro, then save again.");
        }
        lastRequest = raw;
        return lastResponse;
    }
    private void discard() throws IOException {
        Sink previous = sink; sink = null; active = null;
        if (previous != null) previous.abort();
    }
    void expire() { try { discard(); } catch (IOException ignored) { } }
    @Override public void close() { closed = true; try { discard(); } catch (IOException ignored) { } }
}
