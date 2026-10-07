// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import org.json.JSONObject;
import static org.junit.Assert.*;

@RunWith(RobolectricTestRunner.class) @Config(manifest=Config.NONE, sdk=35)
public class BookSpeechMailboxTest {
    @Test public void documentChangesRejectQueuedEvaluationAndLateAudio() throws Exception {
        BookSpeechMailbox box = new BookSpeechMailbox(); String old = box.document();
        assertTrue(box.begin(old)); assertFalse(box.begin(old));
        box.invalidate(); String current = box.document(); assertNotEquals(old,current);
        box.complete(old,JSONObject.quote("{\"open\":true}"));
        assertTrue(new JSONObject(box.read()).isNull("payload"));
        assertFalse(box.begin(old)); assertTrue(box.begin(current));
        box.complete(current,JSONObject.quote("{\"open\":false}"));
        JSONObject result = new JSONObject(box.read());
        assertEquals(current,result.getString("document")); assertEquals(1,result.getLong("poll"));
        assertFalse(result.getJSONObject("payload").getBoolean("open"));
    }
    @Test public void emptyMalformedAndOversizedRepliesClearEarlierAudioInsteadOfRetainingIt() throws Exception {
        BookSpeechMailbox box = new BookSpeechMailbox(); String owner = box.document();
        assertTrue(box.begin(owner)); box.complete(owner,JSONObject.quote("{\"open\":true}"));
        assertEquals(1,new JSONObject(box.read()).getLong("poll"));
        for(String invalid:new String[] {"null","\"bad\"", "x".repeat(110001)}) {
            assertTrue(box.begin(owner)); box.complete(owner,invalid);
            assertTrue(new JSONObject(box.read()).isNull("payload"));
        }
        assertEquals(4,new JSONObject(box.read()).getLong("poll"));
    }
    @Test public void onlyStructuredBoundedStatusIsInsertedInTopDocumentScript() {
        assertNull(BookSpeechMailbox.script("alert(1)"));
        assertNull(BookSpeechMailbox.script("{\"version\":2}"));
        assertNull(BookSpeechMailbox.script("x".repeat(2049)));
        String script = BookSpeechMailbox.script("{\"version\":1,\"session\":\"a\\\"b\"}");
        assertTrue(script.contains("window.maestroBook.speechExchange("));
        assertTrue(script.contains("a\\\"b"));
    }
}
