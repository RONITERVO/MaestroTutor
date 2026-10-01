// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.app.Activity;
import android.content.Intent;
import android.os.Looper;
import android.net.Uri;
import java.io.File;
import java.time.Duration;
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
    static final String A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    Activity activity;SelectedFilesTest.Fixture fixture;
    @Before public void setup() throws Exception {
        activity=Robolectric.buildActivity(Activity.class).setup().get();fixture=SelectedFilesTest.install(activity);
        ModelPicker.Release(A);ModelPicker.Release(B);shadowOf(Looper.getMainLooper()).idle();
    }
    @After public void cleanup(){ModelPicker.Release(A);ModelPicker.Release(B);shadowOf(Looper.getMainLooper()).idle();fixture.data.delete();activity.finish();}
    DocumentPicker start(String id){
        long deadline=System.currentTimeMillis()+3000;
        while(!ModelPicker.ReadyToStart()&&ModelPicker.Read(id).isEmpty()&&System.currentTimeMillis()<deadline)try{Thread.sleep(5);}catch(InterruptedException ex){throw new AssertionError(ex);}
        assertEquals(id,ModelPicker.Start(activity,id));shadowOf(Looper.getMainLooper()).idle();activity.getFragmentManager().executePendingTransactions();
        return (DocumentPicker)activity.getFragmentManager().findFragmentByTag("MaestroDocumentPicker");
    }
    JSONObject read(String id) throws Exception{return new JSONObject(ModelPicker.Read(id));}
    void waitPhase(String phase) throws Exception {
        long until=System.currentTimeMillis()+4000;
        while(!read(A).getString("phase").equals(phase)&&System.currentTimeMillis()<until){Thread.sleep(10);shadowOf(Looper.getMainLooper()).idle();}
        assertEquals(phase,read(A).getString("phase"));
    }
    @Test public void singleExplicitReadOnlyChoiceHasStableIdAndSurvivesPause() throws Exception {
        Intent intent=ModelPicker.selectionIntent();assertEquals(Intent.ACTION_OPEN_DOCUMENT,intent.getAction());assertTrue(intent.hasCategory(Intent.CATEGORY_OPENABLE));
        assertEquals(Intent.FLAG_GRANT_READ_URI_PERMISSION,intent.getFlags());assertFalse(intent.getBooleanExtra(Intent.EXTRA_ALLOW_MULTIPLE,false));
        DocumentPicker picker=start(A);assertEquals(A,ModelPicker.Start(activity,A));assertThrows(IllegalStateException.class,()->ModelPicker.Start(activity,B));
        picker.onPause();assertEquals("selecting",read(A).getString("phase"));picker.onResume();
        picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_CANCELED,null);assertEquals("cancelled",read(A).getString("phase"));
        assertEquals("",read(A).getString("path"));assertEquals("",ModelPicker.Read(B));
    }
    @Test public void copiedModelIsPrivateAndExactReleaseKeepsNewOwnerSafe() throws Exception {
        DocumentPicker picker=start(A);picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));waitPhase("selected");
        File copy=new File(read(A).getString("path"));assertTrue(copy.exists());assertEquals(5,copy.length());
        assertEquals(new File(ModelPicker.CacheRoot(activity)),copy.getParentFile().getParentFile());
        ModelPicker.Release(B);assertTrue(copy.exists());assertEquals("selected",read(A).getString("phase"));
        ModelPicker.Release(A);shadowOf(Looper.getMainLooper()).idle();DocumentPicker next=start(B);
        picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_CANCELED,null);ModelPicker.Release(A);
        assertEquals("selecting",read(B).getString("phase"));assertEquals("",ModelPicker.Read(A));assertNotSame(picker,next);
        long until=System.currentTimeMillis()+3000;while(copy.exists()&&System.currentTimeMillis()<until)Thread.sleep(10);assertFalse(copy.exists());
    }
    @Test public void forgedSourcesAndOversizedMetadataFailWithoutReturningPaths() throws Exception {
        DocumentPicker picker=start(A);picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(Uri.parse("file:///private/settings")));waitPhase("failed");
        assertEquals("",read(A).getString("path"));ModelPicker.Release(A);shadowOf(Looper.getMainLooper()).idle();
        fixture.advertisedSize=ModelPicker.MAX_BYTES+1;picker=start(A);picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));waitPhase("failed");
        assertTrue(read(A).getString("error").contains("64 MB"));assertEquals("",read(A).getString("path"));
    }
    @Test public void timeoutAndDestroyedChooserHaveTerminalOutcomes() throws Exception {
        DocumentPicker picker=start(A);shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMinutes(5));assertEquals("failed",read(A).getString("phase"));
        picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));assertEquals("failed",read(A).getString("phase"));
        ModelPicker.Release(A);shadowOf(Looper.getMainLooper()).idle();picker=start(A);picker.onDestroy();assertEquals("failed",read(A).getString("phase"));
    }
    @Test public void releasingBlockedCopyDoesNotPermitAnUnboundedSecondWorker() throws Exception {
        java.util.concurrent.CountDownLatch entered=new java.util.concurrent.CountDownLatch(1),released=new java.util.concurrent.CountDownLatch(1);
        SelectedFilesTest.Fixture blocked=new SelectedFilesTest.Fixture(){
            @Override public android.database.Cursor query(Uri uri,String[] projection,String selection,String[] args,String order){
                entered.countDown();try{if(!released.await(10,java.util.concurrent.TimeUnit.SECONDS))throw new IllegalStateException("Test provider timed out");}catch(InterruptedException ex){throw new IllegalStateException(ex);}
                return super.query(uri,projection,selection,args,order);
            }
        };
        blocked.data=fixture.data;blocked.attachInfo(activity,activity.getPackageManager().resolveContentProvider(SelectedFilesTest.SOURCE.getAuthority(),0));
        org.robolectric.shadows.ShadowContentResolver.registerProviderInternal(SelectedFilesTest.SOURCE.getAuthority(),blocked);
        try{
            DocumentPicker picker=start(A);picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));
            assertTrue(entered.await(3,java.util.concurrent.TimeUnit.SECONDS));ModelPicker.Release(A);assertFalse(ModelPicker.ReadyToStart());
            assertThrows(IllegalStateException.class,()->ModelPicker.Start(activity,B));
        }finally{released.countDown();}
        long deadline=System.currentTimeMillis()+3000;while(!ModelPicker.ReadyToStart()&&System.currentTimeMillis()<deadline)Thread.sleep(10);
        assertTrue(ModelPicker.ReadyToStart());
    }
    @Test public void duplicateResultsNeverReplaceTheFirstCopiedFile() throws Exception {
        DocumentPicker picker=start(A);picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_OK,new Intent().setData(SelectedFilesTest.SOURCE));waitPhase("selected");String path=read(A).getString("path");
        picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_CANCELED,null);assertEquals("selected",read(A).getString("phase"));assertEquals(path,read(A).getString("path"));
        picker.onDestroy();assertEquals("selected",read(A).getString("phase"));assertTrue(new File(path).exists());
    }
    @Test public void modelAndArchiveShareOneChooserButNeverReadOrReleaseEachOthersRequest() throws Exception {
        DocumentPicker picker=start(A);
        assertFalse(WorkspacePicker.ReadyToStart());assertEquals("",WorkspacePicker.Read(A));
        assertThrows(IllegalStateException.class,()->WorkspacePicker.Start(activity,A));
        WorkspacePicker.Release(A);assertEquals("selecting",read(A).getString("phase"));
        picker.onActivityResult(ModelPicker.REQUEST,Activity.RESULT_CANCELED,null);ModelPicker.Release(A);
        long until=System.currentTimeMillis()+3000;while(!WorkspacePicker.ReadyToStart()&&System.currentTimeMillis()<until)Thread.sleep(10);
        try{
            assertEquals(B,WorkspacePicker.Start(activity,B));ModelPicker.Release(B);assertEquals("",ModelPicker.Read(B));assertFalse(WorkspacePicker.Read(B).isEmpty());
        }finally{WorkspacePicker.Release(B);shadowOf(Looper.getMainLooper()).idle();}
    }

}
