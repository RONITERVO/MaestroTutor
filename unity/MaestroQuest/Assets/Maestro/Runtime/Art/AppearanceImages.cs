// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Art {
    /// <summary>Workspace-owned, main-thread texture leases. Read/hash/inspection runs
    /// off-thread; at most one bounded native decode runs per frame. Late file reads
    /// never upload into a replaced workspace. Zero-owner entries release immediately.</summary>
    internal sealed class AppearanceImages:MonoBehaviour {
        internal const long MaximumResidentBytes=64L*1024*1024;
        internal sealed class Entry {internal string Hash,State="loading",Error="";internal int Owners;internal Texture2D Texture;internal long Bytes;internal CancellationTokenSource Cancel=new();internal Task<ImageAsset> Read;}
        internal sealed class Lease:IDisposable {
            AppearanceImages owner;Entry entry;
            internal Texture Texture=>entry?.Texture?entry.Texture:owner?owner.Placeholder:null;
            internal string State=>entry?.State??"released";
            internal Lease(AppearanceImages owner,Entry entry){this.owner=owner;this.entry=entry;entry.Owners++;}
            public void Dispose(){if(entry==null)return;var old=entry;entry=null;if(owner)owner.Release(old);owner=null;}
        }
        readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
        ImageLibrary library;Texture2D placeholder;bool closed;long resident;
        internal event Action<string> Changed;
        internal int Count=>entries.Count;
        internal long ResidentBytes=>resident;
        internal void Initialize(ImageLibrary value){library=value;closed=false;}
        Texture2D Placeholder {get {if(!placeholder){placeholder=new Texture2D(2,2,TextureFormat.RGBA32,false,false){name="Image pending or unavailable",filterMode=FilterMode.Point};placeholder.SetPixels32(new[]{new Color32(170,170,170,255),new Color32(90,90,90,255),new Color32(90,90,90,255),new Color32(170,170,170,255)});placeholder.Apply(false,true);}return placeholder;}}
        internal Lease Acquire(string hash){
            if(closed||library==null||!ModelLibrary.ValidHash(hash))throw new InvalidOperationException("The image library is unavailable.");
            if(!entries.TryGetValue(hash,out var entry)){entry=new Entry{Hash=hash};entries.Add(hash,entry);}return new Lease(this,entry);
        }
        void StartRead(Entry e){e.Read=library.ReadAsync(e.Hash,e.Cancel.Token);_=Observe(e.Read);}
        static async Task Observe(Task task){try{await task;}catch(Exception){}}
        internal void Retry(string hash){if(closed||!entries.TryGetValue(hash,out var e)||e.State!="failed")return;e.State="loading";e.Error="";e.Read=null;Changed?.Invoke(hash);}
        internal string State(string hash)=>entries.TryGetValue(hash,out var e)?e.State:"unloaded";
        internal string Error(string hash)=>entries.TryGetValue(hash,out var e)?e.Error:"";
        void Update(){
            if(closed)return;
            Entry next=null;
            foreach(var candidate in entries.Values)if(candidate.State=="loading"){next=candidate;break;}
            if(next==null)return;
            var e=next;
            if(e.Read==null){if(library.Readable)StartRead(e);return;}
            if(!e.Read.IsCompleted)return;
            try{var asset=e.Read.GetAwaiter().GetResult();long bytes=asset.Inspection.TextureBytes;
                if(bytes>MaximumResidentBytes-resident)throw new InvalidOperationException("Image memory is full. Remove unused image bindings, then refresh the image library.");
                e.Texture=Decode(asset);e.Bytes=bytes;resident+=bytes;e.State="ready";e.Read=null;
            }catch(Exception ex){e.State="failed";e.Error=ex is System.IO.InvalidDataException||ex is System.IO.FileNotFoundException||ex is InvalidOperationException?ex.Message:"The image could not be loaded. Import its original file again.";e.Read=null;}
            Changed?.Invoke(e.Hash);
        }
        internal static Texture2D Decode(ImageAsset asset){
            var info=asset.Inspection;Texture2D decoded=null,result=null;
            try{
                decoded=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Imported appearance image",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat,anisoLevel=1};
                if(!ImageConversion.LoadImage(decoded,SurfaceImage.DecodeBytes(asset.Content,info),info.Orientation==1)||decoded.width!=info.Width||decoded.height!=info.Height)throw new System.IO.InvalidDataException("The image decoder could not verify these pixels.");
                if(info.Orientation==1){result=decoded;decoded=null;return result;}
                var source=decoded.GetPixels32();var pixels=new Color32[source.Length];int w=info.DisplayWidth,h=info.DisplayHeight;
                for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++){var d=SurfaceImage.Upright(x,y,info.Width,info.Height,info.Orientation);pixels[(h-1-d.Y)*w+d.X]=source[(info.Height-1-y)*info.Width+x];}
                result=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name="Imported appearance image",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat,anisoLevel=1};result.SetPixels32(pixels);result.Apply(true,true);return result;
            }catch{if(result)ArtResources.Release(result);throw;}finally{if(decoded)ArtResources.Release(decoded);}
        }
        void Release(Entry e){if(--e.Owners!=0)return;entries.Remove(e.Hash);e.Cancel.Cancel();e.Cancel.Dispose();resident-=e.Bytes;if(e.Texture)ArtResources.Release(e.Texture);}
        void OnDestroy(){closed=true;foreach(var e in entries.Values){e.Cancel.Cancel();e.Cancel.Dispose();if(e.Texture)ArtResources.Release(e.Texture);}entries.Clear();resident=0;if(placeholder)ArtResources.Release(placeholder);Changed=null;}
    }
}
