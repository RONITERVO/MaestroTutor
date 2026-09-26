// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import org.junit.Test;
import static org.junit.Assert.*;
public final class BrowserSnapshotStoreTest {
    @Test public void suspendResumeAndNavigationRejectLateActivitySnapshots() {
        BrowserSnapshotStore store = new BrowserSnapshotStore();
        long before = store.begin(); store.publish(before,"speaking"); assertEquals("speaking",store.read());
        store.suspend(true); store.publish(before,"old speaking"); assertEquals("",store.read()); assertEquals(-1,store.begin());
        store.suspend(false); store.publish(before,"old speaking"); assertEquals("",store.read());
        long resumed = store.begin(); store.publish(resumed,"audio paused"); assertEquals("audio paused",store.read());
        store.invalidate(); store.publish(resumed,"old page"); assertEquals("",store.read());
        store.publish(store.begin(),new String(new char[4097])); assertEquals("",store.read());
        store.publish(store.begin(),"idle"); assertEquals("idle",store.read());
    }
}
