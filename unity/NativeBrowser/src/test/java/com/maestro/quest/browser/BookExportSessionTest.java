// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.util.Base64;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import org.json.JSONObject;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import org.robolectric.util.ReflectionHelpers;
import static org.junit.Assert.*;

@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class BookExportSessionTest {
    static final String ID="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", OTHER="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    static class Sink implements BookExportSession.Sink {
        final ByteArrayOutputStream data=new ByteArrayOutputStream(); int writes,finishes,aborts; boolean failWrite,failFinish;
        public void write(byte[] value) throws IOException { writes++;data.write(value);if(failWrite)throw new IOException("disk full"); }
        public String finish() throws IOException {finishes++;if(failFinish)throw new IOException("close failed");return "Downloads/Maestro/backup.json";}
        public void abort(){aborts++;}
    }
    Sink sink; BookExportSession session; int opens;
    @Before public void setup(){sink=new Sink();session=new BookExportSession((name,mime)->{opens++;return sink;});}
    @After public void cleanup(){session.close();}
    static JSONObject request(String id,int sequence,String operation) throws Exception {return new JSONObject().put("version",1).put("id",id).put("sequence",sequence).put("operation",operation);}
    static String open() throws Exception {return request(ID,0,"open").put("name","backup.json").put("mime","application/json").toString();}
    static String chunk(int seq,long offset,byte[] bytes) throws Exception {return request(ID,seq,"chunk").put("offset",offset).put("data",Base64.encodeToString(bytes,Base64.NO_WRAP)).toString();}
    static String end(int seq,long offset,String op) throws Exception {return request(ID,seq,op).put("offset",offset).toString();}
    static boolean ok(JSONObject result){return result!=null&&result.optBoolean("ok");}
    @Test public void duplicateRequestsWriteAndPublishExactlyOnce() throws Exception {
        assertTrue(ok(session.handle(open())));assertTrue(ok(session.handle(open())));assertEquals(1,opens);
        byte[] bytes="日本語 🎵".getBytes(StandardCharsets.UTF_8);String chunk=chunk(1,0,bytes);
        assertTrue(ok(session.handle(chunk)));assertTrue(ok(session.handle(chunk)));assertEquals(1,sink.writes);assertArrayEquals(bytes,sink.data.toByteArray());
        String finish=end(2,bytes.length,"finish");JSONObject receipt=session.handle(finish);
        assertTrue(ok(receipt));assertEquals(bytes.length,receipt.getLong("bytes"));assertEquals("Downloads/Maestro/backup.json",receipt.getString("location"));assertEquals(receipt.toString(),session.handle(finish).toString());assertEquals(1,sink.finishes);session.close();assertEquals(0,sink.aborts);
    }
    @Test public void malformedStartCannotCreateFiles() throws Exception {
        for(String name:new String[]{"../a.json","x\\a.json","bad\n.json","a.exe","a:1.json",""}){JSONObject r=new JSONObject(open()).put("name",name);assertFalse(ok(session.handle(r.toString())));}
        assertFalse(ok(session.handle(new JSONObject(open()).put("uri","file:///x").toString())));
        assertFalse(ok(session.handle(new JSONObject(open()).put("mime","image/png").toString())));
        assertFalse(ok(session.handle(new JSONObject(open()).put("version",2).toString())));assertEquals(0,opens);
    }
    @Test public void wrongSequenceOrOffsetDeletesPendingFile() throws Exception {
        assertTrue(ok(session.handle(open())));assertFalse(ok(session.handle(chunk(2,0,new byte[]{1}))));assertEquals(1,sink.aborts);assertEquals(0,sink.writes);
        setup();assertTrue(ok(session.handle(open())));assertFalse(ok(session.handle(chunk(1,1,new byte[]{1}))));assertEquals(1,sink.aborts);
    }
    @Test public void staleTransactionCannotAbortCurrentFile() throws Exception {
        assertTrue(ok(session.handle(open())));JSONObject stale=request(OTHER,4,"abort").put("offset",0);
        assertFalse(ok(session.handle(stale.toString())));assertEquals(0,sink.aborts);assertTrue(ok(session.handle(end(1,0,"finish"))));
    }
    @Test public void partialWriteAndFinishErrorsNeverReturnSuccessLocation() throws Exception {
        assertTrue(ok(session.handle(open())));sink.failWrite=true;JSONObject failure=session.handle(chunk(1,0,new byte[]{1}));assertFalse(ok(failure));assertFalse(failure.has("location"));assertEquals(1,sink.aborts);
        setup();assertTrue(ok(session.handle(open())));sink.failFinish=true;assertFalse(ok(session.handle(end(1,0,"finish"))));assertEquals(1,sink.aborts);
    }
    @Test public void chunksAndTotalBytesAreBoundedBeforeWriting() throws Exception {
        assertTrue(ok(session.handle(open())));assertFalse(ok(session.handle(chunk(1,0,new byte[BookExportSession.CHUNK_BYTES+1]))));assertEquals(0,sink.writes);
        setup();assertTrue(ok(session.handle(open())));ReflectionHelpers.setField(session,"bytes",BookExportSession.MAX_BYTES-1);
        assertTrue(ok(session.handle(chunk(1,BookExportSession.MAX_BYTES-1,new byte[]{1}))));assertFalse(ok(session.handle(chunk(2,BookExportSession.MAX_BYTES,new byte[]{2}))));assertEquals(1,sink.writes);assertEquals(1,sink.aborts);
    }
    @Test public void corruptBase64AndExtraFieldsAbortRatherThanPublish() throws Exception {
        assertTrue(ok(session.handle(open())));assertFalse(ok(session.handle(new JSONObject(chunk(1,0,new byte[]{1})).put("data","AQ==\n").toString())));assertEquals(1,sink.aborts);
        setup();assertTrue(ok(session.handle(open())));assertFalse(ok(session.handle(new JSONObject(end(1,0,"finish")).put("path","elsewhere").toString())));assertEquals(0,sink.finishes);
    }
    @Test public void abortAcceptsUncertainOffsetAndExpirationAllowsFreshExport() throws Exception {
        assertTrue(ok(session.handle(open())));assertTrue(ok(session.handle(end(1,20,"abort"))));assertEquals(1,sink.aborts);
        assertFalse(ok(session.handle(open())));assertEquals(1,opens);
        assertTrue(ok(session.handle(new JSONObject(open()).put("id",OTHER).toString())));session.expire();assertEquals(2,sink.aborts);
        assertTrue(ok(session.handle(new JSONObject(open()).put("id","cccccccccccccccccccccccccccccccc").toString())));session.close();assertEquals(3,sink.aborts);assertNull(session.handle(open()));
    }
}
