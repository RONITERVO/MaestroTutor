// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

/** Invalidates delayed WebView results across suspension, navigation and replacement. */
final class BrowserSnapshotStore {
    private final int maximum;
    BrowserSnapshotStore() { this(4096); }
    BrowserSnapshotStore(int maximum) { this.maximum = maximum; }
    private long generation;
    private boolean suspended;
    private String value = "";
    synchronized void invalidate() { generation++; value = ""; }
    synchronized void suspend(boolean next) { suspended = next; invalidate(); }
    synchronized long begin() { return suspended ? -1 : generation; }
    synchronized void publish(long request, String result) {
        if (!suspended && request >= 0 && request == generation && result != null && result.length() <= maximum) value = result;
    }
    synchronized String read() { return value; }
}
