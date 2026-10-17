// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.graphics.*;
import android.hardware.display.*;
import android.media.Image;
import android.media.ImageReader;
import android.media.projection.*;
import android.os.*;
import android.util.Base64;
import android.view.WindowManager;
import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.nio.ByteBuffer;
import java.security.MessageDigest;
import java.text.SimpleDateFormat;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/** User-selected, video-only screen sharing. No audio capture or durable pixels. */
public final class ScreenShareService extends Service {
    private static final String CHANNEL="maestro-screen-share", STOP="maestro.screen-share.stop";
    private static final Map<String,Lease> pending=new ConcurrentHashMap<>();
    static final class Lease implements BookScreenShare.Capture {
        final String id=UUID.randomUUID().toString();
        Intent consent;
        volatile long touched=SystemClock.elapsedRealtime();
        volatile boolean closed;
        volatile ScreenShareService service;
        private String frame, error;
        Lease(Intent consent) { this.consent=consent; }
        synchronized void publish(String value) { if (!closed) frame=value; }
        synchronized void fail(String value) { error=value; frame=null; }
        public synchronized String read() {
            touched=SystemClock.elapsedRealtime();
            if (closed || error!=null) return "{\"error\":\"screen-share-unavailable\"}";
            String result=frame; frame=null;
            return result==null ? "{}" : result;
        }
        public void stop() {
            closed=true; pending.remove(id);
            synchronized(this) { consent=null; frame=null; }
            ScreenShareService owner=service;
            if(owner!=null) owner.main.post(owner::finish);
        }
        synchronized Intent takeConsent() { Intent result=consent; consent=null; return result; }
    }
    static Lease start(Context context, Intent consent) {
        Lease lease=new Lease(consent); pending.put(lease.id,lease);
        try { context.startForegroundService(new Intent(context,ScreenShareService.class).putExtra("lease",lease.id)); }
        catch(RuntimeException failure) { lease.stop(); throw failure; }
        return lease;
    }
    private final Handler main=new Handler(Looper.getMainLooper());
    private HandlerThread thread;
    private ImageReader reader;
    private VirtualDisplay display;
    private MediaProjection projection;
    private Lease lease;
    private volatile long lastFrame;
    private long started;
    private boolean finished;
    private final Runnable watchdog=new Runnable() {
        public void run() {
            if (lease==null || lease.closed || SystemClock.elapsedRealtime()-lease.touched>2500
                || (lastFrame==0 && SystemClock.elapsedRealtime()-started>8000)) { finish(); return; }
            main.postDelayed(this,500);
        }
    };
    @Override public IBinder onBind(Intent intent) { return null; }
    @Override public int onStartCommand(Intent intent,int flags,int startId) {
        if (intent==null) { finish(); return START_NOT_STICKY; }
        String id=intent.getStringExtra("lease");
        if(STOP.equals(intent.getAction())) {
            if(lease!=null && lease.id.equals(id)) finish();
            return START_NOT_STICKY;
        }
        Lease next=id==null?null:pending.remove(id);
        if (next==null || next.closed || SystemClock.elapsedRealtime()-next.touched>2500 || lease!=null) {
            if(next!=null) next.stop();
            if(lease==null) finish();
            return START_NOT_STICKY;
        }
        lease=next; lease.service=this; started=SystemClock.elapsedRealtime();
        try {
            NotificationManager notifications=getSystemService(NotificationManager.class);
            notifications.createNotificationChannel(new NotificationChannel(CHANNEL,"Maestro screen sharing",NotificationManager.IMPORTANCE_LOW));
            Intent stop=new Intent(this,ScreenShareService.class).setAction(STOP)
                .setData(android.net.Uri.parse("maestro-share:"+id)).putExtra("lease",id);
            PendingIntent action=PendingIntent.getService(this,0,stop,PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
            Notification notice=new Notification.Builder(this,CHANNEL).setSmallIcon(android.R.drawable.ic_menu_camera)
                .setContentTitle("Maestro is sharing your headset view")
                .setContentText("Your selected chat or Live session can receive this view.")
                .setOngoing(true).addAction(new Notification.Action.Builder(null,"Stop sharing",action).build()).build();
            startForeground(4721,notice,ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
            Intent consent=lease.takeConsent();
            if(consent==null || lease.closed) { finish(); return START_NOT_STICKY; }
            projection=getSystemService(MediaProjectionManager.class).getMediaProjection(Activity.RESULT_OK,consent);
            if(projection==null) throw new IllegalStateException("No projection");
            projection.registerCallback(new MediaProjection.Callback() {
                @Override public void onStop() { finish(); }
            },main);
            WindowManager wm=getSystemService(WindowManager.class);
            Rect bounds;
            if(Build.VERSION.SDK_INT>=30) bounds=wm.getMaximumWindowMetrics().getBounds();
            else { android.util.DisplayMetrics metrics=new android.util.DisplayMetrics(); wm.getDefaultDisplay().getRealMetrics(metrics); bounds=new Rect(0,0,metrics.widthPixels,metrics.heightPixels); }
            int[] size=outputSize(bounds.width(),bounds.height());
            thread=new HandlerThread("MaestroScreenShare"); thread.start();
            reader=ImageReader.newInstance(size[0],size[1],PixelFormat.RGBA_8888,2);
            reader.setOnImageAvailableListener(this::image,new Handler(thread.getLooper()));
            // On Quest the compositor writes directly to this Surface. Do not assume
            // VirtualDisplay.resize() or Android's letterboxing controls that output.
            display=projection.createVirtualDisplay("Maestro user screen share",size[0],size[1],
                getResources().getConfiguration().densityDpi,DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                reader.getSurface(),null,main);
            main.post(watchdog);
        } catch(RuntimeException failure) { if(lease!=null) lease.fail("screen-share-unavailable"); finish(); }
        return START_NOT_STICKY;
    }
    static int[] outputSize(int width,int height) {
        if(width<1 || height<1) throw new IllegalArgumentException("Invalid capture size");
        double scale=Math.min(1,512d/Math.max(width,height));
        return new int[]{Math.max(1,(int)Math.round(width*scale)),Math.max(1,(int)Math.round(height*scale))};
    }
    private void image(ImageReader source) {
        Lease owner=lease;
        try(Image image=source.acquireLatestImage()) {
            long now=SystemClock.elapsedRealtime();
            if(image==null || owner==null || owner.closed || now-owner.touched>2500 || now-lastFrame<1000) return;
            lastFrame=now;
            Rect crop=image.getCropRect(); Image.Plane plane=image.getPlanes()[0];
            Bitmap bitmap=rgba(plane.getBuffer(),plane.getRowStride(),plane.getPixelStride(),crop);
            try { String frame=encode(bitmap,new Date()); if(!owner.closed) owner.publish(frame); }
            finally { bitmap.recycle(); }
        } catch(Exception failure) { if(owner!=null && !owner.closed) { owner.fail("screen-share-unavailable"); main.post(this::finish); } }
    }
    static Bitmap rgba(ByteBuffer bytes,int rowStride,int pixelStride,Rect crop) {
        int width=crop.width(),height=crop.height();
        if(width<1 || height<1 || width>512 || height>512 || crop.left<0 || crop.top<0 || pixelStride<4
            || rowStride<(crop.right-1)*pixelStride+4) throw new IllegalArgumentException("Invalid frame layout");
        long last=(long)(crop.bottom-1)*rowStride+(long)(crop.right-1)*pixelStride+3;
        if(last>=bytes.limit()) throw new IllegalArgumentException("Incomplete frame");
        int[] pixels=new int[width*height];
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            int offset=(crop.top+y)*rowStride+(crop.left+x)*pixelStride;
            pixels[y*width+x]=((bytes.get(offset+3)&255)<<24)|((bytes.get(offset)&255)<<16)
                |((bytes.get(offset+1)&255)<<8)|(bytes.get(offset+2)&255);
        }
        return Bitmap.createBitmap(pixels,width,height,Bitmap.Config.ARGB_8888);
    }
    static String encode(Bitmap bitmap,Date capturedAt) throws Exception {
        byte[] bytes=null;
        for(int quality=75;quality>=35;quality-=20) {
            ByteArrayOutputStream output=new ByteArrayOutputStream();
            if(!bitmap.compress(Bitmap.CompressFormat.JPEG,quality,output)) throw new IllegalStateException("Encoding failed");
            bytes=output.toByteArray(); if(bytes.length<=98304) break;
        }
        if(bytes==null || bytes.length>98304) throw new IllegalArgumentException("Frame too large");
        StringBuilder hash=new StringBuilder();
        for(byte value:MessageDigest.getInstance("SHA-256").digest(bytes)) hash.append(String.format(Locale.ROOT,"%02x",value&255));
        SimpleDateFormat timestamp=new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",Locale.ROOT); timestamp.setTimeZone(TimeZone.getTimeZone("UTC"));
        JSONObject capture=new JSONObject().put("captureId",UUID.randomUUID().toString().replace("-",""))
            .put("sha256",hash.toString()).put("mimeType","image/jpeg").put("width",bitmap.getWidth()).put("height",bitmap.getHeight())
            .put("capturedAt",timestamp.format(capturedAt));
        return new JSONObject().put("frame",new JSONObject().put("sourceId","maestro-camera:mixed-view").put("capture",capture)
            .put("data",Base64.encodeToString(bytes,Base64.NO_WRAP))).toString();
    }
    private void finish() {
        if(finished) return; finished=true; main.removeCallbacksAndMessages(null);
        if(lease!=null) { lease.closed=true; lease.fail("screen-share-unavailable"); lease.consent=null; lease.service=null; }
        if(display!=null) { try { display.release(); } catch(RuntimeException ignored) { } display=null; }
        if(reader!=null) { try { reader.close(); } catch(RuntimeException ignored) { } reader=null; }
        if(projection!=null) { try { projection.stop(); } catch(RuntimeException ignored) { } projection=null; }
        if(thread!=null) { thread.quitSafely(); thread=null; }
        stopForeground(STOP_FOREGROUND_REMOVE); stopSelf();
    }
    @Override public void onDestroy() { finish(); super.onDestroy(); }
}
