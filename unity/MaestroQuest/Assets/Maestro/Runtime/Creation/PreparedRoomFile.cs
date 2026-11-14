// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    internal sealed partial class VersionedRoomFile<T> where T : class
    {
        // Capture under the room/memory coordinator, then release it before doing
        // expensive pure validation/encoding on a worker. No accepted state is read
        // from this token by callers.
        internal sealed class PrimaryCapture
        {
            readonly VersionedRoomFile<T> owner;
            readonly byte[] bytes;
            readonly bool exists;
            internal PrimaryCapture(VersionedRoomFile<T> owner,byte[] bytes,bool exists)
            {this.owner=owner;this.bytes=bytes;this.exists=exists;}
            internal bool Read(VersionedRoomFile<T> expected,out byte[] contents,out bool present)
            {
                contents=null;present=false;if(!ReferenceEquals(owner,expected))return false;
                contents=bytes==null?null:(byte[])bytes.Clone();present=exists;return true;
            }
        }

        // A preparation owns private bytes and optionally a worker-written stage.
        // Dispose and every publication attempt consume it; no failed/ambiguous
        // disk effect is silently retried.
        internal sealed class PreparedContent:IDisposable
        {
            internal readonly byte[] Bytes,Hash;
            internal readonly Maestro.Quest.Persistence.PreparedFileStage Stage;
            internal PreparedContent(byte[] bytes,Maestro.Quest.Persistence.PreparedFileStage stage){Bytes=bytes;Hash=Fingerprint(bytes);Stage=stage;}
            public void Dispose()=>Stage?.Dispose();
        }
        internal sealed class PreparedSave:IDisposable
        {
            readonly VersionedRoomFile<T> owner;
            readonly byte[] baseline;
            readonly bool exists,validPrimary;
            PreparedContent content;
            internal PreparedSave(VersionedRoomFile<T> owner,byte[] bytes,byte[] baseline,bool exists,bool validPrimary,Maestro.Quest.Persistence.PreparedFileStage stage)
            {this.owner=owner;content=new PreparedContent(bytes,stage);this.baseline=baseline;this.exists=exists;this.validPrimary=validPrimary;}
            internal bool Take(VersionedRoomFile<T> expected,out PreparedContent contents,out byte[] fingerprint,out bool present,out bool valid)
            {
                contents=null;fingerprint=null;present=false;valid=false;
                if(!ReferenceEquals(owner,expected))return false;
                contents=Interlocked.Exchange(ref content,null);if(contents==null)return false;
                fingerprint=baseline;present=exists;valid=validPrimary;return true;
            }
            // Only the worker that owns this candidate calls Inspect before Take.
            internal bool Inspect(VersionedRoomFile<T> expected,out PreparedContent contents,out byte[] fingerprint,out bool present)
            {contents=content;fingerprint=baseline;present=exists;return ReferenceEquals(owner,expected)&&contents!=null;}
            public void Dispose()=>Interlocked.Exchange(ref content,null)?.Dispose();
        }

        internal PrimaryCapture CaptureForPreparation()
        {
            if(!cachePrimaryInspection)throw new InvalidOperationException("Prepared saving requires content-pure room validation.");
            // The caller owns the room/memory coordinator. Retention scans are
            // readers and need not block this bounded read or the owner thread.
            if(ReadOnly||HasNewerFiles(directory,stem,version))throw new InvalidOperationException("Saved "+label+" is unavailable for editing; its files are preserved.");
            bool exists=File.Exists(primary);var bytes=ReadBytes(primary);
            if(exists!=(bytes!=null)||!exists&&File.Exists(primary))
                throw new IOException("The saved "+label+" could not be captured within its size limit.");
            if(!CheckAcceptedIdentity(bytes==null?null:Fingerprint(bytes),out var error))throw new IOException(error);
            return new PrimaryCapture(this,bytes,exists);
        }

        internal bool PrepareSave(T value,PrimaryCapture captured,out PreparedSave prepared,out string error,bool stageFile=false,CancellationToken cancellation=default)
        {
            prepared=null;error="The saved "+label+" preparation is unavailable.";
            if(!cachePrimaryInspection||captured==null||!captured.Read(this,out var before,out bool existed))return false;
            try {
                bool validPrimary=false,newer=false;
                if(existed) {
                    // Malformed bounded bytes remain recoverable as .unreadable.
                    // A newer document must never become an accepted baseline.
                    try {validPrimary=InspectBytes(before,out newer);}
                    catch(Exception failure) when(failure is ArgumentException||failure is JsonException||failure is OverflowException) {}
                    if(newer){error="Saved "+label+" needs a different app version; its files are preserved.";return false;}
                }
                if(value==null||!validate(value)){error="Invalid "+label+" data";return false;}
                var candidate=copy(value);upgrade(candidate);
                if(!validate(candidate)){error="Invalid "+label+" data";return false;}
                byte[] bytes=new UTF8Encoding(false).GetBytes(Encode(candidate));
                if(bytes.Length>maximum){error="The "+label+" collection is too large to save";return false;}
                // Prove the actual wire bytes on the worker, including normalization
                // and strict field/version checks, before publication can cache them.
                if(!Decode(bytes,version,out _,out _)){error="The prepared "+label+" could not be read back.";return false;}
                cancellation.ThrowIfCancellationRequested();
                var stage=stageFile?Maestro.Quest.Persistence.PreparedFileStage.Create(primary,bytes,cancellation):null;
                prepared=new PreparedSave(this,bytes,existed?Fingerprint(before):null,existed,validPrimary,stage);
                error=null;return true;
            }
            catch(Exception failure) when(failure is ArgumentException||failure is JsonException||failure is OverflowException) {
                error="The "+label+" could not be prepared for saving.";return false;
            }
        }

        bool MatchesPreparation(byte[] fingerprint,bool existed)
        {
            bool exists=File.Exists(primary);if(exists!=existed)return false;
            if(!exists)return !File.Exists(primary);
            var bytes=ReadBytes(primary);return bytes!=null&&Fingerprint(bytes).SequenceEqual(fingerprint);
        }

        // Advisory preflight only. Release all disk locks before awaiting native
        // acceptance; PublishPrepared repeats the baseline check after authorization.
        internal bool CheckPrepared(PreparedSave prepared,out string error)
        {
            error="The "+label+" preparation is unavailable.";
            if(prepared==null||!prepared.Inspect(this,out var content,out var baseline,out bool existed))return false;
            if(!Monitor.TryEnter(retainedGate)){error="Room file inspection is busy; the edit was not accepted.";return false;}
            try {
                if(ReadOnly||HasNewerFiles(directory,stem,version)){error="Saved "+label+" is unavailable; its files are preserved.";return false;}
                if(!CheckAcceptedPrimary(out error))return false;
                if(!MatchesPreparation(baseline,existed)){error="The saved "+label+" changed during preparation; inspect it before retrying.";return false;}
                if(Maestro.Quest.Persistence.WorkspaceFileInventory.Kind(primary+".pending")!="absent"){error="An unfinished room save remains; preserve it before editing.";return false;}
                if(content.Stage!=null&&!content.Stage.Ready){error="Prepared room bytes changed; preserve the staging file.";return false;}
                error=null;return true;
            }finally{Monitor.Exit(retainedGate);}
        }

        // Call under the room/memory coordinator after native authorization.
        // Disk identity cannot prove in-memory edit ownership.
        internal bool PublishPrepared(PreparedSave prepared,out string error,bool wait=false,Action<int> waitForRetry=null)
        {
            error="The "+label+" save preparation was already used or belongs to another store.";
            if(prepared==null||!prepared.Take(this,out var content,out var baseline,out bool existed,out bool validPrimary))return false;
            using(content) {
                // Retention can decode large retained documents on another thread.
                // Refuse a busy publication; never block the interaction thread.
                if(wait)Monitor.Enter(retainedGate);
                else if(!Monitor.TryEnter(retainedGate)){error="Room file inspection is busy; the edit was not published.";return false;}
                var bytes=content.Bytes;
                string pending=primary+".pending",phase="validate";bool staged=false;
                try {
                    if(ReadOnly||HasNewerFiles(directory,stem,version)){ReadOnly=true;error="Saved "+label+" is unavailable for editing; its files are preserved.";return false;}
                    error="The saved "+label+" changed during preparation; inspect it before retrying.";
                    if(!CheckAcceptedPrimary(out error))return false;
                    if(!MatchesPreparation(baseline,existed)){error="The saved "+label+" changed during preparation; inspect it before retrying.";return false;}
                    Directory.CreateDirectory(directory);
                    // Do not claim another writer's unfinished staging file.
                    phase="stage";
                    if(content.Stage!=null){content.Stage.MoveTo(pending);staged=true;}
                    else using(var stream=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                        staged=true;stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                    }
                    if(!CheckAcceptedPrimary(out error))return false;
                    if(!MatchesPreparation(baseline,existed)){error="The saved "+label+" changed during preparation; inspect it before retrying.";return false;}
                    if(existed) {
                        phase="backup";
                        File.Copy(primary,validPrimary?primary+".backup":primary+".unreadable",true);
                        // An accepted worker may recover only the known no-effect
                        // Windows failures, using the same staged bytes. Synchronous
                        // staged callers still never sleep on the interaction thread.
                        phase="replace";
                        if(content.Stage!=null&&!wait)File.Replace(pending,primary,null);
                        else Maestro.Quest.Persistence.FilePublication.Replace(pending,primary,null,
                            Maestro.Quest.Persistence.FilePublication.Windows,File.Replace,waitForRetry??Thread.Sleep);
                    } else File.Move(pending,primary);
                    // The worker proved these exact bytes, including the staged
                    // content check above. Reuse its hash instead of hashing the
                    // same private payload again on the interaction thread.
                    AcceptPrimaryIdentity(content.Hash);
                    primaryInspectionHash=content.Hash;primaryInspectionValid=true;primaryInspectionNewer=false;
                    error=null;return true;
                }
                catch(Exception failure) when(failure is IOException||failure is UnauthorizedAccessException||failure is NotSupportedException) {
                    // Preserve useful diagnostics without including local paths or data.
                    error="Could not publish the prepared "+label+"; inspect the saved files before retrying. "+
                        phase+" failed ("+failure.GetType().Name+", 0x"+failure.HResult.ToString("X8")+").";return false;
                }
                finally {
                    try{if(staged)try{if(File.Exists(pending))File.Delete(pending);}catch(IOException){}catch(UnauthorizedAccessException){}}
                    finally{Monitor.Exit(retainedGate);}
                }
            }
        }
    }
}
