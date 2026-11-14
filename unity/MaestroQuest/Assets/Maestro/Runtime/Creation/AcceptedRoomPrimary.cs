// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
namespace Maestro.Quest.Creation
{
    internal sealed partial class VersionedRoomFile<T> where T:class
    {
        // Separate from the decode cache: this is the primary accepted by this
        // live store. Never bless an independent writer merely by inspecting it.
        readonly object acceptedGate=new();
        readonly bool trackAcceptedPrimary;
        byte[] acceptedPrimary;
        bool primaryAccepted;
        volatile string primaryConflict;
        internal string PrimaryConflict=>primaryConflict;
        static bool SamePrimary(byte[] a,byte[] b)=>a==null?b==null:b!=null&&a.SequenceEqual(b);
        byte[] ReadPrimaryIdentity()
        {
            if(!File.Exists(primary)) {
                if(Directory.Exists(primary))throw new IOException("The saved room path is not a file.");
                return null;
            }
            using var stream=new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length>maximum)throw new IOException("The saved room exceeds its inspection limit.");
            var bytes=new byte[(int)stream.Length];int at=0;
            while(at<bytes.Length){int count=stream.Read(bytes,at,bytes.Length-at);if(count==0)throw new EndOfStreamException();at+=count;}
            if(stream.ReadByte()!=-1)throw new IOException("The saved room changed while reading.");
            // Empty/corrupt bounded primaries can still be recovered from backup.
            return Fingerprint(bytes);
        }
        bool RejectPrimary(out string error)
        {
            ReadOnly=true;
            error=primaryConflict="The saved "+label+" changed outside this live room; its files are preserved. Reopen or inspect workspace recovery before saving.";
            return false;
        }
        bool CheckAcceptedIdentity(byte[] identity,out string error,bool allowInitial=true)
        {
            error=primaryConflict;if(error!=null)return false;
            if(!trackAcceptedPrimary)return true;
            lock(acceptedGate) {
                if(primaryAccepted) {
                    if(!SamePrimary(acceptedPrimary,identity))return RejectPrimary(out error);
                } else {
                    if(!allowInitial||identity!=null){error="Load the saved room before saving changes.";return false;}
                    acceptedPrimary=identity;primaryAccepted=true;
                }
            }
            return true;
        }
        bool CheckAcceptedPrimary(out string error)
        {
            error=null;if(!trackAcceptedPrimary)return true;
            return CheckAcceptedIdentity(ReadPrimaryIdentity(),out error);
        }
        void AcceptPrimaryIdentity(byte[] identity)
        {
            if(!trackAcceptedPrimary)return;
            lock(acceptedGate){acceptedPrimary=identity;primaryAccepted=true;}
        }
        internal bool CheckPairedPrimary(byte[] before,out string error)
        {
            if(ReadOnly){error=primaryConflict??"Saved room data is unavailable for editing.";return false;}
            return CheckAcceptedIdentity(before==null?null:Fingerprint(before),out error,allowInitial:false);
        }
        internal void AcceptPairedPrimary(byte[] before,byte[] after)
        {
            // The paired publisher supplies its exact private committed bytes;
            // a fresh disk read could accidentally adopt a later external write.
            lock(acceptedGate) {
                if(!CheckPairedPrimary(before,out var error))throw new IOException(error);
                AcceptPrimaryIdentity(after==null?null:Fingerprint(after));
            }
        }
        T LoadAccepted(out string message)
        {
            lock(retainedGate) {
                try {
                    if(primaryConflict!=null){message=primaryConflict;return null;}
                    var before=ReadPrimaryIdentity();
                    var result=LoadExclusive(out message);
                    if(!SamePrimary(before,ReadPrimaryIdentity())){RejectPrimary(out message);return null;}
                    if(!ReadOnly)AcceptPrimaryIdentity(before);
                    return result;
                }catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is NotSupportedException) {
                    ReadOnly=true;message="Saved "+label+" could not be inspected; preserve its files for recovery.";return null;
                }
            }
        }
    }
}
