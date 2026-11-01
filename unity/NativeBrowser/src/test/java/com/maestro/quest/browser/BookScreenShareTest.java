// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.app.Activity;
import android.content.Intent;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class BookScreenShareTest {
    long now; boolean active=true,failStart; int prompts,starts,stops;
    BookScreenShare broker;
    @Before public void setup() {
        broker=new BookScreenShare(new BookScreenShare.Host() {
            public boolean active(){return active;}
            public void prompt(){prompts++;}
            public BookScreenShare.Capture capture(Intent intent){
                assertNotNull(intent); starts++; if(failStart)throw new IllegalStateException("Service failure");
                return new BookScreenShare.Capture(){
                    public String read(){return "{}";}
                    public void stop(){stops++;}
                };
            }
        },()->now);
        broker.context("document:room");
    }
    private void approved() { broker.start(); broker.result(Activity.RESULT_OK,new Intent("consent")); }
    @Test public void consentAndForegroundNeverStartCapture() {
        broker.start(); assertEquals(1,prompts); assertEquals(0,starts);
        active=false; broker.stopCapture(); broker.result(Activity.RESULT_OK,new Intent("consent"));
        assertEquals(0,starts); active=true; assertTrue(broker.read().contains("screen-share-consent"));
        broker.start(); assertEquals(1,starts); assertEquals(1,prompts);
    }
    @Test public void stopAndSelectAgainConsumeEachApprovalOnlyOnce() {
        approved(); broker.start(); broker.stopCapture(); broker.start();
        assertEquals(1,starts); assertEquals(1,stops); assertEquals(2,prompts);
    }
    @Test public void lateGrantAfterNavigationIsIgnored() {
        broker.start(); broker.context("different document"); broker.result(Activity.RESULT_OK,new Intent("late"));
        broker.start(); assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void lateGrantAfterRoomChangeIsIgnored() {
        broker.start(); broker.context("document:another room"); broker.result(Activity.RESULT_OK,new Intent("late"));
        broker.start(); assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void selectingAnotherSourceInvalidatesUnusedConsent() {
        approved(); broker.context(""); broker.context("document:room"); broker.start();
        assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void expiredApprovalRequiresNewConsent() {
        approved(); now=60001; broker.start(); assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void expiredDialogCannotGrant() {
        broker.start(); now=120001; broker.result(Activity.RESULT_OK,new Intent("late"));
        assertTrue(broker.read().contains("denied")); broker.start(); assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void denyingDialogNeverStarts() {
        broker.start(); broker.result(Activity.RESULT_CANCELED,new Intent()); broker.start();
        assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void missingGrantNeverStarts() {
        broker.start(); broker.result(Activity.RESULT_OK,null); broker.start();
        assertEquals(2,prompts); assertEquals(0,starts);
    }
    @Test public void backgroundCannotConsumeApproval() {
        approved(); active=false; broker.start(); assertEquals(0,starts);
        active=true; broker.start(); assertEquals(1,starts);
    }
    @Test public void failingServiceCannotReuseToken() {
        approved(); failStart=true; broker.start(); broker.start();
        assertEquals(1,starts); assertEquals(2,prompts);
    }
    @Test public void repeatedSelectionCannotStackDialogs() {
        broker.start(); broker.start(); assertEquals(1,prompts);
    }
    @Test public void destructionDropsLateResultAndStopsOwner() {
        approved(); broker.start(); broker.close(); broker.result(Activity.RESULT_OK,new Intent());
        broker.context("other"); broker.start(); assertEquals(1,starts); assertEquals(1,stops);
    }
}
