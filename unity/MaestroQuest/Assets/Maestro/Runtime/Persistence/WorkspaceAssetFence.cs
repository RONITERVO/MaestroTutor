// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using Maestro.Quest.Imports;
namespace Maestro.Quest.Persistence
{
    internal enum ImportedAssetKind { Image, Audio, Model }

    /// <summary>Per-workspace exact-source admission. A retirement is a scoped
    /// transaction, not a tombstone or permission to delete retained work.</summary>
    internal sealed class WorkspaceAssetFence
    {
        sealed class Entry {internal int Pins;internal bool Retiring;}
        sealed class Lease:IDisposable
        {
            WorkspaceAssetFence owner;readonly (ImportedAssetKind kind,string hash) key;
            readonly Entry entry;readonly bool retirement;readonly IDisposable retained;
            internal Lease(WorkspaceAssetFence owner,(ImportedAssetKind,string) key,Entry entry,bool retirement,IDisposable retained=null)
            {this.owner=owner;this.key=key;this.entry=entry;this.retirement=retirement;this.retained=retained;}
            public void Dispose(){
                var previous=Interlocked.Exchange(ref owner,null);if(previous==null)return;
                try{lock(previous.sync){
                    if(retirement)entry.Retiring=false;else entry.Pins--;
                    if(entry.Pins==0&&!entry.Retiring)previous.entries.Remove(key);
                }}finally{retained?.Dispose();}
            }
        }
        readonly WorkspaceWriteGate workspace;readonly object sync=new();
        readonly Dictionary<(ImportedAssetKind kind,string hash),Entry> entries=new();
        internal WorkspaceAssetFence(WorkspaceWriteGate workspace){this.workspace=workspace??throw new ArgumentNullException(nameof(workspace));}
        static void Validate(ImportedAssetKind kind,string hash){
            if(kind is not (ImportedAssetKind.Image or ImportedAssetKind.Audio or ImportedAssetKind.Model)||!ModelLibrary.ValidHash(hash))
                throw new ArgumentException("Choose an exact imported asset kind and content hash.");
        }
        internal int Pinned(ImportedAssetKind kind,string hash){
            Validate(kind,hash);lock(sync)return entries.TryGetValue((kind,hash),out var entry)?entry.Pins:0;
        }
        internal IDisposable Pin(ImportedAssetKind kind,string hash)=>TryPin(kind,hash,out var error)??throw new InvalidOperationException(error);
        internal IDisposable TryPin(ImportedAssetKind kind,string hash,out string error){
            Validate(kind,hash);IDisposable retained;
            // Retained reads coexist with ordinary preservation, but keep a closing
            // workspace alive and reject new admission after its retirement.
            try{retained=workspace.RetainRead();}catch(InvalidOperationException failure){error=failure.Message;return null;}
            lock(sync){
                var key=(kind,hash);entries.TryGetValue(key,out var entry);
                if(entry?.Retiring!=true){
                    if(entry==null){entry=new Entry();entries.Add(key,entry);}entry.Pins++;error=null;return new Lease(this,key,entry,false,retained);
                }
                error="The asset is being removed. Wait for library maintenance to finish.";
            }
            retained.Dispose();return null;
        }
        internal IDisposable TryRetire(ImportedAssetKind kind,string hash,out string error){
            Validate(kind,hash);lock(sync){
                var key=(kind,hash);entries.TryGetValue(key,out var entry);
                error=entry?.Retiring==true?"The asset already has a library maintenance operation.":
                    entry?.Pins>0?"The asset is in use. Release its live users and wait for pending work to finish.":null;
                if(error!=null)return null;
                if(entry==null){entry=new Entry();entries.Add(key,entry);}entry.Retiring=true;return new Lease(this,key,entry,true);
            }
        }
    }
}
