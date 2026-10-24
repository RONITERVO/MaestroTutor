// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.app.Activity;
import android.content.Intent;
/** Trusted native facade over the one selected-file owner; no page JavaScript bridge. */
public final class WorkspacePicker {
    static final int REQUEST=DocumentPicker.REQUEST;
    static final long MAX_BYTES=512L*1024*1024;
    private WorkspacePicker(){}
    public static String CacheRoot(Activity activity)throws java.io.IOException{return DocumentPicker.CacheRoot(activity);}
    public static boolean ReadyToStart(){return DocumentPicker.ReadyToStart();}
    public static String Start(Activity activity,String requestId){return DocumentPicker.Start(activity,requestId,"archive");}
    public static String Read(String requestId){return DocumentPicker.Read(requestId,"archive");}
    public static void Release(String requestId){DocumentPicker.Release(requestId,"archive");}
    static Intent selectionIntent(){return DocumentPicker.selectionIntent();}
}
