// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    public sealed class AndroidMotionBatchSource : IMotionBatchSource
    {
        [Serializable] public sealed class Selection
        {
            public string kind,path,name,error,session;
            public int count,index,request;
        }
        const string Picker="com.maestro.quest.browser.MotionBatchPicker";
        bool disposed;
        int request;
        readonly string session;
        readonly string[] names;
        public int Count { get; }
        public AndroidMotionBatchSource(int count,string session) { Count=count; this.session=session; names=new string[count]; }
        public string Name(int index) => names[index];
        public static bool ReadyToStart {get{using var picker=new AndroidJavaClass(Picker);return picker.CallStatic<bool>("ReadyToStart");}}
        public static string Open(string id)
        {
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker=new AndroidJavaClass(Picker); return picker.CallStatic<string>("Start",activity,id);
        }
        public static Selection Poll(string session)
        {
            using var picker=new AndroidJavaClass(Picker); string json=picker.CallStatic<string>("ReadResult",session);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Selection>(json);
        }
        public static void ClosePicker(string session) { using var picker=new AndroidJavaClass(Picker); picker.CallStatic("ReleaseSession",session); }
        public async Task<MotionBatchInput> ReadAsync(int index,CancellationToken cancellation)
        {
            if (disposed) throw new ObjectDisposedException(nameof(AndroidMotionBatchSource));
            cancellation.ThrowIfCancellationRequested();
            using var picker=new AndroidJavaClass(Picker); int attempt=++request;
            picker.CallStatic("Copy",session,index,attempt);
            try
            {
                // Java also times out and cancels provider reads. The local bound
                // handles an interrupted/recreated activity or lost JNI result.
                var timeout=System.Diagnostics.Stopwatch.StartNew();
                Selection selected;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (disposed) throw new OperationCanceledException(cancellation);
                    selected=Poll(session);
                    if (selected?.kind == "error") throw new ModelImportException(selected.error);
                    if (selected != null && selected.session != session) throw new ModelImportException("This selection has ended. Choose files again.");
                    if (selected?.kind == "file" && selected.request == attempt && selected.index == index) break;
                    if (timeout.Elapsed.TotalSeconds > 125) throw new ModelImportException("The file picker stopped responding. Stop, then choose local files again.");
                    await Task.Delay(40,cancellation);
                }
                names[index]=selected.name;
                if (!string.IsNullOrEmpty(selected.error)) throw new ModelImportException(selected.error);
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                string cache=picker.CallStatic<string>("CacheRoot",activity),path=ImportWorkshop.SelectedPath(selected.path,cache);
                var bytes=await Task.Run(() => ModelLibrary.ReadBounded(path),cancellation);
                cancellation.ThrowIfCancellationRequested();
                return new MotionBatchInput(selected.name,bytes);
            }
            finally
            {
                // Posting cancel/release in order handles both a completed local
                // read and a provider that is still streaming when Stop is used.
                picker.CallStatic("CancelCopy",session,attempt); picker.CallStatic("ReleaseFile",session,attempt);
            }
        }
        public void Dispose() { if (disposed) return; disposed=true; using var picker=new AndroidJavaClass(Picker); picker.CallStatic("ReleaseSession",session); }
    }
}
