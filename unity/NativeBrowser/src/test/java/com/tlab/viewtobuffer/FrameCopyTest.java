// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.tlab.viewtobuffer;

import android.graphics.SurfaceTexture;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;

@RunWith(RobolectricTestRunner.class)
@Config(manifest=Config.NONE, sdk=35)
public final class FrameCopyTest {
    // Exercise the real scheduling/lifecycle path; only GPU calls are replaced.
    private static final class Renderer extends ViewToBufferRenderer {
        int updates, copies, allocations;
        Renderer(boolean newFrames) { mInitialized=true; setCopyOnNewFrame(newFrames); }
        @Override public void createSurfaceAndSurfaceTexture(int width,int height) {
            mSurfaceTexture=new SurfaceTexture(0); mFrameAvailable=false;
        }
        @Override protected void initBuffer() { allocations++; }
        @Override protected void destroyBuffer() { }
        @Override protected void updateSurfaceTexture() { updates++; }
        @Override public void CopySurfaceTextureToBuffer() { copies++; }
        void surface() { onSurfaceChanged(null,1024,768); }
        void frame() { onFrameAvailable(mSurfaceTexture); }
        void draw() { onDrawFrame(null); }
    }

    @Test public void idleDrawsRetainTheLastImageWithoutAnotherCopy() {
        Renderer r=new Renderer(true);r.surface();r.draw();
        assertEquals(0,r.copies);assertFalse(r.contentExists());
        r.frame();r.draw();assertEquals(1,r.copies);assertEquals(1,r.updates);
        for(int i=0;i<100;i++)r.draw();
        assertEquals(1,r.copies);assertEquals(1,r.updates);assertTrue(r.contentExists());
        assertArrayEquals(new long[]{102,1,1,1,1},r.frameCopyStatistics());
    }
    @Test public void producerBurstsCoalesceAndNextFrameStillCopies() {
        Renderer r=new Renderer(true);r.surface();
        for(int i=0;i<5;i++)r.frame();r.draw();
        assertEquals(1,r.copies);assertEquals(1,r.updates);
        r.frame();r.draw();assertEquals(2,r.copies);assertTrue(r.contentExists());
        assertArrayEquals(new long[]{2,6,2,1,1},r.frameCopyStatistics());
    }
    @Test public void textureResizeRepopulatesFromTheLatchedFrameOnce() {
        Renderer r=new Renderer(true);r.surface();r.frame();r.draw();
        r.requestResizeTex();r.draw();r.draw();
        assertEquals(2,r.allocations);assertEquals(2,r.copies);assertEquals(1,r.updates);assertTrue(r.contentExists());
    }
    @Test public void resizeBeforeFirstFrameCannotPublishEmptyContent() {
        Renderer r=new Renderer(true);r.surface();r.requestResizeTex();r.draw();
        assertEquals(2,r.allocations);assertEquals(0,r.copies);assertFalse(r.contentExists());
        r.frame();r.draw();assertEquals(1,r.copies);assertTrue(r.contentExists());
    }
    @Test public void pauseAndRecreatedSurfaceRejectQueuedOldCallbacks() {
        Renderer r=new Renderer(true);r.surface();SurfaceTexture old=r.mSurfaceTexture;
        r.frame();r.draw();r.disable();r.onFrameAvailable(old);r.draw();
        assertEquals(1,r.copies);assertFalse(r.contentExists());
        r.surface();r.onFrameAvailable(old);r.draw();
        assertEquals(1,r.copies);assertEquals(1,r.frameCopyStatistics()[1]);assertFalse(r.contentExists());
        r.frame();r.draw();assertEquals(2,r.copies);assertTrue(r.contentExists());
    }
    @Test public void continuousComparisonAndPboDrainKeepCopying() {
        Renderer r=new Renderer(false);r.surface();r.frame();r.draw();r.draw();r.draw();
        assertEquals(3,r.copies);assertEquals(3,r.updates);
        r.setCopyOnNewFrame(true);r.draw();assertEquals(3,r.copies);assertTrue(r.contentExists());
        r.frame();r.draw();assertEquals(4,r.copies);
        r.setCopyOnNewFrame(false);r.draw();assertEquals(5,r.copies);
    }
}
