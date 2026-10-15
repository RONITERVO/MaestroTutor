// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Art
{
    // Main-thread admission for authored appearance images and import verification.
    // These are conservative source/staging estimates, not measured process/GPU RAM.
    internal static class ImageResources
    {
        internal const int MaximumTextureInstances=128,MaximumPayloadReads=4;
        internal const long MaximumTextureBytes=128L*1024*1024,MaximumPayloadBytes=64L*1024*1024,MaximumScratchBytes=128L*1024*1024;
        static readonly List<PayloadLease> payloads=new();
        static readonly List<TextureLease> textures=new();
        static readonly LinkedList<object> decodeQueue=new();
        static int decodedFrame=-1;
        static long Sum(IEnumerable<long> values){long sum=0;foreach(long value in values){if(value>long.MaxValue-sum)return long.MaxValue;sum+=value;}return sum;}
        static void Owner(RoomResourceOwner owner){if(owner==null||owner.Role is not ("appearance" or "preview" or "unscoped"))throw new ArgumentException("Invalid image resource owner");}
        static bool Hash(string hash)=>string.IsNullOrEmpty(hash)||ModelLibrary.ValidHash(hash);
        internal sealed class PayloadLease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal readonly RoomResourceOwner Owner;
            internal readonly long Bytes;
            internal string Hash;
            readonly List<Task> work=new();
            internal bool WorkPending=>work.Any(task=>!task.IsCompleted);
            internal void Drain()=>work.RemoveAll(task=>task.IsCompleted);
            internal bool Retiring;
            internal string Phase=>Retiring?"retiring":WorkPending?"working":"held";
            internal PayloadLease(RoomResourceOwner owner,long bytes,string hash){Owner=owner;Bytes=bytes;Hash=hash??"";}
            internal void Identify(string hash){if(Retiring||!payloads.Contains(this)||!ModelLibrary.ValidHash(hash)||Hash!=""&&Hash!=hash)throw new InvalidOperationException("The image payload owner changed");Hash=hash;}
            internal void Hold(Task task){
                if(task==null)throw new ArgumentNullException(nameof(task));
                // A synchronously finished load may already have disposed its lease.
                if(Retiring){if(task.IsCompleted)return;throw new InvalidOperationException("The image read already ended");}
                if(!payloads.Contains(this))throw new InvalidOperationException("Image payload ownership ended");Drain();if(!task.IsCompleted&&!work.Contains(task))work.Add(task);
            }
            public void Dispose(){if(Retiring)return;Retiring=true;Collect();}
        }
        internal static PayloadLease BeginPayload(RoomResourceOwner owner,long bytes,string hash="")
        {
            Owner(owner);if(bytes<1||bytes>2L*SurfaceImage.MaximumBytes||!Hash(hash))throw new ArgumentException("Invalid image payload reservation");Collect();
            if(payloads.Count>=MaximumPayloadReads||bytes>MaximumPayloadBytes-Sum(payloads.Select(p=>p.Bytes)))throw new RoomResourceLimitException("Image preparation is full. Finish or cancel another image import, or wait for a closing image read, then retry. Saved images are unchanged.");
            var lease=new PayloadLease(owner,bytes,hash);payloads.Add(lease);return lease;
        }
        sealed class TextureCharge
        {
            internal Texture2D Texture;internal long Bytes,ReadableBytes;
        }
        internal sealed class TextureLease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),Hash;
            internal readonly RoomResourceOwner Owner;
            internal readonly long ExpectedBytes,WorkingScratch;
            readonly List<TextureCharge> retained=new();
            int unallocated;bool working=true;
            internal string Phase="preparing";
            internal Texture2D Texture {get;private set;}
            internal int TextureCount=>unallocated+retained.Count(t=>t.Texture);
            internal long TextureBytes=>Sum(retained.Where(t=>t.Texture).Select(t=>t.Bytes).Append(unallocated*ExpectedBytes));
            internal long ScratchBytes=>working?WorkingScratch:Sum(retained.Where(t=>t.Texture).Select(t=>t.ReadableBytes));
            internal TextureLease(RoomResourceOwner owner,string hash,long bytes,int count,long scratch){Owner=owner;Hash=hash;ExpectedBytes=bytes;unallocated=count;WorkingScratch=scratch;}
            internal void Track(Texture2D texture){if(!working||Phase!="preparing"||unallocated<=0||!texture)throw new InvalidOperationException("The image decoder allocation changed");unallocated--;retained.Add(new TextureCharge{Texture=texture,Bytes=ExpectedBytes,ReadableBytes=ExpectedBytes});}
            internal void Inspect(Texture2D texture){
                var charge=retained.Single(t=>t.Texture==texture);long actual=TextureCost(texture.width,texture.height);charge.Bytes=Math.Max(charge.Bytes,actual);charge.ReadableBytes=texture.isReadable?charge.Bytes:0;
                if(actual>ExpectedBytes)throw new System.IO.InvalidDataException("The image decoder exceeded its reserved dimensions.");
            }
            internal void Ready(Texture2D result){if(!working||Phase!="preparing"||!result||!retained.Any(t=>t.Texture==result))throw new InvalidOperationException("The image preparation ended");Texture=result;working=false;unallocated=0;Phase="ready";Collect();}
            internal void ReleaseScratch(Texture2D resource){if(resource)ArtResources.Release(resource);}
            public void Dispose(){if(Phase=="retiring")return;Phase="retiring";working=false;unallocated=0;Texture=null;foreach(var value in retained)if(value.Texture)ArtResources.Release(value.Texture);Collect();}
            internal bool Retained=>unallocated>0||working||retained.Any(t=>t.Texture);
        }
        internal static long TextureCost(int width,int height)
        {
            if(width<1||height<1)throw new ArgumentException("Invalid image dimensions");
            try{long bytes=0;checked{do{bytes+=(long)width*height*4;if(width==1&&height==1)break;width=Math.Max(1,width/2);height=Math.Max(1,height/2);}while(true);}return bytes;}catch(OverflowException){return long.MaxValue;}
        }
        internal static TextureLease BeginTexture(SurfaceImage.Info info,int encodedBytes,RoomResourceOwner owner,string hash)
        {
            Owner(owner);if(info==null||info.Width<1||info.Height<1||info.Width>SurfaceImage.MaximumSide||info.Height>SurfaceImage.MaximumSide||info.Orientation<1||info.Orientation>8||info.Format is not ("png" or "jpeg")||encodedBytes<24||encodedBytes>SurfaceImage.MaximumBytes||!ModelLibrary.ValidHash(hash))throw new ArgumentException("Invalid image texture reservation");
            int count=info.Orientation==1?1:2;long bytes=Math.Max(TextureCost(2,2),TextureCost(info.Width,info.Height));
            // Metadata removal can hold its stream and output; orientation holds
            // two Color32 arrays. Readable native texture copies are charged too.
            long scratch=(info.Metadata.Count==0?0:2L*encodedBytes)+(count==1?0:8L*info.Width*info.Height)+count*bytes;
            Collect();if(textures.Count>=MaximumTextureInstances||count*bytes>MaximumTextureBytes-Sum(textures.Select(t=>t.TextureBytes))||scratch>MaximumScratchBytes-Sum(textures.Select(t=>t.ScratchBytes)))
                throw new RoomResourceLimitException("The shared image texture budget is full. Remove an unused image binding or wait for retiring textures, then refresh the image library. Saved images are unchanged.");
            var lease=new TextureLease(owner,hash,bytes,count,scratch);textures.Add(lease);return lease;
        }
        internal static Task<TextureLease> DecodeAsync(ImageAsset asset,PayloadLease payload,CancellationToken cancel=default)
        {
            if(asset==null||payload==null||payload.Retiring||!payloads.Contains(payload)||asset.Content.Length>payload.Bytes)throw new InvalidOperationException("The image payload reservation is unavailable");
            payload.Identify(asset.Hash);var task=DecodeScheduled(asset,payload,cancel);payload.Hold(task);return task;
        }
        static async Task<TextureLease> DecodeScheduled(ImageAsset asset,PayloadLease payload,CancellationToken cancel)
        {
            if(decodeQueue.Count>=MaximumPayloadReads)throw new RoomResourceLimitException("The image decoder queue is full. Retry after the current image finishes.");
            var ticket=decodeQueue.AddLast(new object());
            try{
                while(decodeQueue.First!=ticket||decodedFrame==Time.frameCount){cancel.ThrowIfCancellationRequested();if(payload.Retiring)throw new OperationCanceledException();await Task.Yield();}
                cancel.ThrowIfCancellationRequested();if(payload.Retiring)throw new OperationCanceledException();decodedFrame=Time.frameCount;
                return Decode(asset,payload.Owner);
            }finally{decodeQueue.Remove(ticket);}
        }
        static TextureLease Decode(ImageAsset asset,RoomResourceOwner owner)
        {
            var info=asset.Inspection;var lease=BeginTexture(info,asset.Content.Length,owner,asset.Hash);Texture2D decoded=null,result=null;
            try{
                decoded=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Imported appearance image",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat,anisoLevel=1};lease.Track(decoded);
                bool loaded;
                try{loaded=ImageConversion.LoadImage(decoded,SurfaceImage.DecodeBytes(asset.Content,info),info.Orientation==1);}finally{lease.Inspect(decoded);}
                if(!loaded||decoded.width!=info.Width||decoded.height!=info.Height)throw new System.IO.InvalidDataException("The image decoder could not verify these pixels.");
                if(info.Orientation==1){result=decoded;decoded=null;lease.Ready(result);return lease;}
                var source=decoded.GetPixels32();var pixels=new Color32[source.Length];int w=info.DisplayWidth,h=info.DisplayHeight;
                for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++){var d=SurfaceImage.Upright(x,y,info.Width,info.Height,info.Orientation);pixels[(h-1-d.Y)*w+d.X]=source[(info.Height-1-y)*info.Width+x];}
                result=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name="Imported appearance image",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat,anisoLevel=1};lease.Track(result);result.SetPixels32(pixels);result.Apply(true,true);lease.Inspect(result);lease.Ready(result);return lease;
            }catch{lease.Dispose();throw;}finally{if(decoded&&lease.Phase!="retiring")lease.ReleaseScratch(decoded);}
        }
        internal static void Collect()
        {
            for(int i=payloads.Count-1;i>=0;i--){var p=payloads[i];p.Drain();if(p.Retiring&&!p.WorkPending)payloads.RemoveAt(i);}
            for(int i=textures.Count-1;i>=0;i--)if(!textures[i].Retained)textures.RemoveAt(i);
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["textureInstances"]=textures.Count,["textureBytes"]=Sum(textures.Select(t=>t.TextureBytes)),["decodeScratchBytes"]=Sum(textures.Select(t=>t.ScratchBytes)),["payloadReads"]=payloads.Count,["payloadBytes"]=Sum(payloads.Select(p=>p.Bytes)),["queuedDecodes"]=decodeQueue.Count,
                ["textureInstanceLimit"]=MaximumTextureInstances,["textureByteLimit"]=MaximumTextureBytes,["decodeScratchByteLimit"]=MaximumScratchBytes,["payloadReadLimit"]=MaximumPayloadReads,["payloadByteLimit"]=MaximumPayloadBytes,["decodesPerFrame"]=1};
        }
        static JObject Entry(string id,RoomResourceOwner owner,string hash,string kind,string phase,long textureBytes,long payloadBytes,long scratchBytes,int count,bool pending)=>new(){
            ["reservationId"]=id,["worldId"]=owner.World,["regionId"]=owner.Region,["target"]=owner.Target,["role"]=owner.Role,["imageHash"]=hash,["kind"]=kind,["phase"]=phase,["textureBytes"]=textureBytes,["payloadBytes"]=payloadBytes,["decodeScratchBytes"]=scratchBytes,["textureCount"]=count,["workPending"]=pending};
        internal static JObject Observe(int index)
        {
            Collect();if(index<0)return null;if(index<payloads.Count){var p=payloads[index];return Entry(p.Id,p.Owner,p.Hash,"payload",p.Phase,0,p.Bytes,0,0,p.WorkPending);}index-=payloads.Count;
            if(index>=textures.Count)return null;var t=textures[index];return Entry(t.Id,t.Owner,t.Hash,"texture",t.Phase,t.TextureBytes,0,t.ScratchBytes,t.TextureCount,false);
        }
    }
}
