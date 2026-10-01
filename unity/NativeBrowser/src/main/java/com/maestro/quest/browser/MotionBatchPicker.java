// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.Activity;
import android.app.Fragment;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import org.json.JSONObject;
import java.util.LinkedHashSet;
import java.io.File;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Native-only, user-confirmed motion imports. No URI or path is exposed to page JS. */
@SuppressWarnings("deprecation")
public final class MotionBatchPicker extends Fragment {
    static final int REQUEST=4811, MAX_FILES=128;
    private static final Object GATE=new Object();
    private static volatile MotionBatchPicker current;
    private static volatile String result="";
    private final Handler main=new Handler(Looper.getMainLooper());
    private final ExecutorService worker=Executors.newSingleThreadExecutor(r -> { Thread t=new Thread(r,"Maestro motion copy"); t.setDaemon(true); return t; });
    private volatile SelectedFiles files;
    private volatile boolean closed;
    private volatile int cancelledThrough;
    private String session;
    private Uri[] sources;
    private int requestId, selectedIndex=-1;
    private boolean started, copying;
    private final Runnable selectionTimeout=() -> selectionError("File selection timed out. Choose files again.");
    private final Runnable copyTimeout=() -> cancelCopy(requestId,"Reading this file timed out. Try a local copy and Retry failed.");

    public static boolean ReadyToStart(){synchronized(GATE){return current==null&&FileSelectionGate.ready();}}
    public static String CacheRoot(Activity activity) throws java.io.IOException{return DocumentPicker.CacheRoot(activity);}
    public static String Start(Activity activity,String requested) {
        synchronized(GATE){
            if(requested==null||!requested.matches("[a-f0-9]{32}"))throw new IllegalArgumentException("Invalid batch identity");
            if(current!=null){if(current.session.equals(requested))return requested;throw new IllegalStateException("Finish or clear the current batch first");}
            if(activity==null||activity.isFinishing()||activity.isDestroyed())throw new IllegalStateException("Resume Maestro before choosing files");
            MotionBatchPicker owner=new MotionBatchPicker();owner.session=requested;FileSelectionGate.acquire(owner);current=owner;result="";
            activity.runOnUiThread(()->{
                if(current!=owner||owner.closed)return;
                try {activity.getFragmentManager().beginTransaction().add(owner,"MaestroMotionBatchPicker").commit();}
                catch(RuntimeException error){owner.selectionError("The document picker is unavailable. Resume Maestro and try again.");}
            });
            return requested;
        }
    }
    public static String ReadResult(String session){synchronized(GATE){return owns(session)?result:"";}}
    public static void Copy(String session,int index,int request) { new Handler(Looper.getMainLooper()).post(() -> { synchronized(GATE){if (owns(session)) current.copy(index,request);} }); }
    public static void CancelCopy(String session,int request) { new Handler(Looper.getMainLooper()).post(() -> { synchronized(GATE){if (owns(session)) current.cancelCopy(request,"File read cancelled.");} }); }
    public static void ReleaseFile(String session,int request) {
        new Handler(Looper.getMainLooper()).post(() -> {
            synchronized(GATE){if (!owns(session) || current.requestId != request || current.copying) return;
            MotionBatchPicker owner=current; owner.worker.execute(owner::clearCopy);}
        });
    }
    private static boolean owns(String session) { return current != null && current.session.equals(session); }
    public static void ReleaseSession(String session) {
        MotionBatchPicker owner;
        synchronized(GATE){if(!owns(session))return;owner=current;current=null;result="";owner.close();}
        owner.main.post(()->{if(owner.isAdded())owner.getFragmentManager().beginTransaction().remove(owner).commitAllowingStateLoss();});
    }
    static Intent selectionIntent() { return ModelPicker.selectionIntent().putExtra(Intent.EXTRA_ALLOW_MULTIPLE,true); }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (current != this) { getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss(); return; }
        if (started) return; started=true; main.postDelayed(selectionTimeout,300000);
        try { startActivityForResult(selectionIntent(),REQUEST); }
        catch (RuntimeException error) { selectionError("No document picker is available. Resume Maestro and try again."); }
    }
    @Override public void onActivityResult(int request,int code,Intent data) {
        if (request != REQUEST || closed || current != this || sources != null || !result.isEmpty()) return;
        if (code != Activity.RESULT_OK || data == null) { selectionError("File selection cancelled."); return; }
        ClipData clip=data.getClipData(); int count=clip == null ? (data.getData() == null ? 0 : 1) : clip.getItemCount();
        if (count < 1 || count > MAX_FILES) { selectionError("Choose between 1 and 128 animation files."); return; }
        LinkedHashSet<Uri> unique=new LinkedHashSet<>();
        for (int i=0;i<count;i++) {
            Uri uri=clip == null ? data.getData() : clip.getItemAt(i).getUri();
            if (uri == null || uri.toString().length() > 8192) { selectionError("The selected documents are unavailable. Choose local files again."); return; }
            unique.add(uri);
        }
        sources=unique.toArray(new Uri[0]); main.removeCallbacks(selectionTimeout);
        // Selecting does not open provider streams. Unity must explicitly confirm
        // the batch before asking for the first private copy.
        publish("ready",sources.length,-1,0,"","","");
    }
    private void selectionError(String error) {
        if (closed || current != this || sources != null || !result.isEmpty()) return;
        main.removeCallbacks(selectionTimeout); publish("error",0,-1,0,"","",error);
    }
    private void copy(int index,int request) {
        if (closed || current != this || sources == null || copying || request <= requestId) return;
        requestId=request; selectedIndex=index;
        if (index < 0 || index >= sources.length) { publish("file",sources.length,index,request,"","","The selected file is unavailable."); return; }
        Activity activity=getActivity(); if (activity == null) { publish("file",sources.length,index,request,"","","Resume Maestro and choose files again."); return; }
        copying=true; result=""; main.postDelayed(copyTimeout,120000); Uri uri=sources[index];
        worker.execute(() -> {
            try {
                clearCopy(); if (closed || request <= cancelledThrough) return;
                SelectedFiles cache=new SelectedFiles(activity); files=cache;
                if (closed || request <= cancelledThrough) { clearCopy(); return; }
                Uri selected=cache.copy(new Uri[] { uri })[0];
                if (closed || request <= cancelledThrough) { clearCopy(); return; }
                SelectedFiles.Entry entry=SelectedFiles.ENTRIES.get(selected.getLastPathSegment());
                if (entry == null) throw new IllegalStateException();
                main.post(() -> finish(index,request,entry.file.getAbsolutePath(),entry.name,""));
            } catch (Exception error) {
                String message=error instanceof SelectedFiles.SelectionException ? error.getMessage() : "This file could not be read. Choose a local GLB or VRM export.";
                clearCopy(); main.post(() -> finish(index,request,"","",message));
            }
        });
    }
    private void finish(int index,int request,String path,String name,String error) {
        if (closed || current != this || request != requestId || !copying) return;
        copying=false; main.removeCallbacks(copyTimeout); publish("file",sources.length,index,request,path,name,error);
    }
    private void cancelCopy(int request,String error) {
        if (closed || request != requestId || !copying) return;
        copying=false; cancelledThrough=request; main.removeCallbacks(copyTimeout); SelectedFiles cache=files; if (cache != null) cache.cancelCopy();
        worker.execute(this::clearCopy); publish("file",sources.length,selectedIndex,request,"","",error);
    }
    // Worker-owned: clear the previous file before starting the next. This keeps
    // private disk use bounded to one 64 MiB source even for 128 selections.
    private void clearCopy() { if (files != null) { files.close(); files=null; } }
    private void publish(String kind,int count,int index,int request,String path,String name,String error) {
        if(current!=this||closed)return;
        try { result=new JSONObject().put("session",session).put("kind",kind).put("count",count).put("index",index).put("request",request).put("path",path).put("name",name).put("error",error).toString(); }
        catch (Exception ignored) { result="{\"kind\":\"error\",\"error\":\"File selection failed.\"}"; }
    }
    private void close() {
        if (closed) return; closed=true; main.removeCallbacks(selectionTimeout); main.removeCallbacks(copyTimeout);
        SelectedFiles cache=files; if (cache != null) cache.cancelCopy(); worker.execute(()->{try{clearCopy();}finally{FileSelectionGate.release(this);}}); worker.shutdown(); sources=null;
    }
    @Override public void onDestroy() {
        // A selected copy may still be under Unity validation. Keep its bytes
        // until the exact source owner releases them, even after Activity loss.
        if(current==this&&!closed){if(copying)cancelCopy(requestId,"File selection was interrupted. Choose files again.");else if(sources==null)selectionError("File selection was interrupted. Choose files again.");}
        super.onDestroy();
    }
}
