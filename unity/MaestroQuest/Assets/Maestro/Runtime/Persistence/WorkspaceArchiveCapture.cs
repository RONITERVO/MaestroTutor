// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using UnityEngine;

namespace Maestro.Quest.Persistence
{
    public sealed class CapturedWorkspaceArchive
    {
        // Private completed snapshot, not proof that a user-visible download was published.
        public string Path {get;internal set;}
        public WorkspaceArchiveReceipt Receipt {get;internal set;}
    }
    public static class WorkspaceArchiveCapture
    {
        /// <summary>Call on the Unity owner thread. One non-yielding copy captures the accepted documents;
        /// scoped library gates keep referenced bytes stable until the worker closes the archive.
        /// In-progress takes, transient physics/animation state, Undo, scans, chat and receipts are excluded.</summary>
        public static bool CanStart(RoomEditor editor,RuleWorkshop rules,MovementControls controls,out string error)
        {
            error=null;
            if(!editor||!rules||!controls||rules.Editor!=editor||controls.ArchiveEditor!=editor)error="Workspace controls are not ready.";
            else if(editor.TemporaryRoom||editor.TemporarySavePending)error="Keep or discard the temporary room before exporting the saved workspace.";
            else if(!editor.CanSaveRoom||rules.ReadOnly||editor.ActivityProfiles.ReadOnly||!controls.ArchiveReady)error="Resolve unavailable native storage before exporting a portable workspace.";
            else if(rules.Memory==null||!rules.Memory.Ready||rules.Memory.Pending||rules.Memory.Error!=null)error="Wait for remembered values to load/save, or recover unavailable memory before exporting.";
            else {
                rules.Modules.Poll();
                if(!rules.Modules.Ready||rules.Modules.Pending||rules.Modules.Error!=null)error="Wait for the reusable library to finish loading or writing.";
                else if(rules.Modules.Search("").Any(x=>x.Error!=null))error="A reusable module is damaged; its original remains available for recovery.";
            }
            return error==null;
        }
        public static Task<CapturedWorkspaceArchive> Start(RoomEditor editor,RuleWorkshop rules,MovementControls controls,string outputDirectory,CancellationToken cancellation=default)
        {
            string output=System.IO.Path.GetFullPath(outputDirectory);
            return Capture(editor,rules,controls,(snapshot,token)=>{
                string path=null;bool created=false;
                try {
                    token.ThrowIfCancellationRequested();Directory.CreateDirectory(output);WorkspaceArchive.NoLink(output);path=System.IO.Path.Combine(output,"maestro-workspace-"+Guid.NewGuid().ToString("N")+".zip");
                    WorkspaceArchiveReceipt receipt;using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){created=true;receipt=WorkspaceArchive.Write(file,snapshot,token);file.Flush(true);}
                    token.ThrowIfCancellationRequested();return new CapturedWorkspaceArchive {Path=path,Receipt=receipt};
                }catch{if(created&&path!=null&&File.Exists(path))File.Delete(path);throw;}
            },cancellation);
        }
        internal static Task<WorkspaceArchiveReceipt> Fingerprint(RoomEditor editor,RuleWorkshop rules,MovementControls controls,CancellationToken cancellation=default)=>
            Capture(editor,rules,controls,WorkspaceArchive.Fingerprint,cancellation);
        static Task<T> Capture<T>(RoomEditor editor,RuleWorkshop rules,MovementControls controls,Func<WorkspaceArchiveSnapshot,CancellationToken,T> process,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!CanStart(editor,rules,controls,out var error))throw new InvalidOperationException(error);
            // Included defaults ship with the app; pinned imports already embed their source.
            // Preserve only private library files, keeping the portable 256-file bound.
            var modules=rules.Modules.Search("").Where(e=>!e.Included).ToArray();var memory=rules.Memory.Snapshot();
            if(!editor.Models.TryCaptureArchive(out var models))throw new InvalidOperationException("Wait for the model import to finish before exporting.");
            WorkspaceLibraryCapture motions=null,sounds=null,images=null;
            try {
                if(!editor.Motions.TryCaptureArchive(out motions))throw new InvalidOperationException("Wait for motion import or maintenance before exporting.");
                if(!editor.Sounds.TryCaptureArchive(out sounds))throw new InvalidOperationException("Wait for sound import or decoding before exporting.");
                if(!editor.Images.TryCaptureArchive(out images))throw new InvalidOperationException("Wait for image import or loading before exporting.");
                var room=editor.Snapshot();room.version=RoomDocument.CurrentVersion;var behaviours=rules.Snapshot();var preferences=controls.Preferences;var activities=editor.ActivityProfiles.Snapshot();
                string savedDirectory=editor.SaveDirectory;
                var definitions=modules.ToDictionary(x=>"program-modules.v1/"+x.Hash+".json",x=>x.ReadDefinition(),StringComparer.Ordinal);var heldMotions=motions;var heldSounds=sounds;var heldImages=images;
                return Task.Run(()=>{
                    try {
                        using(var pairOwner=RoomSnapshotTransaction.Inspect(savedDirectory)){cancellation.ThrowIfCancellationRequested();}
                        cancellation.ThrowIfCancellationRequested();var documents=new Dictionary<string,byte[]>(StringComparer.Ordinal);var assets=new Dictionary<string,Func<Stream>>(StringComparer.Ordinal);
                        var utf8=new UTF8Encoding(false,true);byte[] Json(object value)=>utf8.GetBytes(JsonUtility.ToJson(value));
                        documents.Add(ProgramMemoryStore.FileName,memory.Encode());
                        documents.Add(RoomStorage.FileName,Json(room));documents.Add("behaviours.v2.json",Json(behaviours));documents.Add("controls.v2.json",Json(preferences));documents.Add("avatar-activities.v2.json",Json(activities));
                        foreach(var pair in definitions)documents.Add(pair.Key,utf8.GetBytes(pair.Value.ToString(Formatting.None)));
                        models.Collect(documents,assets);heldMotions.Collect(documents,assets);heldSounds.Collect(documents,assets);heldImages.Collect(documents,assets);return process(new WorkspaceArchiveSnapshot(documents,assets),cancellation);
                    }finally{heldImages.Dispose();heldSounds.Dispose();heldMotions.Dispose();models.Dispose();}
                });
            }catch{images?.Dispose();sounds?.Dispose();motions?.Dispose();models.Dispose();throw;}
        }
    }
}
