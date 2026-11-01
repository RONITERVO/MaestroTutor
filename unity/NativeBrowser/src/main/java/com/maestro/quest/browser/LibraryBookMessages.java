// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import org.json.JSONObject;

final class LibraryBookMessages {
    private LibraryBookMessages() { }
    static String publishScript(String json) { return publishScript(json, "libraryState"); }
    static String publishScript(String json, String method) {
        if (!("libraryState".equals(method) || "roomState".equals(method) || "cameraState".equals(method) || "roomCapture".equals(method) || "integrityResult".equals(method))) return null;
        if (json == null || json.length() > ("roomState".equals(method) ? 327680 : ("cameraState".equals(method) || "roomCapture".equals(method)) ? 140000 : "integrityResult".equals(method) ? 36864 : 32768)) return null;
        try {
            JSONObject state = new JSONObject(json);
            if (state.optInt("version") != 1 || !state.optString("session").matches("[a-f0-9]{32}") || state.optInt("revision") < 1) return null;
            // Validation above does not require rebuilding the payload. Keep the native
            // JSON intact and quote it as data, including source attribution.
            return "window.maestroBook && window.maestroBook." + method + " && window.maestroBook." + method + "(JSON.parse(" + JSONObject.quote(json) + "))";
        } catch (Exception ignored) { return null; }
    }
}
