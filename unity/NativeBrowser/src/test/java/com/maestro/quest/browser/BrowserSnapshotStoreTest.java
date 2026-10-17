// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import org.junit.Test;
import static org.junit.Assert.*;
public final class BrowserSnapshotStoreTest {
    @Test public void roomChannelHasIndependentBoundAndRejectsLateCallbacks() {
        BrowserSnapshotStore room=new BrowserSnapshotStore(32768),book=new BrowserSnapshotStore();
        String recipe="r".repeat(6000);long before=room.begin();room.publish(before,recipe);book.publish(book.begin(),recipe);
        assertEquals(recipe,room.read());assertEquals("",book.read());
        room.suspend(true);room.suspend(false);room.publish(before,recipe);assertEquals("",room.read());
        room.publish(room.begin(),"r".repeat(32769));assertEquals("",room.read());
    }
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
