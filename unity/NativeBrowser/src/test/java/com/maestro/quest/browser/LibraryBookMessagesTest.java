// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import org.json.JSONObject;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class LibraryBookMessagesTest {
    @Test public void roomStateIsDataAndOnlyKnownReceiversAreAllowed() throws Exception {
        String json=new JSONObject().put("version",1).put("session","a".repeat(32)).put("revision",1).put("status","');window.injected=true;//").toString();
        String script=LibraryBookMessages.publishScript(json,"roomState");
        String prefix="window.maestroBook && window.maestroBook.roomState && window.maestroBook.roomState(JSON.parse(";
        assertTrue(script.startsWith(prefix));
        assertEquals(json,new org.json.JSONTokener(script.substring(prefix.length(),script.length()-2)).nextValue());
        assertNull(LibraryBookMessages.publishScript(json,"eval"));
    }
    @Test public void quotesAttributionAsDataAndRejectsInvalidOrExcessiveState() throws Exception {
        JSONObject state = new JSONObject().put("version",1).put("session","a".repeat(32)).put("revision",1).put("attribution","'); alert('source'); //\n\\");
        String script = LibraryBookMessages.publishScript(state.toString());
        String prefix = "window.maestroBook && window.maestroBook.libraryState && window.maestroBook.libraryState(JSON.parse(";
        assertTrue(script.startsWith(prefix));
        String argument = script.substring(prefix.length(),script.length()-2);
        assertEquals(state.toString(),new org.json.JSONTokener(argument).nextValue());
        assertNull(LibraryBookMessages.publishScript(state.put("version",2).toString()));
        assertNull(LibraryBookMessages.publishScript("x".repeat(32769)));
        assertNull(LibraryBookMessages.publishScript("not json"));
    }
}
