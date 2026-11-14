// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import java.io.File;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import org.json.JSONObject;
import org.json.JSONArray;
import android.database.Cursor;
import android.provider.OpenableColumns;
import android.os.CancellationSignal;
import java.util.Locale;

/** Native file selection has an exact request identity and survives ordinary app pause.
 * A result grants only a bounded private copy. It never activates a workspace or reaches JS. */
@SuppressWarnings("deprecation")
public final class DocumentPicker extends Fragment {
    static final int REQUEST=4811;
    static final long MAX_BYTES=512L*1024*1024;
    private static final Object GATE=new Object();
    private static DocumentPicker current,retiring;
    private final Handler main=new Handler(Looper.getMainLooper());
    private final ExecutorService worker=Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"Maestro file selection");t.setDaemon(true);return t;});
    private volatile SelectedFiles files,archiveFiles;
    private volatile CancellationSignal metadataRead;
    private volatile boolean preparationCancelled;
    private volatile int cancelledThrough=-1;
    private MotionArchive modelArchive;
    private JSONArray members;
    private int memberRequest;
    private volatile boolean closed;
    private String kind;
    private long maximumBytes;
    private String id,phase="opening",name="",path="",error="";
    private boolean started;
    private final Runnable timeout=()->fail("File selection timed out. Choose the file again.");

    public static String CacheRoot(Activity activity) throws java.io.IOException {
        return new File(activity.getCacheDir(),"maestro-selected").getCanonicalPath();
    }
    /** Returns synchronously so Unity can finish its opening receipt before focus is lost. */
    public static String Start(Activity activity,String requestId,String kind) {
        synchronized(GATE) {
            if(!"model".equals(kind)&&!"archive".equals(kind)&&!"audio".equals(kind)&&!"image".equals(kind))throw new IllegalArgumentException("Unknown selected file kind");
            if(requestId==null||!requestId.matches("[a-f0-9]{32}"))throw new IllegalArgumentException("Invalid file request identity");
            if(current!=null) {
                if(current.id.equals(requestId)&&current.kind.equals(kind))return requestId;
                throw new IllegalStateException("Finish or cancel the current file selection first");
            }
            if(retiring!=null)throw new IllegalStateException("Wait for the previous selected stream to close");
            if(activity==null||activity.isFinishing()||activity.isDestroyed())throw new IllegalStateException("Resume Maestro before choosing a file");
            DocumentPicker picker=new DocumentPicker();picker.id=requestId;picker.kind=kind;picker.maximumBytes="image".equals(kind)?ImagePicker.MAX_BYTES:"audio".equals(kind)?AudioPicker.MAX_BYTES:"model".equals(kind)?64L*1024*1024:MAX_BYTES;FileSelectionGate.acquire(picker);current=picker;
            activity.runOnUiThread(()->{
                synchronized(GATE) {
                    if(current!=picker||picker.closed)return;
                    try {activity.getFragmentManager().beginTransaction().add(picker,"MaestroDocumentPicker").commit();}
                    catch(RuntimeException ex){picker.fail("The document picker is unavailable. Resume Maestro and try again.");}
                }
            });
            return requestId;
        }
    }
    public static boolean ReadyToStart(){synchronized(GATE){return current==null&&retiring==null&&FileSelectionGate.ready();}}
    public static String Read(String requestId,String kind) {
        synchronized(GATE) {
            if(current==null||!current.id.equals(requestId)||!current.kind.equals(kind))return "";
            try{return new JSONObject().put("id",current.id).put("phase",current.phase).put("name",current.name).put("path",current.path).put("error",current.error).put("memberRequest",current.memberRequest).put("members",current.phase.equals("archive")&&current.members!=null?current.members:new JSONArray()).toString();}
            catch(Exception ex){throw new IllegalStateException("File selection status is unavailable",ex);}
        }
    }
    /** A stale owner cannot close a newer selection. Files are released only after native reading ends. */
    public static void Release(String requestId,String kind) {
        DocumentPicker previous;
        synchronized(GATE){if(current==null||!current.id.equals(requestId)||!current.kind.equals(kind))return;previous=current;current=null;retiring=previous;previous.closed=true;}
        previous.closeFiles();
        previous.main.post(()->{
            previous.main.removeCallbacks(previous.timeout);
            if(previous.isAdded())previous.getFragmentManager().beginTransaction().remove(previous).commitAllowingStateLoss();
        });
    }
    static Intent selectionIntent(){
        // Providers disagree about GLB/VRM MIME types. Validate the selected bytes natively.
        Uri downloads=android.provider.DocumentsContract.buildDocumentUri("com.android.externalstorage.documents","primary:"+android.os.Environment.DIRECTORY_DOWNLOADS);
        return new Intent(Intent.ACTION_OPEN_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType("*/*")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION).putExtra(android.provider.DocumentsContract.EXTRA_INITIAL_URI,downloads);
    }
    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        synchronized(GATE){
            // Android may recreate an old Fragment after process loss. Never adopt its result.
            if(current!=this||closed){getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();return;}
            if(started)return;started=true;phase="selecting";
        }
        main.postDelayed(timeout,300000);
        try{startActivityForResult(selectionIntent(),REQUEST);}
        catch(RuntimeException ex){fail("No document picker is available. Resume Maestro and try again.");}
    }
    @Override public void onActivityResult(int request,int code,Intent data) {
        if(request!=REQUEST)return;
        synchronized(GATE){
            if(current!=this||closed||!phase.equals("selecting"))return;
            if(code!=Activity.RESULT_OK||data==null||data.getData()==null||data.getClipData()!=null&&data.getClipData().getItemCount()>1){phase="cancelled";main.removeCallbacks(timeout);return;}
            phase="copying";
            final Activity activity=getActivity();final Uri source=data.getData();
            if(activity==null){fail("Selection was interrupted. Resume Maestro and choose the file again.");return;}
            // Submit under the gate so Release cannot shut down this executor between acceptance and submit.
            worker.execute(()->{
                try {
                    boolean zip=false;
                    if(kind.equals("model")){
                        if(!SelectedFiles.allowedUri(activity,source))throw MotionArchive.invalid("The selected file source is unavailable");
                        CancellationSignal signal=new CancellationSignal();metadataRead=signal;MotionArchive.check(()->closed||preparationCancelled);
                        try(Cursor cursor=activity.getContentResolver().query(source,new String[]{OpenableColumns.DISPLAY_NAME},null,null,null,signal)){
                            if(cursor!=null&&cursor.moveToFirst()){int column=cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);if(column>=0&&!cursor.isNull(column))zip=cursor.getString(column).toLowerCase(Locale.ROOT).endsWith(".zip");}
                        }
                        String mime=activity.getContentResolver().getType(source);zip|="application/zip".equals(mime)||"application/x-zip-compressed".equals(mime);metadataRead=null;
                    }
                    long limit=zip?MotionArchive.MAX_BYTES:maximumBytes;
                    SelectedFiles cache=new SelectedFiles(activity,limit,limit);if(zip)archiveFiles=cache;else files=cache;
                    if(closed||preparationCancelled){cache.close();return;}
                    Uri selected=cache.copy(new Uri[]{source},()->closed||preparationCancelled)[0];
                    SelectedFiles.Entry entry=SelectedFiles.ENTRIES.get(selected.getLastPathSegment());
                    if(entry==null)throw new java.io.IOException("Selected copy is missing");
                    if(zip){
                        modelArchive=new MotionArchive(entry.file,()->closed||preparationCancelled);JSONArray list=new JSONArray();
                        for(int i=0;i<modelArchive.count();i++)list.put(new JSONObject().put("name",modelArchive.name(i)).put("bytes",modelArchive.bytes(i)));
                        synchronized(GATE){if(current==this&&!closed&&phase.equals("copying")){members=list;name=entry.name;path="";phase="archive";main.removeCallbacks(timeout);}}
                    }else{
                        String privatePath=entry.file.getCanonicalPath();
                        synchronized(GATE){if(current==this&&!closed&&phase.equals("copying")){name=entry.name;path=privatePath;phase="selected";main.removeCallbacks(timeout);}}
                    }
                }catch(Exception ex){fail(ex instanceof SelectedFiles.SelectionException?ex.getMessage():"The file could not be copied. Try a local file.");}
                finally {metadataRead=null;if(closed)clearFiles();}
            });
        }
    }
    /** Native-only member choice. A newer request can begin only after Unity releases its read. */
    public static boolean SelectModelMember(String id,int index,int request){
        synchronized(GATE){
            DocumentPicker owner=current;
            if(owner==null||owner.closed||!owner.kind.equals("model")||!owner.id.equals(id)||owner.modelArchive==null||
                !(owner.phase.equals("archive")||owner.phase.equals("selected")||owner.phase.equals("memberFailed"))||
                index<0||index>=owner.modelArchive.count()||request<=owner.memberRequest)return false;
            Activity activity=owner.getActivity();if(activity==null)return false;
            owner.memberRequest=request;owner.phase="copying";owner.path="";owner.name="";owner.error="";owner.main.postDelayed(owner.timeout,120000);
            owner.worker.execute(()->{
                try{
                    if(owner.files!=null){owner.files.close();owner.files=null;}
                    MotionArchive.check(()->owner.closed||request<=owner.cancelledThrough);
                    SelectedFiles cache=new SelectedFiles(activity);owner.files=cache;
                    Uri selected=owner.modelArchive.copy(index,cache,()->owner.closed||request<=owner.cancelledThrough);
                    SelectedFiles.Entry entry=SelectedFiles.ENTRIES.get(selected.getLastPathSegment());
                    if(entry==null)throw new java.io.IOException("Selected member is unavailable");
                    synchronized(GATE){if(current==owner&&!owner.closed&&owner.memberRequest==request&&owner.phase.equals("copying")){
                        owner.name=entry.name;owner.path=entry.file.getCanonicalPath();owner.phase="selected";owner.main.removeCallbacks(owner.timeout);
                    }}
                }catch(Exception ex){owner.failMember(request,ex instanceof SelectedFiles.SelectionException?ex.getMessage():"This ZIP model could not be read. Try another file or select the ZIP again.");}
                finally{if(owner.closed)owner.clearFiles();else if(request<=owner.cancelledThrough&&owner.files!=null){owner.files.close();owner.files=null;}}
            });return true;
        }
    }
    private void failMember(int request,String message){
        synchronized(GATE){if(memberRequest==request)fail(message);}
    }
    private void clearFiles(){
        if(files!=null){files.close();files=null;}
        if(modelArchive!=null){try{modelArchive.close();}catch(java.io.IOException ignored){}modelArchive=null;}
        if(archiveFiles!=null){archiveFiles.close();archiveFiles=null;}
    }
    private void cancelReads(){
        if(files!=null)files.cancelCopy();if(archiveFiles!=null)archiveFiles.cancelCopy();
        CancellationSignal signal=metadataRead;if(signal!=null){Thread cancel=new Thread(signal::cancel,"Cancel model ZIP selection");cancel.setDaemon(true);cancel.start();}
    }
    private void fail(String message) {
        synchronized(GATE){
            if(current!=this||closed||phase.equals("selected")||phase.equals("cancelled")||phase.equals("failed")||phase.equals("memberFailed"))return;
            if(modelArchive!=null&&memberRequest>0){phase="memberFailed";cancelledThrough=memberRequest;}else{phase="failed";preparationCancelled=true;}
            path="";name="";error=message;main.removeCallbacks(timeout);cancelReads();
        }
    }
    private void closeFiles(){
        cancelReads();
        worker.execute(()->{try{clearFiles();}finally{synchronized(GATE){if(retiring==this)retiring=null;FileSelectionGate.release(this);}}});worker.shutdown();
    }
    @Override public void onDestroy(){
        synchronized(GATE){if(current==this&&!closed)fail("File selection was interrupted. Choose the file again.");}
        // Keep the terminal result and its selected bytes until the exact owner releases them.
        // A selected copy can still be under validation by Unity when the Activity is recreated.
        super.onDestroy();
    }
}
