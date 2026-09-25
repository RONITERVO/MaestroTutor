// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.content.Intent;
import android.os.Looper;
import android.net.Uri;
import android.provider.DocumentsContract;
import java.io.File;
import org.json.JSONObject;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.Robolectric;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@SuppressWarnings("deprecation")
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class ModelPickerTest {
    Activity activity;
    SelectedFilesTest.Fixture fixture;
    @Before public void setup() throws Exception { activity=Robolectric.buildActivity(Activity.class).setup().get(); fixture=SelectedFilesTest.install(activity); ModelPicker.Release(); shadowOf(Looper.getMainLooper()).idle(); }
    @After public void cleanup() { ModelPicker.Release(); shadowOf(Looper.getMainLooper()).idle(); fixture.data.delete(); activity.finish(); }
    ModelPicker start() {
        ModelPicker.Start(activity); shadowOf(Looper.getMainLooper()).idle(); activity.getFragmentManager().executePendingTransactions();
        return (ModelPicker)activity.getFragmentManager().findFragmentByTag("MaestroModelPicker");
    }
    @Test public void opensSingleReadOnlyDocumentIntentAndReportsCancellation() throws Exception {
        Intent intent=ModelPicker.selectionIntent(); assertEquals(Intent.ACTION_OPEN_DOCUMENT,intent.getAction()); assertTrue(intent.hasCategory(Intent.CATEGORY_OPENABLE));
        assertEquals(Intent.FLAG_GRANT_READ_URI_PERMISSION,intent.getFlags()); assertFalse(intent.getBooleanExtra(Intent.EXTRA_ALLOW_MULTIPLE,false));
        assertEquals("*/*",intent.getType()); assertFalse(intent.hasExtra(Intent.EXTRA_MIME_TYPES));
        Uri initial=intent.getParcelableExtra(DocumentsContract.EXTRA_INITIAL_URI);
        assertEquals("com.android.externalstorage.documents",initial.getAuthority());
        assertEquals("primary:Download",DocumentsContract.getDocumentId(initial));
        assertNull(intent.getComponent()); assertNull(intent.getPackage());
        ModelPicker picker=start(); picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_CANCELED,null);
        JSONObject result=new JSONObject(ModelPicker.ReadResult()); assertEquals("",result.getString("path")); assertTrue(result.getString("error").contains("cancelled"));
    }
    @Test public void deliversOnlyPrivateCopyThenReleasesIt() throws Exception {
        ModelPicker picker=start(); picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));
        waitResult(); JSONObject result=new JSONObject(ModelPicker.ReadResult()); assertEquals("",result.getString("error"));
        File copy=new File(result.getString("path")); assertTrue(copy.exists()); assertEquals(5,copy.length()); assertNotEquals(fixture.data,copy);
        ModelPicker.Release(); shadowOf(Looper.getMainLooper()).idle();
        long until=System.currentTimeMillis()+3000; while(copy.exists() && System.currentTimeMillis()<until) Thread.sleep(10);
        assertFalse(copy.exists()); assertEquals("",ModelPicker.ReadResult());
    }
    @Test public void ignoresResultsFromReleasedOwner() throws Exception {
        ModelPicker previous=start(); ModelPicker.Release(); shadowOf(Looper.getMainLooper()).idle();
        previous.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));
        assertEquals("",ModelPicker.ReadResult());
    }
    void waitResult() throws Exception {
        long until=System.currentTimeMillis()+3000;
        while(ModelPicker.ReadResult().isEmpty() && System.currentTimeMillis()<until) { Thread.sleep(10); shadowOf(Looper.getMainLooper()).idle(); }
        assertFalse("No picker result",ModelPicker.ReadResult().isEmpty());
    }
}
