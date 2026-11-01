// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.browser;
import android.app.Activity;
import android.content.Intent;
/** Trusted native facade over the one selected-file owner; no page JavaScript bridge. */
public final class ModelPicker {
    static final int REQUEST=DocumentPicker.REQUEST;
    static final long MAX_BYTES=64L*1024*1024;
    private ModelPicker(){}
    public static String CacheRoot(Activity activity)throws java.io.IOException{return DocumentPicker.CacheRoot(activity);}
    public static boolean ReadyToStart(){return DocumentPicker.ReadyToStart();}
    public static String Start(Activity activity,String requestId){return DocumentPicker.Start(activity,requestId,"model");}
    public static String Read(String requestId){return DocumentPicker.Read(requestId,"model");}
    public static void Release(String requestId){DocumentPicker.Release(requestId,"model");}
    public static boolean SelectMember(String id,int index,int request){return DocumentPicker.SelectModelMember(id,index,request);}
    static Intent selectionIntent(){return DocumentPicker.selectionIntent();}
}
