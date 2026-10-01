// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Persistence
{
    /// <summary>One native owner for book, agent and program exports. Only closed, privately captured
    /// files reach the platform publisher. A result is successful only after public publication.</summary>
    public sealed class WorkspaceExport:MonoBehaviour
    {
        RoomEditor editor;RuleWorkshop rules;MovementControls controls;
        string outputDirectory,unavailable;
        Func<string,string> publish;
        Task<JObject> pending;bool retiring;
        CancellationTokenSource cancellation;
        public bool Available=>publish!=null;
        public bool Busy=>pending!=null&&!pending.IsCompleted;
        public void Initialize(RoomEditor editor,RuleWorkshop rules,MovementControls controls)
        {
            this.editor=editor;this.rules=rules;this.controls=controls;
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                using var exports=new AndroidJavaClass("com.maestro.quest.browser.WorkspaceExports");
                outputDirectory=exports.CallStatic<string>("Directory",activity);publish=PublishAndroid;
            }catch(Exception){unavailable="Downloads is unavailable. Resume Maestro before exporting.";}
#else
            unavailable="Native workspace export requires the Quest app.";
#endif
        }
        internal void InitializeForTests(RoomEditor editor,RuleWorkshop rules,MovementControls controls,string directory,Func<string,string> publisher)
        {this.editor=editor;this.rules=rules;this.controls=controls;outputDirectory=directory;publish=publisher;}
        internal void Bind(RoomEditor source,RuleWorkshop behaviours,MovementControls movement){editor=source;rules=behaviours;controls=movement;}
        public bool CanStart(out string error)
        {
            error=unavailable;if(retiring){error="The previous workspace export owner is closing.";return false;}
            if(GetComponent<WorkspaceHost>()?.History?.Busy==true){error="Wait for history preservation to finish.";return false;}
            if(GetComponent<WorkspaceHost>()?.Recovery?.BlocksOtherOperations==true){error="Finish or cancel workspace recovery before exporting.";return false;}
            if(!Available){error??="Workspace export is unavailable";return false;}
            if(Busy){error="Wait for the current workspace export to finish";return false;}
            return WorkspaceArchiveCapture.CanStart(editor,rules,controls,out error);
        }
        internal Task<JObject> StartExport()
        {
            if(!CanStart(out var error))throw new InvalidOperationException(error);
            cancellation?.Dispose();cancellation=new CancellationTokenSource();
            // Capture must start synchronously on the owner thread, before the worker continuation.
            var capture=WorkspaceArchiveCapture.Start(editor,rules,controls,outputDirectory,cancellation.Token);
            var publisher=publish;var token=cancellation.Token;pending=Task.Run(()=>Publish(capture,publisher,token));return pending;
        }
        static async Task<JObject> Publish(Task<CapturedWorkspaceArchive> capture,Func<string,string> publisher,CancellationToken token)
        {
            CapturedWorkspaceArchive archive=null;
            try {
                archive=await capture.ConfigureAwait(false);token.ThrowIfCancellationRequested();
                long bytes=new FileInfo(archive.Path).Length;
                // The platform call streams on this worker and returns only after closing and publishing.
                string location=publisher(archive.Path);
                if(string.IsNullOrEmpty(location)||location.Length>128||!location.StartsWith("Downloads/Maestro/",StringComparison.Ordinal)||!location.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)||location.Substring(18).IndexOfAny(new[]{'/','\\'})>=0||System.Linq.Enumerable.Any(location,char.IsControl))throw new IOException("The archive publication result is unavailable; check Downloads/Maestro before exporting again.");
                var summary=archive.Receipt.Summary;
                return new JObject {["location"]=location,["sizeKiB"]=bytes/1024d,["manifestHash"]=archive.Receipt.ManifestHash,["files"]=summary.Files,["models"]=summary.Models,["motions"]=summary.Motions,["modules"]=summary.Modules,["unavailablePrograms"]=summary.UnavailablePrograms,["missingModels"]=summary.MissingModels.Length,["missingMotions"]=summary.MissingMotions.Length,["missingControllerPrograms"]=summary.MissingControllerPrograms.Length};
            } finally {
                // Deleting a cache file cannot turn a confirmed Downloads publication into failure.
                if(archive!=null)try{File.Delete(archive.Path);}catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        static string PublishAndroid(string path)
        {
            if(AndroidJNI.AttachCurrentThread()!=0)throw new IOException("Could not connect the archive worker to Android.");
            try {
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                using var exports=new AndroidJavaClass("com.maestro.quest.browser.WorkspaceExports");
                return exports.CallStatic<string>("Publish",activity,path);
            }finally{AndroidJNI.DetachCurrentThread();}
        }
#endif
        internal Task Retire(){retiring=true;cancellation?.Cancel();return (Task)pending??Task.CompletedTask;}
        void OnDestroy()=>_=Retire();
    }
}
