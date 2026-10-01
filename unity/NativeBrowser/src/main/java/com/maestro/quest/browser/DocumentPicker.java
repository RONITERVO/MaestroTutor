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
    private volatile SelectedFiles files;
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
            if(!"model".equals(kind)&&!"archive".equals(kind))throw new IllegalArgumentException("Unknown selected file kind");
            if(requestId==null||!requestId.matches("[a-f0-9]{32}"))throw new IllegalArgumentException("Invalid file request identity");
            if(current!=null) {
                if(current.id.equals(requestId)&&current.kind.equals(kind))return requestId;
                throw new IllegalStateException("Finish or cancel the current file selection first");
            }
            if(retiring!=null)throw new IllegalStateException("Wait for the previous selected stream to close");
            if(activity==null||activity.isFinishing()||activity.isDestroyed())throw new IllegalStateException("Resume Maestro before choosing a file");
            DocumentPicker picker=new DocumentPicker();picker.id=requestId;picker.kind=kind;picker.maximumBytes="model".equals(kind)?64L*1024*1024:MAX_BYTES;FileSelectionGate.acquire(picker);current=picker;
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
            try{return new JSONObject().put("id",current.id).put("phase",current.phase).put("name",current.name).put("path",current.path).put("error",current.error).toString();}
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
                    SelectedFiles cache=new SelectedFiles(activity,maximumBytes,maximumBytes);files=cache;
                    if(closed){cache.close();return;}
                    Uri selected=cache.copy(new Uri[]{source})[0];
                    SelectedFiles.Entry entry=SelectedFiles.ENTRIES.get(selected.getLastPathSegment());
                    if(entry==null)throw new java.io.IOException("Selected copy is missing");
                    String privatePath=entry.file.getCanonicalPath();
                    synchronized(GATE){if(current==this&&!closed&&phase.equals("copying")){name=entry.name;path=privatePath;phase="selected";main.removeCallbacks(timeout);}}
                }catch(Exception ex){fail(ex instanceof SelectedFiles.SelectionException?ex.getMessage():"The file could not be copied. Try a local file.");}
                finally {if(closed&&files!=null)files.close();}
            });
        }
    }
    private void fail(String message) {
        synchronized(GATE){
            if(current!=this||closed||phase.equals("selected")||phase.equals("cancelled")||phase.equals("failed"))return;
            phase="failed";path="";name="";error=message;main.removeCallbacks(timeout);
            if(files!=null)files.cancelCopy();
        }
    }
    private void closeFiles(){
        if(files!=null)files.cancelCopy();
        worker.execute(()->{try{if(files!=null)files.close();}finally{synchronized(GATE){if(retiring==this)retiring=null;FileSelectionGate.release(this);}}});worker.shutdown();
    }
    @Override public void onDestroy(){
        synchronized(GATE){if(current==this&&!closed)fail("File selection was interrupted. Choose the file again.");}
        // Keep the terminal result and its selected bytes until the exact owner releases them.
        // A selected copy can still be under validation by Unity when the Activity is recreated.
        super.onDestroy();
    }
}
