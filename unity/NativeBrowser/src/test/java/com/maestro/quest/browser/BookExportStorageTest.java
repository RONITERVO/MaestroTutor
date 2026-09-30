// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.content.*;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.MediaStore;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import org.robolectric.shadows.ShadowContentResolver;
import static org.junit.Assert.*;

@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class BookExportStorageTest {
    static final Uri ROW=Uri.parse("content://media/external/downloads/42");
    public static class Provider extends ContentProvider {
        ContentValues values; File file; int deleted,publications; boolean failPublish,failName; String resolvedName="backup (1).json";
        public boolean onCreate(){return true;}
        public Uri insert(Uri uri,ContentValues value){assertEquals(MediaStore.Downloads.EXTERNAL_CONTENT_URI,uri);assertFalse(value.containsKey(MediaStore.MediaColumns.DATE_EXPIRES));values=new ContentValues(value);return ROW;}
        public String getType(Uri uri){return "application/json";}
        public ParcelFileDescriptor openFile(Uri uri,String mode) throws FileNotFoundException {assertEquals(ROW,uri);assertEquals("w",mode);return ParcelFileDescriptor.open(file,ParcelFileDescriptor.MODE_CREATE|ParcelFileDescriptor.MODE_WRITE_ONLY|ParcelFileDescriptor.MODE_TRUNCATE);}
        public int update(Uri uri,ContentValues value,String selection,String[] args){assertEquals(ROW,uri);assertFalse(value.containsKey(MediaStore.MediaColumns.DATE_EXPIRES));publications++;if(failPublish)return 0;values.putAll(value);return 1;}
        public int delete(Uri uri,String selection,String[] args){assertEquals(ROW,uri);deleted++;return 1;}
        public Cursor query(Uri uri,String[] projection,String selection,String[] args,String order){if(failName)return null;MatrixCursor c=new MatrixCursor(new String[]{MediaStore.MediaColumns.DISPLAY_NAME});c.addRow(new Object[]{resolvedName});return c;}
    }
    Provider provider;BookExportSession session;
    @Before public void setup() throws Exception {
        provider=new Provider();provider.file=File.createTempFile("quest-export",".json",RuntimeEnvironment.getApplication().getCacheDir());
        android.content.pm.ProviderInfo info=new android.content.pm.ProviderInfo();info.authority="media";provider.attachInfo(RuntimeEnvironment.getApplication(),info);
        ShadowContentResolver.registerProviderInternal("media",provider);
        session=new BookExportSession(new BookExportStorage(RuntimeEnvironment.getApplication().getContentResolver()));
    }
    @After public void cleanup(){session.close();provider.file.delete();}
    @Test public void fileStaysPendingUntilClosedAndReceiptUsesResolvedName() throws Exception {
        assertTrue(BookExportSessionTest.ok(session.handle(BookExportSessionTest.open())));
        assertEquals(Integer.valueOf(1),provider.values.getAsInteger(MediaStore.MediaColumns.IS_PENDING));assertEquals("Download/Maestro/",provider.values.getAsString(MediaStore.MediaColumns.RELATIVE_PATH));assertFalse(provider.values.containsKey(MediaStore.MediaColumns.DATE_EXPIRES));
        byte[] bytes="{\"name\":\"音楽\"}".getBytes(StandardCharsets.UTF_8);session.handle(BookExportSessionTest.chunk(1,0,bytes));assertEquals(0,provider.publications);
        assertEquals("Downloads/Maestro/backup (1).json",session.handle(BookExportSessionTest.end(2,bytes.length,"finish")).getString("location"));assertArrayEquals(bytes,Files.readAllBytes(provider.file.toPath()));assertEquals(Integer.valueOf(0),provider.values.getAsInteger(MediaStore.MediaColumns.IS_PENDING));assertNull(provider.values.get(MediaStore.MediaColumns.DATE_EXPIRES));session.close();assertEquals(0,provider.deleted);
    }
    @Test public void nativeArchivePublishesBinaryThroughTheSamePendingDownloadsProvider() throws Exception {
        String directory=WorkspaceExports.Directory(RuntimeEnvironment.getApplication());File source=new File(directory,"maestro-workspace-"+"a".repeat(32)+".zip");
        byte[] bytes=new byte[]{0,80,75,-1,1,2};Files.write(source.toPath(),bytes);provider.resolvedName=source.getName().replace(".zip"," (1).zip");
        try {assertEquals("Downloads/Maestro/"+provider.resolvedName,WorkspaceExports.Publish(RuntimeEnvironment.getApplication(),source.getAbsolutePath()));assertArrayEquals(bytes,Files.readAllBytes(provider.file.toPath()));assertEquals("application/zip",provider.values.getAsString(MediaStore.MediaColumns.MIME_TYPE));assertEquals(Integer.valueOf(0),provider.values.getAsInteger(MediaStore.MediaColumns.IS_PENDING));assertEquals(1,provider.publications);assertEquals(0,provider.deleted);}finally{source.delete();}
    }
    @Test public void lifecycleCloseDeletesOnlyItsPendingRow() throws Exception {
        session.handle(BookExportSessionTest.open());session.close();session.close();assertEquals(1,provider.deleted);assertEquals(0,provider.publications);
    }
    @Test public void failedPublicationDeletesPendingRowAndReturnsFailure() throws Exception {
        session.handle(BookExportSessionTest.open());provider.failPublish=true;assertFalse(BookExportSessionTest.ok(session.handle(BookExportSessionTest.end(1,0,"finish"))));assertEquals(1,provider.deleted);
    }
    @Test public void missingResolvedNameCannotClaimTheRequestedFilename() throws Exception {
        session.handle(BookExportSessionTest.open());provider.failName=true;assertFalse(BookExportSessionTest.ok(session.handle(BookExportSessionTest.end(1,0,"finish"))));assertEquals(1,provider.deleted);assertEquals(0,provider.publications);
    }
}
