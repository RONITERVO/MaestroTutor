// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Imports {
    /// <summary>Ephemeral byte transport. Advertising never imports an image.
    /// The shared import action selects exact advertised bytes before receiving.</summary>
    internal sealed class ChatImageTransfer {
        internal const int ChunkBytes=16384;
        string context="",set="",request="",hash="",name="",error="";
        JArray offers=new(); byte[] buffer; int offset; double changed;
        internal string OfferSet=>set;
        internal string Error=>error;
        internal string Name=>name;
        internal bool Active=>buffer!=null;
        static bool Id(string v)=>v!=null&&v.Length==32&&v.All(c=>c>='a'&&c<='f'||c>='0'&&c<='9');
        static bool Text(JToken v,int max)=>v?.Type==JTokenType.String&&((string)v).Length<=max&&!((string)v).Any(char.IsControl);
        static bool Integer(JToken v,int min,int max)=>v?.Type==JTokenType.Integer&&(long)v>=min&&(long)v<=max;
        static bool Valid(JObject v)=>v!=null&&v.Count==5&&ModelLibrary.ValidHash((string)v["imageHash"])&&Text(v["name"],80)&&Text(v["messageId"],128)&&Text(v["origin"],32)&&Integer(v["bytes"],24,SurfaceImage.MaximumBytes);
        internal void Clear(){context=set=request=hash=name=error="";offers=new();buffer=null;offset=0;}
        internal void Receive(string session,JObject wire,double now) {
            if(context!=session){Clear();context=session;}
            if(wire==null)return; // Another large command can temporarily own the wire.
            if(wire.Count<2||wire.Count>3||!Id((string)wire["offerSet"])||wire["offers"] is not JArray list||list.Count>8||list.Any(v=>!Valid(v as JObject))||list.Select(v=>(string)v["imageHash"]).Distinct().Count()!=list.Count)
                throw new InvalidDataException("Invalid chat image offers.");
            string incoming=(string)wire["offerSet"];
            if(set==incoming&&!JToken.DeepEquals(offers,list))throw new InvalidDataException("Chat image offers changed without a new identity.");
            if(set!=incoming){if(Active)Fail("The chat image selection changed. Choose the image again.");set=incoming;offers=(JArray)list.DeepClone();}
            if(wire["chunk"]==null||!Active)return;
            if(wire["chunk"] is not JObject chunk||chunk.Count!=4||!Integer(chunk["offset"],0,SurfaceImage.MaximumBytes)||chunk["data"]?.Type!=JTokenType.String)throw new InvalidDataException("Invalid image transfer chunk.");
            if((string)chunk["requestId"]!=request||(string)chunk["imageHash"]!=hash)return;
            int at=(int)chunk["offset"];if(at<offset)return; // Already acknowledged; never write twice.
            if(at!=offset)throw new InvalidDataException("Out-of-order image transfer.");
            string encoded=(string)chunk["data"];
            if(encoded.Length>21848)throw new InvalidDataException("Image chunk exceeds its bound.");
            byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch(FormatException){throw new InvalidDataException("Invalid image chunk encoding.");}
            if(bytes.Length!=Math.Min(ChunkBytes,buffer.Length-offset))throw new InvalidDataException("Incomplete image transfer chunk.");
            Buffer.BlockCopy(bytes,0,buffer,offset,bytes.Length);offset+=bytes.Length;changed=now;
        }
        internal bool CanSelect(string offerSet,string imageHash,out string issue){
            issue="This chat image is no longer offered. Read image.chat.library again.";
            if(!Id(offerSet)||set!=offerSet||!ModelLibrary.ValidHash(imageHash)||!offers.Any(v=>(string)v["imageHash"]==imageHash))return false;
            issue=null;return true;
        }
        internal void Start(string id,string offerSet,string imageHash,double now){
            if(!CanSelect(offerSet,imageHash,out var issue))throw new InvalidOperationException(issue);
            var offer=offers.First(v=>(string)v["imageHash"]==imageHash);
            request=id;hash=imageHash;name=(string)offer["name"];buffer=new byte[(int)offer["bytes"]];offset=0;error="";changed=now;
        }
        internal void Stop(){buffer=null;request="";offset=0;}
        internal void Fail(string issue){Stop();error=issue;}
        internal byte[] TakeComplete(double now){
            if(!Active)return null;
            if(offset==buffer.Length){var bytes=buffer;Stop();return bytes;}
            if(now-changed>15)Fail("The chat image transfer stopped. Resume the same book and choose the image again.");
            return null;
        }
        internal JObject Request()=>Active&&offset<buffer.Length?new JObject{["requestId"]=request,["offerSet"]=set,["imageHash"]=hash,["offset"]=offset,["bytes"]=buffer.Length}:null;
        internal JObject Library(int start)=>new(){["offerSet"]=set,["total"]=offers.Count,["next"]=start+2<offers.Count?start+2:-1,["entries"]=new JArray(offers.Skip(start).Take(2).Select(v=>new JObject{
            ["id"]=(string)v["imageHash"],["revision"]=1,["imageHash"]=(string)v["imageHash"],["name"]=(string)v["name"],["messageId"]=(string)v["messageId"],["origin"]=(string)v["origin"],["kibibytes"]=(int)v["bytes"]/1024d}))};
    }
}
