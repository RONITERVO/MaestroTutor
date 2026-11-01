// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import java.io.*;
import java.nio.file.Files;
import java.util.*;
import org.junit.*;
import static org.junit.Assert.*;

public class WorkspaceExportsTest {
    File root,source;byte[] payload;Store store;
    static class Store implements BookExportSession.Storage,BookExportSession.Sink {
        ByteArrayOutputStream output=new ByteArrayOutputStream();int opens,finishes,aborts;boolean failWrite,failFinish;String name,mime;
        public BookExportSession.Sink open(String name,String mime){opens++;this.name=name;this.mime=mime;return this;}
        public void write(byte[] data)throws IOException{if(failWrite)throw new IOException("write failed");output.write(data);}
        public String finish()throws IOException{finishes++;if(failFinish)throw new IOException("publish failed");return "Downloads/Maestro/"+name;}
        public void abort(){aborts++;}
    }
    @Before public void setup()throws Exception{
        root=Files.createTempDirectory("workspace-export-").toFile().getCanonicalFile();source=new File(root,"maestro-workspace-"+UUID.randomUUID().toString().replace("-","")+".zip");
        payload=new byte[150001];new Random(17).nextBytes(payload);Files.write(source.toPath(),payload);store=new Store();
    }
    @After public void cleanup()throws Exception{try(var files=Files.list(root.toPath())){for(var p:files.toList())Files.delete(p);}Files.delete(root.toPath());}
    @Test public void streamsExactBinaryBytesAndReturnsOnlyPublishedLocation()throws Exception{
        assertEquals("Downloads/Maestro/"+source.getName(),WorkspaceExports.publish(root,source,store));assertArrayEquals(payload,store.output.toByteArray());assertEquals("application/zip",store.mime);assertEquals(1,store.finishes);assertEquals(0,store.aborts);assertTrue(source.exists());
    }
    @Test public void writeFailureAbortsPendingFile()throws Exception{store.failWrite=true;assertThrows(IOException.class,()->WorkspaceExports.publish(root,source,store));assertEquals(1,store.aborts);assertEquals(0,store.finishes);}
    @Test public void publicationFailureCannotReportSuccess()throws Exception{store.failFinish=true;assertThrows(IOException.class,()->WorkspaceExports.publish(root,source,store));assertEquals(1,store.aborts);}
    @Test public void rejectsOutsideAndNoncanonicalSourcesBeforeOpeningDownloads()throws Exception{
        assertThrows(IOException.class,()->WorkspaceExports.publish(new File(root,"nested"),source,store));
        assertThrows(IOException.class,()->WorkspaceExports.publish(root,new File(root,"../"+root.getName()+"/"+source.getName()),store));
        File secret=new File(root,"credentials.json");Files.write(secret.toPath(),new byte[]{1,2,3});assertThrows(IOException.class,()->WorkspaceExports.publish(root,secret,store));assertEquals(0,store.opens);
    }
    @Test public void refusesEmptyAndOversizedFilesBeforePublication()throws Exception{
        try(var file=new RandomAccessFile(source,"rw")){file.setLength(0);}assertThrows(IOException.class,()->WorkspaceExports.publish(root,source,store));
        try(var file=new RandomAccessFile(source,"rw")){file.setLength(WorkspaceExports.MAX_BYTES+1);}assertThrows(IOException.class,()->WorkspaceExports.publish(root,source,store));assertEquals(0,store.opens);
    }
    @Test public void detectsTruncationAndGrowthDuringStreaming(){
        assertThrows(IOException.class,()->WorkspaceExports.copy(new ByteArrayInputStream(payload),payload.length+1,store));
        assertThrows(IOException.class,()->WorkspaceExports.copy(new ByteArrayInputStream(payload),payload.length-1,store));assertEquals(0,store.finishes);
    }
    @Test public void resolvedArchiveNamesAllowProviderCollisionSuffixButNoPathOrOtherType(){
        assertTrue(BookExportStorage.validArchiveName(source.getName()));assertTrue(BookExportStorage.validArchiveName(source.getName().replace(".zip"," (2).zip")));
        for(String name:new String[]{"../"+source.getName(),source.getName()+".txt",source.getName()+"\n",source.getName().replace(".zip","/x.zip"),"secrets.zip"})assertFalse(name,BookExportStorage.validArchiveName(name));
        assertFalse("Browser text exports must not gain ZIP permission",BookExportSession.validName(source.getName()));
        String evidence=source.getName().replace("workspace","evidence");
        assertTrue(BookExportStorage.validArchiveName(evidence));assertTrue(BookExportStorage.validArchiveName(evidence.replace(".zip"," (2).zip")));
        assertFalse(BookExportSession.validName(evidence));assertFalse(BookExportStorage.validArchiveName("maestro-evidence-not-an-id.zip"));

    }
}
