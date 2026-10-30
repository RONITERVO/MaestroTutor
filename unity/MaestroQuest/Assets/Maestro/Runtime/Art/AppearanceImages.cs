// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using UnityEngine;
namespace Maestro.Quest.Art {
    /// <summary>Workspace-owned, main-thread texture leases. Read/hash/inspection runs
    /// off-thread; the shared image scheduler decodes at most once per frame. Late file reads
    /// never upload into a replaced workspace. Zero-owner entries release immediately.</summary>
    internal sealed class AppearanceImages:MonoBehaviour {
        internal const long MaximumResidentBytes=64L*1024*1024;
        // A refresh briefly holds old and replacement bindings together. These
        // limits bound observations without confusing owners with texture copies.
        internal const int MaximumEntries=128,MaximumOwnersPerImage=132;
        internal sealed class Entry {internal readonly string Id=Guid.NewGuid().ToString("N");internal string Hash,State="loading",Error="";internal readonly List<Lease> Owners=new();internal Texture2D Texture;internal long Bytes;internal CancellationTokenSource Cancel=new();internal Task Work;internal RoomResourceOwner Scope;internal ImageResources.PayloadLease Payload;internal ImageResources.TextureLease Native;internal IDisposable Pin;internal bool Retired;}
        internal sealed class Lease:IDisposable {
            AppearanceImages owner;Entry entry;
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal readonly RoomResourceOwner Scope;
            internal Texture Texture=>owner&&!owner.closed&&entry!=null?(entry.Texture?entry.Texture:owner.Placeholder):null;
            internal string State=>owner&&!owner.closed&&entry!=null?entry.State:"released";
            internal Lease(AppearanceImages owner,Entry entry,RoomResourceOwner scope){this.owner=owner;this.entry=entry;Scope=scope;entry.Owners.Add(this);}
            public void Dispose(){if(entry==null)return;var old=entry;entry=null;if(owner&&!owner.closed)owner.Release(old,this);owner=null;}
        }
        readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
        ImageLibrary library;Texture2D placeholder;bool closed;long resident;
        internal event Action<string> Changed;
        internal int Count=>entries.Count;
        internal long ResidentBytes=>resident;
        internal void Initialize(ImageLibrary value){library=value;closed=false;}
        Texture2D Placeholder {get {if(!placeholder){placeholder=new Texture2D(2,2,TextureFormat.RGBA32,false,false){name="Image pending or unavailable",filterMode=FilterMode.Point};placeholder.SetPixels32(new[]{new Color32(170,170,170,255),new Color32(90,90,90,255),new Color32(90,90,90,255),new Color32(170,170,170,255)});placeholder.Apply(false,true);}return placeholder;}}
        internal Lease Acquire(string hash,RoomResourceOwner scope=null){
            if(closed||library==null||!ModelLibrary.ValidHash(hash))throw new InvalidOperationException("The image library is unavailable.");
            scope??=new RoomResourceOwner(null,null,"unscoped");
            if(scope.Role is not ("appearance" or "unscoped"))throw new ArgumentException("Invalid appearance image owner");
            if(!entries.TryGetValue(hash,out var entry)){
                if(entries.Count>=MaximumEntries)throw new InvalidOperationException("The appearance image entry budget is full.");
                var pin=library.Assets.Pin(ImportedAssetKind.Image,hash);
                try{entry=new Entry{Hash=hash,Scope=scope,Pin=pin};entries.Add(hash,entry);}catch{pin.Dispose();throw;}
            }else{
                if(entry.Owners.Count>=MaximumOwnersPerImage)throw new InvalidOperationException("The appearance image owner budget is full.");
                // Cached reuse still passes admission, including workspace retirement.
                using var admission=library.Assets.Pin(ImportedAssetKind.Image,hash);
                return new Lease(this,entry,scope);
            }
            return new Lease(this,entry,scope);
        }
        internal JObject ObserveBudget()=>new(){["entries"]=entries.Count,["owners"]=entries.Values.Sum(e=>e.Owners.Count),["textureBytes"]=resident,
            ["entryLimit"]=MaximumEntries,["ownerLimitPerImage"]=MaximumOwnersPerImage,["textureByteLimit"]=MaximumResidentBytes};
        internal JObject ObserveReservation(int index){
            if(closed||index<0||index>=entries.Count)return null;
            var e=entries.Values.OrderBy(e=>e.Hash,StringComparer.Ordinal).ElementAt(index);
            return new JObject{["reservationId"]=e.Id,["imageHash"]=e.Hash,["state"]=e.State,["owners"]=e.Owners.Count,["textureBytes"]=e.Bytes};
        }
        internal JObject ObserveOwner(string reservationId,int index){
            if(closed||index<0)return null;var e=entries.Values.FirstOrDefault(e=>e.Id==reservationId);
            if(e==null||index>=e.Owners.Count)return null;var lease=e.Owners[index];var scope=lease.Scope;
            return new JObject{["reservationId"]=e.Id,["leaseId"]=lease.Id,["imageHash"]=e.Hash,["worldId"]=scope.World,["regionId"]=scope.Region,["target"]=scope.Target,["role"]=scope.Role};
        }
        void StartRead(Entry e){
            var payload=ImageResources.BeginPayload(e.Scope,SurfaceImage.MaximumBytes,e.Hash);e.Payload=payload;
            var work=Load(e,payload);e.Work=work;payload.Hold(work);_=Observe(e,work);
        }
        static async Task Observe(Entry entry,Task task){try{await task;}catch(Exception error){Debug.LogException(error);}finally{
            if(ReferenceEquals(entry.Work,task))entry.Work=null;
            if(entry.Retired)ReleasePin(entry);
        }}
        static void ReleasePin(Entry entry){entry.Cancel.Dispose();entry.Pin?.Dispose();entry.Pin=null;}
        async Task Load(Entry e,ImageResources.PayloadLease payload){
            ImageAsset asset=null;ImageResources.TextureLease candidate=null;var token=e.Cancel.Token;
            try{
                asset=await library.ReadAsync(e.Hash,token);token.ThrowIfCancellationRequested();
                long bytes=asset.Inspection.TextureBytes;
                if(bytes>MaximumResidentBytes-resident)throw new InvalidOperationException("Image memory is full. Remove unused image bindings, then refresh the image library.");
                candidate=await ImageResources.DecodeAsync(asset,payload,token);token.ThrowIfCancellationRequested();
                if(closed||e.Retired)throw new OperationCanceledException();
                e.Native=candidate;e.Texture=candidate.Texture;candidate=null;e.Bytes=bytes;resident+=bytes;e.State="ready";
            }catch(Exception ex){if(!closed&&!e.Retired)Failure(e,ex);}
            finally{candidate?.Dispose();asset=null;payload.Dispose();e.Payload=null;}
            if(!closed&&!e.Retired)Changed?.Invoke(e.Hash);
        }
        static void Failure(Entry e,Exception ex){e.State="failed";e.Error=ex is System.IO.InvalidDataException||ex is System.IO.FileNotFoundException||ex is InvalidOperationException?ex.Message:"The image could not be loaded. Import its original file again.";}
        internal void Retry(string hash){if(closed||!entries.TryGetValue(hash,out var e)||e.State!="failed")return;e.State="loading";e.Error="";Changed?.Invoke(hash);}
        internal string State(string hash)=>entries.TryGetValue(hash,out var e)?e.State:"unloaded";
        internal string Error(string hash)=>entries.TryGetValue(hash,out var e)?e.Error:"";
        void Update(){
            if(closed)return;
            Entry next=null;foreach(var candidate in entries.Values)if(candidate.State=="loading"){next=candidate;break;}
            if(next==null||next.Work!=null||!library.Readable)return;
            try{StartRead(next);}catch(Exception ex){next.Payload?.Dispose();next.Payload=null;Failure(next,ex);Changed?.Invoke(next.Hash);}
        }
        void Release(Entry e,Lease lease){if(!e.Owners.Remove(lease)||e.Owners.Count!=0)return;Release(e);}
        void Release(Entry e){
            if(e.Retired)return;e.Retired=true;entries.Remove(e.Hash);e.Cancel.Cancel();e.Payload?.Dispose();
            resident-=e.Bytes;e.Bytes=0;e.Native?.Dispose();e.Native=null;e.Texture=null;
            // Requested cancellation does not end the source use. The awaited Load
            // includes both disk read and queued/native decode; Observe releases
            // the pin only after that task has actually completed.
            if(e.Work==null||e.Work.IsCompleted)ReleasePin(e);
        }
        void OnDestroy(){closed=true;foreach(var e in entries.Values.ToArray()){e.Owners.Clear();Release(e);}entries.Clear();resident=0;if(placeholder)ArtResources.Release(placeholder);Changed=null;}
    }
}
