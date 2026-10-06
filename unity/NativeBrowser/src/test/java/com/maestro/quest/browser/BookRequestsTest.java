// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.webkit.PermissionRequest;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.Robolectric;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;
import android.os.Looper;

@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class BookRequestsTest {
    static final class Request extends PermissionRequest {
        Uri origin = Uri.parse("https://appassets.androidplatform.net");
        String[] resources = { RESOURCE_AUDIO_CAPTURE };
        int granted, denied;
        public Uri getOrigin() { return origin; }
        public String[] getResources() { return resources; }
        public void grant(String[] values) { assertArrayEquals(new String[] { RESOURCE_AUDIO_CAPTURE },values); granted++; }
        public void deny() { denied++; }
    }
    static final class Host implements BookRequests.Host {
        Activity activity; boolean active=true, gesture=true; Object document=new Object(); int prompts, pickers; Intent intent; String message;
        public Activity activity() { return activity; }
        public boolean active() { return active; }
        public Object document() { return document; }
        public void askMicrophone() { prompts++; }
        public void pickFiles(Intent value) { pickers++; intent=value; }
        public void checkFileGesture(ValueCallback<Boolean> callback) { callback.onReceiveValue(gesture); }
        public void report(String value) { message=value; }
    }
    static final class Choice extends WebChromeClient.FileChooserParams {
        int mode=MODE_OPEN;
        public int getMode() { return mode; }
        public String[] getAcceptTypes() { return new String[] { "image/png", "image/jpeg" }; }
        public boolean isCaptureEnabled() { return false; }
        public CharSequence getTitle() { return "Choose file"; }
        public String getFilenameHint() { return null; }
        public Intent createIntent() { throw new AssertionError("Use the restricted document intent"); }
    }
    Host host; BookRequests broker;
    @Before public void setup() { host=new Host(); host.activity=Robolectric.buildActivity(Activity.class).setup().get(); broker=new BookRequests(host); }
    @After public void close() { broker.close(); host.activity.finish(); }
    private void grantMicrophone() { shadowOf(host.activity.getApplication()).grantPermissions(Manifest.permission.RECORD_AUDIO); }
    @Test public void packagedBrowserDeclaresBothWebRtcAudioPermissions() throws Exception {
        String[] requested = host.activity.getPackageManager().getPackageInfo(
            host.activity.getPackageName(), android.content.pm.PackageManager.GET_PERMISSIONS).requestedPermissions;
        assertNotNull("Browser manifest must declare WebRTC audio permissions", requested);
        java.util.List<String> permissions = java.util.Arrays.asList(requested);
        assertTrue(permissions.contains(Manifest.permission.RECORD_AUDIO));
        assertTrue("Android WebView cannot open an audio source without MODIFY_AUDIO_SETTINGS",
            permissions.contains(Manifest.permission.MODIFY_AUDIO_SETTINGS));
    }
    @Test public void promptsOnlyForAppMicrophoneAndGrantsOnlyAfterAndroidConsent() {
        Request request=new Request(); broker.requestMicrophone(request); assertEquals(1,host.prompts); assertEquals(0,request.granted);
        grantMicrophone(); broker.microphoneResult(); assertEquals(1,request.granted); assertEquals(0,request.denied);
        Request again=new Request(); broker.requestMicrophone(again); assertEquals(1,again.granted); assertEquals(1,host.prompts);
    }
    @Test public void deniesOpaqueAndRemoteFramesCameraAndUnknownResources() {
        for (String origin : new String[] { "https://evil.example", "null", "http://appassets.androidplatform.net", "https://appassets.androidplatform.net:444", "https://user@appassets.androidplatform.net" }) {
            Request request=new Request(); request.origin=Uri.parse(origin); broker.requestMicrophone(request); assertEquals(1,request.denied);
        }
        Request camera=new Request(); camera.resources=new String[] { PermissionRequest.RESOURCE_AUDIO_CAPTURE,PermissionRequest.RESOURCE_VIDEO_CAPTURE }; broker.requestMicrophone(camera); assertEquals(1,camera.denied); assertEquals(0,host.prompts);
    }
    @Test public void cancellationAndInterruptionNeverGrantAnOldRequestOrRestartCapture() {
        Request first=new Request(); broker.requestMicrophone(first); broker.cancelMicrophone(first); grantMicrophone(); broker.microphoneResult(); assertEquals(0,first.granted); assertEquals(0,first.denied);
        shadowOf(host.activity.getApplication()).denyPermissions(Manifest.permission.RECORD_AUDIO);
        Request second=new Request(); broker.requestMicrophone(second); host.active=false; broker.suspended(); grantMicrophone(); broker.microphoneResult();
        assertEquals(0,second.granted); assertEquals(1,second.denied); assertTrue(host.message.contains("bell"));
    }
    @Test public void navigationDisposalAndDenialCompletePermissionAtMostOnce() {
        Request denied=new Request(); broker.requestMicrophone(denied); broker.microphoneResult(); assertEquals(1,denied.denied);
        Request stale=new Request(); broker.requestMicrophone(stale); host.document=new Object(); grantMicrophone(); broker.microphoneResult(); assertEquals(1,stale.denied); assertEquals(0,stale.granted);
        shadowOf(host.activity.getApplication()).denyPermissions(Manifest.permission.RECORD_AUDIO);
        Request closed=new Request(); broker.requestMicrophone(closed); broker.close(); grantMicrophone(); broker.microphoneResult(); assertEquals(1,closed.denied); assertEquals(0,closed.granted);
    }
    @Test public void pickerRequiresTopDocumentGestureAndUsesReadOnlyOpenableFiles() {
        int[] cancelled={0}; host.gesture=false; broker.chooseFiles(value -> { assertNull(value); cancelled[0]++; },new Choice()); assertEquals(1,cancelled[0]); assertEquals(0,host.pickers);
        host.gesture=true; Choice choice=new Choice(); choice.mode=Choice.MODE_OPEN_MULTIPLE;
        broker.chooseFiles(value -> cancelled[0]++,choice); assertEquals(Intent.ACTION_OPEN_DOCUMENT,host.intent.getAction()); assertTrue(host.intent.hasCategory(Intent.CATEGORY_OPENABLE));
        assertTrue(host.intent.getBooleanExtra(Intent.EXTRA_ALLOW_MULTIPLE,false)); assertEquals(0,host.intent.getFlags() & Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        broker.fileResult(Activity.RESULT_CANCELED,null); assertEquals(2,cancelled[0]); broker.fileResult(Activity.RESULT_CANCELED,null); assertEquals(2,cancelled[0]);
    }
    @Test public void rejectsFolderSaveAndConcurrentRequestsAndCancelsDisposedPicker() {
        int[] cancelled={0}; Choice save=new Choice(); save.mode=Choice.MODE_SAVE; broker.chooseFiles(value -> cancelled[0]++,save); assertEquals(0,host.pickers);
        broker.chooseFiles(value -> cancelled[0]++,new Choice()); broker.chooseFiles(value -> cancelled[0]++,new Choice()); assertEquals(1,host.pickers);
        broker.close(); broker.fileResult(Activity.RESULT_OK,new Intent().setData(Uri.parse("content://provider/file"))); assertEquals(3,cancelled[0]);
    }
    @Test public void mimeFiltersAreNormalizedAndUnsupportedExtensionsRemainSelectable() {
        Intent value=BookRequests.pickerIntent(new String[] { ".png, image/jpeg", "image/png", "invalid" },true);
        assertArrayEquals(new String[] { "image/png","image/jpeg" },value.getStringArrayExtra(Intent.EXTRA_MIME_TYPES));
        assertEquals("*/*",BookRequests.pickerIntent(new String[] { ".vrm,.ndjson" },false).getType());
    }
    @Test public void selectedFileReturnsAfterForegroundResumeAndNeverToAReplacedPage() throws Exception {
        SelectedFilesTest.Fixture fixture=SelectedFilesTest.install(host.activity);
        try {
            Uri[][] result={null}; int[] calls={0};
            broker.chooseFiles(value -> { calls[0]++; result[0]=value; },new Choice());
            host.active=false; broker.suspended(); broker.fileResult(Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));
            long deadline=System.currentTimeMillis()+5000;
            while (SelectedFiles.ENTRIES.isEmpty() && System.currentTimeMillis()<deadline) Thread.sleep(10);
            for (int i=0;i<10;i++) { Thread.sleep(10); shadowOf(Looper.getMainLooper()).idle(); }
            assertEquals(0,calls[0]); host.active=true; broker.resumed(); assertEquals(1,calls[0]); assertNotNull(result[0]);
            broker.resumed(); assertEquals(1,calls[0]);
            broker.chooseFiles(value -> { assertNull(value); calls[0]++; },new Choice()); host.document=new Object();
            broker.fileResult(Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE)); assertEquals(2,calls[0]);
        } finally { fixture.data.delete(); }
    }
}
