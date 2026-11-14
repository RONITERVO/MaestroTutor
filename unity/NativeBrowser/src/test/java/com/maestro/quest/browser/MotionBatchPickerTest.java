// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.os.Looper;
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
public class MotionBatchPickerTest {
    Activity activity; SelectedFilesTest.Fixture fixture;String session;
    @Before public void setup() throws Exception {
        activity=Robolectric.buildActivity(Activity.class).setup().get(); fixture=SelectedFilesTest.install(activity);
        MotionBatchPicker.ReleaseSession(session); shadowOf(Looper.getMainLooper()).idle();
    }
    @After public void cleanup() { MotionBatchPicker.ReleaseSession(session); shadowOf(Looper.getMainLooper()).idle(); fixture.data.delete(); activity.finish(); }
    MotionBatchPicker start() {
        long until=System.currentTimeMillis()+3000;while(!MotionBatchPicker.ReadyToStart()&&System.currentTimeMillis()<until)try{Thread.sleep(5);}catch(InterruptedException ex){throw new AssertionError(ex);}
        session=java.util.UUID.randomUUID().toString().replace("-","");MotionBatchPicker.Start(activity,session); shadowOf(Looper.getMainLooper()).idle(); activity.getFragmentManager().executePendingTransactions();
        return (MotionBatchPicker)activity.getFragmentManager().findFragmentByTag("MaestroMotionBatchPicker");
    }
    Intent selection(Uri... values) {
        ClipData clip=ClipData.newRawUri("Motion exports",values[0]);
        for (int i=1;i<values.length;i++) clip.addItem(new ClipData.Item(values[i]));
        Intent result=new Intent(); result.setClipData(clip); return result;
    }
    JSONObject result() throws Exception {
        long until=System.currentTimeMillis()+10000;
        while(System.currentTimeMillis()<until){
            shadowOf(Looper.getMainLooper()).idle();String value=MotionBatchPicker.ReadResult(session);
            if(!value.isEmpty()){JSONObject result=new JSONObject(value);if(!result.optString("kind").equals("preparing"))return result;}
            Thread.sleep(5);
        }
        throw new AssertionError("Selection did not finish");
    }
    JSONObject copy(int index,int request) throws Exception {
        MotionBatchPicker.Copy(result().getString("session"),index,request); shadowOf(Looper.getMainLooper()).idle(); return waitFile(request);
    }
    JSONObject waitFile(int request) throws Exception {
        long until=System.currentTimeMillis()+4000;
        while (System.currentTimeMillis()<until) {
            shadowOf(Looper.getMainLooper()).idle(); String text=MotionBatchPicker.ReadResult(session);
            if (!text.isEmpty()) { JSONObject value=new JSONObject(text); if (value.optString("kind").equals("file") && value.optInt("request")==request) return value; }
            Thread.sleep(10);
        }
        fail("No file result for request "+request); return null;
    }
    void removed(File file) throws Exception {
        long until=System.currentTimeMillis()+3000; while(file.exists() && System.currentTimeMillis()<until) Thread.sleep(10); assertFalse(file.exists());
    }
    @Test public void batchAndSinglePickersCannotOverlapOrReleaseEachOther() throws Exception {
        MotionBatchPicker picker=start();String id=session,other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        assertEquals(id,MotionBatchPicker.Start(activity,id));assertFalse(ModelPicker.ReadyToStart());
        assertThrows(IllegalStateException.class,()->ModelPicker.Start(activity,other));assertThrows(IllegalStateException.class,()->WorkspacePicker.Start(activity,other));
        ModelPicker.Release(id);WorkspacePicker.Release(id);MotionBatchPicker.ReleaseSession(other);assertEquals("",MotionBatchPicker.ReadResult(other));
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));File file=new File(copy(0,1).getString("path"));
        picker.onDestroy();assertTrue("Activity loss must preserve a copy still being read",file.exists());
        MotionBatchPicker.ReleaseSession(id);shadowOf(Looper.getMainLooper()).idle();removed(file);
        long until=System.currentTimeMillis()+3000;while(!ModelPicker.ReadyToStart()&&System.currentTimeMillis()<until)Thread.sleep(5);
        ModelPicker.Start(activity,other);assertFalse(MotionBatchPicker.ReadyToStart());assertThrows(IllegalStateException.class,()->MotionBatchPicker.Start(activity,id));
        MotionBatchPicker.ReleaseSession(id);assertFalse(ModelPicker.Read(other).isEmpty());ModelPicker.Release(other);shadowOf(Looper.getMainLooper()).idle();
    }
    @Test public void aRetiringBatchWorkerKeepsEveryPickerClosedUntilItDrains() throws Exception {
        MotionBatchPicker picker=start();java.lang.reflect.Field field=MotionBatchPicker.class.getDeclaredField("worker");field.setAccessible(true);
        java.util.concurrent.ExecutorService worker=(java.util.concurrent.ExecutorService)field.get(picker);
        java.util.concurrent.CountDownLatch entered=new java.util.concurrent.CountDownLatch(1),finish=new java.util.concurrent.CountDownLatch(1);
        worker.execute(()->{entered.countDown();try{finish.await();}catch(InterruptedException ex){Thread.currentThread().interrupt();}});
        assertTrue(entered.await(3,java.util.concurrent.TimeUnit.SECONDS));String next="cccccccccccccccccccccccccccccccc";
        try{MotionBatchPicker.ReleaseSession(session);assertFalse(MotionBatchPicker.ReadyToStart());assertFalse(ModelPicker.ReadyToStart());assertFalse(WorkspacePicker.ReadyToStart());
            assertThrows(IllegalStateException.class,()->MotionBatchPicker.Start(activity,next));assertThrows(IllegalStateException.class,()->WorkspacePicker.Start(activity,next));
        }finally{finish.countDown();}
        long until=System.currentTimeMillis()+3000;while(!MotionBatchPicker.ReadyToStart()&&System.currentTimeMillis()<until)Thread.sleep(5);assertTrue(ModelPicker.ReadyToStart());assertTrue(WorkspacePicker.ReadyToStart());
    }
    @Test public void multiSelectionIsReadOnlyBoundedAndDoesNotCopyUntilConfirmed() throws Exception {
        Intent intent=MotionBatchPicker.selectionIntent(); assertTrue(intent.getBooleanExtra(Intent.EXTRA_ALLOW_MULTIPLE,false));
        assertEquals(Intent.FLAG_GRANT_READ_URI_PERMISSION,intent.getFlags()); assertEquals(Intent.ACTION_OPEN_DOCUMENT,intent.getAction());
        assertFalse(ModelPicker.selectionIntent().getBooleanExtra(Intent.EXTRA_ALLOW_MULTIPLE,false));
        int before=SelectedFiles.ENTRIES.size(); MotionBatchPicker picker=start();
        Uri[] values=new Uri[128]; for(int i=0;i<values.length;i++) values[i]=Uri.parse("content://maestro.fixture/export-"+i);
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(values));
        assertEquals("ready",result().getString("kind")); assertEquals(128,result().getInt("count")); assertEquals(before,SelectedFiles.ENTRIES.size());
        File first=new File(copy(0,1).getString("path")); assertTrue(first.exists()); assertEquals(before+1,SelectedFiles.ENTRIES.size());
        File last=new File(copy(127,2).getString("path")); assertTrue(last.exists()); assertFalse(first.exists()); assertEquals(before+1,SelectedFiles.ENTRIES.size());
        MotionBatchPicker.ReleaseFile(result().getString("session"),1); shadowOf(Looper.getMainLooper()).idle(); assertTrue("A stale acknowledgement must not delete the current file",last.exists());
        MotionBatchPicker.ReleaseFile(result().getString("session"),2); shadowOf(Looper.getMainLooper()).idle(); removed(last); assertEquals(before,SelectedFiles.ENTRIES.size());
        assertEquals(5,fixture.data.length());
    }
    @Test public void rejectsTooManySelectionsAndAcceptsSingleUriWithoutClipData() throws Exception {
        MotionBatchPicker picker=start(); Uri[] values=new Uri[129]; for(int i=0;i<values.length;i++) values[i]=SelectedFilesTest.SOURCE;
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(values)); assertEquals("error",result().getString("kind"));
        MotionBatchPicker.ReleaseSession(session); shadowOf(Looper.getMainLooper()).idle(); picker=start();
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));
        assertEquals(1,result().getInt("count")); assertEquals("",copy(0,1).getString("error"));
    }
    @Test public void deduplicatesAndContinuesAfterARejectedSourceThenAllowsRetry() throws Exception {
        MotionBatchPicker picker=start(); Uri invalid=Uri.parse("file:///data/private/settings");
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(invalid,SelectedFilesTest.SOURCE,SelectedFilesTest.SOURCE));
        assertEquals(2,result().getInt("count")); assertFalse(copy(0,1).getString("error").isEmpty());
        JSONObject good=copy(1,2); assertEquals("",good.getString("error")); File file=new File(good.getString("path")); assertTrue(file.exists());
        assertFalse(copy(0,3).getString("error").isEmpty()); assertFalse(file.exists()); assertEquals(5,fixture.data.length());
        JSONObject retry=copy(1,4); assertEquals("",retry.getString("error")); assertTrue(new File(retry.getString("path")).exists());
    }
    @Test public void cancelledReadCannotPublishOverNewerRequestOrReleasedOwner() throws Exception {
        MotionBatchPicker picker=start(); picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));
        String oldSession=result().getString("session");
        MotionBatchPicker.Copy(oldSession,0,1); MotionBatchPicker.CancelCopy(oldSession,1); shadowOf(Looper.getMainLooper()).idle();
        assertTrue(waitFile(1).getString("path").isEmpty());
        File copy=new File(copy(0,2).getString("path")); assertTrue(copy.exists());
        MotionBatchPicker.CancelCopy(oldSession,1); shadowOf(Looper.getMainLooper()).idle(); assertEquals(2,result().getInt("request"));
        MotionBatchPicker.ReleaseSession(session); shadowOf(Looper.getMainLooper()).idle(); removed(copy);
        MotionBatchPicker replacement=start(); replacement.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));
        MotionBatchPicker.Copy(oldSession,0,3); MotionBatchPicker.ReleaseSession(oldSession); shadowOf(Looper.getMainLooper()).idle();
        assertEquals("ready",result().getString("kind")); assertEquals(1,result().getInt("count"));
    }
    @Test public void cancellationAndMalformedClipDataDoNotGrantAnyFiles() throws Exception {
        MotionBatchPicker picker=start(); picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_CANCELED,null);
        assertTrue(result().getString("error").contains("cancelled"));
        MotionBatchPicker.ReleaseSession(session); shadowOf(Looper.getMainLooper()).idle(); picker=start();
        Intent text=new Intent(); text.setClipData(ClipData.newPlainText("text","not a file"));
        picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,text);
        assertEquals("error",result().getString("kind")); assertEquals("",result().getString("path"));
    }
    @Test public void zipMembersAreListedThenCopiedOneAtATimeWithoutReopeningProvider() throws Exception {
        MotionArchiveTest.writeZip(fixture.data,300,false);fixture.name="character-animations.zip";byte[] original=java.nio.file.Files.readAllBytes(fixture.data.toPath());
        int before=SelectedFiles.ENTRIES.size();MotionBatchPicker picker=start();picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));
        JSONObject ready=result();assertEquals("ready",ready.getString("kind"));assertEquals(300,ready.getInt("count"));assertEquals(300,ready.getJSONArray("names").length());
        assertEquals("motion-0000.glb",ready.getJSONArray("names").getString(0));assertEquals(before+1,SelectedFiles.ENTRIES.size());
        // The provider can disappear after selection: the exact archive is retained.
        java.nio.file.Files.write(fixture.data.toPath(),new byte[0]);File first=new File(copy(0,1).getString("path"));assertTrue(first.exists());assertEquals(before+2,SelectedFiles.ENTRIES.size());
        File last=new File(copy(299,2).getString("path"));assertTrue(last.exists());assertFalse(first.exists());assertEquals(32,last.length());
        MotionBatchPicker.ReleaseFile(session,2);shadowOf(Looper.getMainLooper()).idle();removed(last);assertEquals(before+1,SelectedFiles.ENTRIES.size());
        File retry=new File(copy(0,3).getString("path"));assertTrue(retry.exists());MotionBatchPicker.ReleaseSession(session);shadowOf(Looper.getMainLooper()).idle();removed(retry);
        long until=System.currentTimeMillis()+3000;while(!MotionBatchPicker.ReadyToStart()&&System.currentTimeMillis()<until)Thread.sleep(5);assertEquals(before,SelectedFiles.ENTRIES.size());
        java.nio.file.Files.write(fixture.data.toPath(),original);
    }
    @Test public void malformedZipOrMixedZipSelectionReturnsUsefulFailureAndNoCopies() throws Exception {
        fixture.name="damaged.zip";int before=SelectedFiles.ENTRIES.size();MotionBatchPicker picker=start();picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));
        assertEquals("error",result().getString("kind"));assertFalse(result().getString("error").isEmpty());assertEquals(before,SelectedFiles.ENTRIES.size());
        MotionBatchPicker.ReleaseSession(session);shadowOf(Looper.getMainLooper()).idle();picker=start();picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE,Uri.parse("content://maestro.fixture/other")));
        assertTrue(result().getString("error").contains("by itself"));assertEquals(before,SelectedFiles.ENTRIES.size());
    }
    @Test public void releasingDuringPreparationCannotPublishLateReadyOrLeakTheArchive() throws Exception {
        MotionArchiveTest.writeZip(fixture.data,2,false);fixture.name="motions.zip";int before=SelectedFiles.ENTRIES.size();MotionBatchPicker picker=start();
        java.lang.reflect.Field field=MotionBatchPicker.class.getDeclaredField("worker");field.setAccessible(true);java.util.concurrent.ExecutorService worker=(java.util.concurrent.ExecutorService)field.get(picker);
        java.util.concurrent.CountDownLatch entered=new java.util.concurrent.CountDownLatch(1),finish=new java.util.concurrent.CountDownLatch(1);
        worker.execute(()->{entered.countDown();try{finish.await();}catch(InterruptedException ex){Thread.currentThread().interrupt();}});assertTrue(entered.await(3,java.util.concurrent.TimeUnit.SECONDS));
        try{picker.onActivityResult(MotionBatchPicker.REQUEST,Activity.RESULT_OK,selection(SelectedFilesTest.SOURCE));assertEquals("preparing",new JSONObject(MotionBatchPicker.ReadResult(session)).getString("kind"));MotionBatchPicker.ReleaseSession(session);assertFalse(MotionBatchPicker.ReadyToStart());}finally{finish.countDown();}
        long until=System.currentTimeMillis()+4000;while(!MotionBatchPicker.ReadyToStart()&&System.currentTimeMillis()<until){shadowOf(Looper.getMainLooper()).idle();Thread.sleep(5);}
        assertTrue(MotionBatchPicker.ReadyToStart());assertEquals("",MotionBatchPicker.ReadResult(session));assertEquals(before,SelectedFiles.ENTRIES.size());
    }
}
