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

        // A preparation owns only private bytes. Dispose and every publication
        // attempt consume it; no failed/ambiguous disk effect is silently retried.
        internal sealed class PreparedSave:IDisposable
        {
            readonly VersionedRoomFile<T> owner;
            readonly byte[] baseline;
            readonly bool exists,validPrimary;
            byte[] bytes;
            internal PreparedSave(VersionedRoomFile<T> owner,byte[] bytes,byte[] baseline,bool exists,bool validPrimary)
            {this.owner=owner;this.bytes=bytes;this.baseline=baseline;this.exists=exists;this.validPrimary=validPrimary;}
            internal bool Take(VersionedRoomFile<T> expected,out byte[] contents,out byte[] fingerprint,out bool present,out bool valid)
            {
                contents=null;fingerprint=null;present=false;valid=false;
                if(!ReferenceEquals(owner,expected))return false;
                contents=Interlocked.Exchange(ref bytes,null);if(contents==null)return false;
                fingerprint=baseline;present=exists;valid=validPrimary;return true;
            }
            public void Dispose()=>Interlocked.Exchange(ref bytes,null);
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
            return new PrimaryCapture(this,bytes,exists);
        }

        internal bool PrepareSave(T value,PrimaryCapture captured,out PreparedSave prepared,out string error)
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
                byte[] bytes=new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(candidate));
                if(bytes.Length>maximum){error="The "+label+" collection is too large to save";return false;}
                // Prove the actual wire bytes on the worker, including normalization
                // and strict field/version checks, before publication can cache them.
                if(!Decode(bytes,version,out _,out _)){error="The prepared "+label+" could not be read back.";return false;}
                prepared=new PreparedSave(this,bytes,existed?Fingerprint(before):null,existed,validPrimary);
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

        // Call under the room/memory coordinator, on the journal owner after checking
        // its revision/lifecycle. Disk identity cannot prove in-memory edit ownership.
        internal bool PublishPrepared(PreparedSave prepared,out string error)
        {
            error="The "+label+" save preparation was already used or belongs to another store.";
            if(prepared==null||!prepared.Take(this,out var bytes,out var baseline,out bool existed,out bool validPrimary))return false;
            lock(retainedGate) {
                string pending=primary+".pending";bool staged=false;
                try {
                    if(ReadOnly||HasNewerFiles(directory,stem,version)){ReadOnly=true;error="Saved "+label+" is unavailable for editing; its files are preserved.";return false;}
                    error="The saved "+label+" changed during preparation; inspect it before retrying.";
                    if(!MatchesPreparation(baseline,existed))return false;
                    Directory.CreateDirectory(directory);
                    // Do not claim another writer's unfinished staging file.
                    using(var stream=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                        staged=true;stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                    }
                    if(!MatchesPreparation(baseline,existed))return false;
                    if(existed) {
                        File.Copy(primary,validPrimary?primary+".backup":primary+".unreadable",true);
                        Maestro.Quest.Persistence.FilePublication.Replace(pending,primary,null);
                    } else File.Move(pending,primary);
                    primaryInspectionHash=Fingerprint(bytes);primaryInspectionValid=true;primaryInspectionNewer=false;
                    error=null;return true;
                }
                catch(Exception failure) when(failure is IOException||failure is UnauthorizedAccessException||failure is NotSupportedException) {
                    error="Could not publish the prepared "+label+"; inspect the saved files before retrying.";return false;
                }
                finally {if(staged)try{if(File.Exists(pending))File.Delete(pending);}catch(IOException){}catch(UnauthorizedAccessException){}}
            }
        }
    }
}
