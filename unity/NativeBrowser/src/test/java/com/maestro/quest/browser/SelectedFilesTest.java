// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.content.*;
import android.content.pm.*;
import android.database.*;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.Robolectric;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import org.robolectric.shadows.ShadowContentResolver;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class SelectedFilesTest {
    static final Uri SOURCE=Uri.parse("content://maestro.fixture/selected");
    static class Fixture extends ContentProvider {
        File data; String name="../hello.txt"; Long advertisedSize=null;
        public boolean onCreate() { return true; }
        public String getType(Uri uri) { return "text/plain"; }
        public Cursor query(Uri uri,String[] projection,String selection,String[] args,String order) {
            MatrixCursor cursor=new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE }); cursor.addRow(new Object[] { name,advertisedSize }); return cursor;
        }
        public ParcelFileDescriptor openFile(Uri uri,String mode) throws FileNotFoundException { return ParcelFileDescriptor.open(data,ParcelFileDescriptor.MODE_READ_ONLY); }
        public Uri insert(Uri uri,ContentValues values) { throw new UnsupportedOperationException(); }
        public int delete(Uri uri,String selection,String[] args) { throw new UnsupportedOperationException(); }
        public int update(Uri uri,ContentValues values,String selection,String[] args) { throw new UnsupportedOperationException(); }
    }
    static Fixture install(Activity activity) throws Exception {
        ProviderInfo info=new ProviderInfo(); info.authority=SOURCE.getAuthority(); info.name=Fixture.class.getName(); info.packageName="maestro.fixture";
        info.applicationInfo=new ApplicationInfo(); info.applicationInfo.packageName=info.packageName; info.applicationInfo.uid=activity.getApplicationInfo().uid+100;
        info.exported=true; info.grantUriPermissions=true;
        shadowOf(activity.getPackageManager()).addOrUpdateProvider(info);
        Fixture provider=new Fixture(); provider.data=File.createTempFile("maestro-selection-",".txt",activity.getCacheDir()); Files.write(provider.data.toPath(),"hello".getBytes(StandardCharsets.UTF_8));
        provider.attachInfo(activity,info); ShadowContentResolver.registerProviderInternal(info.authority,provider); return provider;
    }
    Activity activity; Fixture fixture; SelectedFiles cache;
    @Before public void setup() throws Exception { activity=Robolectric.buildActivity(Activity.class).setup().get(); fixture=install(activity); cache=new SelectedFiles(activity,8,12); }
    @After public void close() { cache.close(); fixture.data.delete(); activity.finish(); }
    @Test public void copiesUnknownSizeStreamAndServesOnlyReadOnlyCopiesWithSafeMetadata() throws Exception {
        Uri uri=cache.copy(new Uri[] { SOURCE })[0]; SelectedFiles.Entry entry=SelectedFiles.ENTRIES.get(uri.getLastPathSegment());
        assertEquals("hello",new String(Files.readAllBytes(entry.file.toPath()),StandardCharsets.UTF_8)); assertEquals(".._hello.txt",entry.name); assertNotEquals(SOURCE,uri);
        ProviderInfo info=new ProviderInfo(); info.authority=activity.getPackageName()+".maestro.selected";
        SelectedFileProvider provider=new SelectedFileProvider(); provider.attachInfo(activity,info);
        try (Cursor cursor=provider.query(uri,null,null,null,null)) { assertTrue(cursor.moveToFirst()); assertEquals(5,cursor.getLong(cursor.getColumnIndexOrThrow(OpenableColumns.SIZE))); }
        try (ParcelFileDescriptor fd=provider.openFile(uri,"r")) { assertEquals(5,fd.getStatSize()); }
        assertThrows(SecurityException.class,() -> provider.openFile(uri,"rw"));
        assertThrows(SecurityException.class,() -> provider.openFile(Uri.parse("content://wrong-authority/"+uri.getLastPathSegment()),"r"));
        cache.close(); assertFalse(entry.file.exists()); assertFalse(SelectedFiles.ENTRIES.containsKey(uri.getLastPathSegment()));
    }
    @Test public void cancellationBeforeProviderOpenDoesNotGrantAnyBytes() throws Exception {
        int before=SelectedFiles.ENTRIES.size();assertThrows(InterruptedIOException.class,()->cache.copy(new Uri[]{SOURCE},()->true));assertEquals(before,SelectedFiles.ENTRIES.size());
        assertEquals(1,cache.copy(new Uri[]{SOURCE}).length);
    }
    @Test public void rejectsPrivateSchemesAndOwnProvidersInsteadOfReturningTheirUrisToWebView() {
        assertFalse(SelectedFiles.allowedUri(activity,Uri.parse("file:///data/private")));
        assertFalse(SelectedFiles.allowedUri(activity,Uri.parse("https://example.com/file")));
        assertFalse(SelectedFiles.allowedUri(activity,Uri.parse("content://not-installed/file")));
        ProviderInfo own=new ProviderInfo(); own.authority="maestro.private"; own.name=Fixture.class.getName(); own.packageName=activity.getPackageName(); own.applicationInfo=activity.getApplicationInfo();
        shadowOf(activity.getPackageManager()).addOrUpdateProvider(own);
        assertFalse(SelectedFiles.allowedUri(activity,Uri.parse("content://maestro.private/settings")));
        assertThrows(IOException.class,() -> cache.copy(new Uri[] { Uri.parse("file:///private") }));
    }
    @Test public void enforcesActualByteAndSessionLimitsEvenWhenProviderLiesAndCleansPartialFiles() throws Exception {
        fixture.advertisedSize=1L; Files.write(fixture.data.toPath(),"0123456789".getBytes(StandardCharsets.UTF_8));
        int before=SelectedFiles.ENTRIES.size(); assertThrows(IOException.class,() -> cache.copy(new Uri[] { SOURCE })); assertEquals(before,SelectedFiles.ENTRIES.size());
        Files.write(fixture.data.toPath(),"hello".getBytes(StandardCharsets.UTF_8)); Uri[] first=cache.copy(new Uri[] { SOURCE }); cache.copy(new Uri[] { SOURCE });
        assertThrows(IOException.class,() -> cache.copy(new Uri[] { SOURCE }));
        cache.release(first); cache.copy(new Uri[] { SOURCE });
        assertEquals(before+2,SelectedFiles.ENTRIES.size());
    }
    @Test public void rejectsOversizedMetadataBeforeCopyAndBoundsBatchCount() throws Exception {
        fixture.advertisedSize=99L; assertThrows(IOException.class,() -> cache.copy(new Uri[] { SOURCE }));
        assertThrows(IOException.class,() -> cache.copy(new Uri[9]));
        cache.close(); assertThrows(IOException.class,() -> cache.copy(new Uri[] { SOURCE }));
        assertEquals("selected-file",SelectedFiles.safeName("..")); assertEquals("_secret_key",SelectedFiles.safeName("/secret\\key"));
    }
}
