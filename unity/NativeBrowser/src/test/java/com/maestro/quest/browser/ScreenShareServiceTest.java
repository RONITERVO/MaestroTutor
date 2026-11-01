// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.content.*;
import android.content.pm.*;
import android.graphics.*;
import android.os.*;
import org.json.JSONObject;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import java.nio.ByteBuffer;
import java.util.Date;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class ScreenShareServiceTest {
    @Test public void manifestRequiresUserProjectionServiceWithoutExportedEntry() throws Exception {
        Context context=RuntimeEnvironment.getApplication();
        ServiceInfo info=context.getPackageManager().getServiceInfo(new ComponentName(context,ScreenShareService.class),0);
        assertFalse(info.exported);
        assertEquals(ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION,info.getForegroundServiceType());
        String[] permissions=context.getPackageManager().getPackageInfo(context.getPackageName(),PackageManager.GET_PERMISSIONS).requestedPermissions;
        assertTrue(java.util.Arrays.asList(permissions).contains("android.permission.FOREGROUND_SERVICE_MEDIA_PROJECTION"));
    }
    @Test public void outputPreservesAspectAndBoundsEachDimension() {
        assertArrayEquals(new int[]{512,288},ScreenShareService.outputSize(1920,1080));
        assertArrayEquals(new int[]{288,512},ScreenShareService.outputSize(1080,1920));
        assertArrayEquals(new int[]{512,512},ScreenShareService.outputSize(2048,2048));
    }
    @Test public void pixelsHonorCropAndRowPaddingWithoutSwappingRedBlue() {
        ByteBuffer bytes=ByteBuffer.allocate(32);
        bytes.put(4,(byte)255);bytes.put(7,(byte)255); // red at x1/y0
        bytes.put(22,(byte)255);bytes.put(23,(byte)255); // blue at x1/y1
        Bitmap bitmap=ScreenShareService.rgba(bytes,16,4,new Rect(1,0,2,2));
        assertEquals(Color.RED,bitmap.getPixel(0,0));assertEquals(Color.BLUE,bitmap.getPixel(0,1));bitmap.recycle();
    }
    @Test public void shortLastRowIsRejectedBeforeReading() {
        try {ScreenShareService.rgba(ByteBuffer.allocate(23),16,4,new Rect(1,0,2,2));fail();}
        catch(IllegalArgumentException expected){}
    }
    @Test public void oversizedImageIsRejected() {
        try {ScreenShareService.rgba(ByteBuffer.allocate(1),4096,4,new Rect(0,0,1024,1));fail();}
        catch(IllegalArgumentException expected){}
    }
    @Test public void encodedFrameCarriesExactPixelsHashOriginAndNoPose() throws Exception {
        Bitmap bitmap=Bitmap.createBitmap(32,16,Bitmap.Config.ARGB_8888);bitmap.eraseColor(Color.RED);
        JSONObject frame=new JSONObject(ScreenShareService.encode(bitmap,new Date(0))).getJSONObject("frame");
        JSONObject metadata=frame.getJSONObject("capture");byte[] jpeg=android.util.Base64.decode(frame.getString("data"),0);
        assertEquals("maestro-camera:mixed-view",frame.getString("sourceId"));assertEquals(32,metadata.getInt("width"));
        assertEquals("1970-01-01T00:00:00.000Z",metadata.getString("capturedAt"));assertFalse(metadata.has("position"));
        StringBuilder hash=new StringBuilder();for(byte b:java.security.MessageDigest.getInstance("SHA-256").digest(jpeg))hash.append(String.format("%02x",b&255));
        assertEquals(hash.toString(),metadata.getString("sha256"));assertTrue(jpeg.length<=98304);bitmap.recycle();
    }
    @Test public void stoppedOrLateLeaseCannotExposePixelsOrConsent() {
        ScreenShareService.Lease lease=new ScreenShareService.Lease(new Intent());lease.publish("frame1");lease.publish("frame2");
        assertEquals("frame2",lease.read());assertEquals("{}",lease.read());lease.stop();
        lease.publish("late");assertFalse(lease.read().contains("late"));assertNull(lease.takeConsent());
    }
    @Test public void serviceWithoutFreshLeaseStopsAndDoesNotRestart() {
        var controller=Robolectric.buildService(ScreenShareService.class).create();ScreenShareService service=controller.get();
        assertEquals(android.app.Service.START_NOT_STICKY,service.onStartCommand(new Intent().putExtra("lease","unknown"),0,1));
        assertTrue(Shadows.shadowOf(service).isStoppedBySelf());controller.destroy();
    }
    @Test public void cancellationBeforeAndroidStartsServiceCannotReviveCapture() {
        var app=RuntimeEnvironment.getApplication();
        ScreenShareService.Lease lease=ScreenShareService.start(app,new Intent("consent"));
        Intent queued=Shadows.shadowOf(app).getNextStartedService();assertNotNull(queued);
        lease.stop();
        var controller=Robolectric.buildService(ScreenShareService.class).create();ScreenShareService service=controller.get();
        service.onStartCommand(queued,0,1);
        assertTrue(Shadows.shadowOf(service).isStoppedBySelf());assertNull(lease.takeConsent());controller.destroy();
    }
    @Test public void staleHostBeforeAndroidStartsServiceDropsTheApproval() {
        var app=RuntimeEnvironment.getApplication();
        ScreenShareService.Lease lease=ScreenShareService.start(app,new Intent("consent"));
        Intent queued=Shadows.shadowOf(app).getNextStartedService();assertNotNull(queued);
        lease.touched=-10000;
        var controller=Robolectric.buildService(ScreenShareService.class).create();ScreenShareService service=controller.get();
        service.onStartCommand(queued,0,1);
        assertTrue(Shadows.shadowOf(service).isStoppedBySelf());assertTrue(lease.closed);assertNull(lease.takeConsent());controller.destroy();
    }
}
