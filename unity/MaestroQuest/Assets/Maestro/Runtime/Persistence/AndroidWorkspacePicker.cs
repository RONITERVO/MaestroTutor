// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    internal sealed class AndroidWorkspacePicker:IWorkspaceArchivePicker
    {
        readonly int ownerThread=Thread.CurrentThread.ManagedThreadId;
        public string CacheRoot {get;}
        public bool ReadyToStart {get{using var picker=new AndroidJavaClass("com.maestro.quest.browser.WorkspacePicker");return picker.CallStatic<bool>("ReadyToStart");}}
        public AndroidWorkspacePicker()
        {
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker=new AndroidJavaClass("com.maestro.quest.browser.WorkspacePicker");CacheRoot=picker.CallStatic<string>("CacheRoot",activity);
        }
        public void Start(string id)
        {
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker=new AndroidJavaClass("com.maestro.quest.browser.WorkspacePicker");
            if(picker.CallStatic<string>("Start",activity,id)!=id)throw new InvalidOperationException("Archive selection identity changed.");
        }
        public JObject Read(string id)
        {
            using var picker=new AndroidJavaClass("com.maestro.quest.browser.WorkspacePicker");string json=picker.CallStatic<string>("Read",id);
            if(string.IsNullOrEmpty(json))return null;if(json.Length>4096)throw new InvalidOperationException("Invalid archive selection result.");return JObject.Parse(json);
        }
        public void Release(string id)
        {
            bool worker=Thread.CurrentThread.ManagedThreadId!=ownerThread;
            if(worker&&AndroidJNI.AttachCurrentThread()!=0)throw new InvalidOperationException("Archive cleanup could not attach to Android.");
            try{using var picker=new AndroidJavaClass("com.maestro.quest.browser.WorkspacePicker");picker.CallStatic("Release",id);}
            finally{if(worker)AndroidJNI.DetachCurrentThread();}
        }
    }
}
#endif
