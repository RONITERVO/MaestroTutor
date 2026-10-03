// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Maestro.Quest.Avatar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    /// <summary>Immutable packaged motions, installed into the same portable library as user imports.</summary>
    public sealed class BundledMotions
    {
        public const string ResourcePath="Avatars/IncludedMotions",RelativeDirectory="MaestroContent/Motions/";
        public string Hash {get;}
        public string PackId {get;}
        public string Name {get;}
        public string AvatarHash {get;}
        public string RigHash {get;}
        public int Revision {get;}
        public int Count=>catalogue.entries.Length;
        public long Bytes=>catalogue.entries.Sum(x=>(long)x.bytes);
        readonly MotionCatalogue catalogue;
        readonly AvatarActivityDocument activities=new();
        public AvatarActivityDocument DefaultActivities(string modelHash)=>modelHash==AvatarHash?activities.Copy():new AvatarActivityDocument();
        readonly Func<string,int,byte[]> read;
        internal MotionCatalogue Catalogue=>catalogue.Copy();
        internal BundledMotions(string json,Func<string,int,byte[]> reader)
        {
            if(json==null||Encoding.UTF8.GetByteCount(json)>4*1024*1024)throw Invalid();
            using var input=new JsonTextReader(new StringReader(json)){MaxDepth=16,DateParseHandling=DateParseHandling.None};
            var value=JObject.Load(input,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            int version=value["version"]?.Type==JTokenType.Integer&&(long)value["version"] is >=1 and <=2?(int)value["version"]:0;
            var fields=version==2?new[]{"version","packId","revision","name","avatarHash","rigHash","catalogue","activities"}:new[]{"version","packId","revision","name","avatarHash","rigHash","catalogue"};
            if(input.Read()||value.Count!=fields.Length||!fields.All(value.ContainsKey)||version==0||
                !Text(value["packId"],80)||!System.Text.RegularExpressions.Regex.IsMatch((string)value["packId"],"^[a-z][a-z0-9.-]*$")||!Text(value["name"],100)||
                value["revision"]?.Type!=JTokenType.Integer||(long)value["revision"]<1||(long)value["revision"]>1000000||
                value["avatarHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)value["avatarHash"])||
                value["rigHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)value["rigHash"])||value["catalogue"] is not JObject)throw Invalid();
            catalogue=MotionLibrary.DecodeSnapshot(Encoding.UTF8.GetBytes(value["catalogue"].ToString(Formatting.None)));
            if(catalogue.entries.Length==0||catalogue.entries.Any(x=>x.rigHash!=(string)value["rigHash"]||x.archived||x.removed||x.favourite))throw Invalid();
            Hash=ModelLibrary.Hash(Encoding.UTF8.GetBytes(json));PackId=(string)value["packId"];Name=(string)value["name"];Revision=(int)value["revision"];AvatarHash=(string)value["avatarHash"];RigHash=(string)value["rigHash"];read=reader;
            if(version==2)activities=ReadActivities(value["activities"]);
        }
        AvatarActivityDocument ReadActivities(JToken value)
        {
            bool Exact(JObject item,params string[] fields)=>item!=null&&item.Count==fields.Length&&fields.All(item.ContainsKey);
            bool Number(JToken item)=>item?.Type is JTokenType.Integer or JTokenType.Float;
            if(value is not JArray roles||roles.Count>4)throw Invalid();
            foreach(var role in roles) {
                if(role is not JObject group||!Exact(group,"role","choices")||group["role"]?.Type!=JTokenType.Integer||(long)group["role"] is <0 or >3||group["choices"] is not JArray choices||choices.Count is <1 or >4)throw Invalid();
                foreach(var item in choices)if(item is not JObject choice||!Exact(choice,"motionId","weight","speed","cooldown","loop")||
                    choice["motionId"]?.Type!=JTokenType.String||choice["weight"]?.Type!=JTokenType.Integer||(long)choice["weight"] is <1 or >10||!Number(choice["speed"])||!Number(choice["cooldown"])||choice["loop"]?.Type!=JTokenType.Boolean)throw Invalid();
            }
            var profile=new AvatarActivityProfile {modelHash=AvatarHash,rigHash=RigHash,roles=JsonUtility.FromJson<AvatarActivityProfile>(new JObject {["roles"]=roles.DeepClone()}.ToString()).roles};
            var result=new AvatarActivityDocument {avatars=roles.Count==0?Array.Empty<AvatarActivityProfile>():new[]{profile}};
            if(!result.Valid()||profile.roles.SelectMany(x=>x.choices).Any(x=>!catalogue.entries.Any(entry=>entry.id==x.motionId&&!entry.Short)))throw Invalid();
            return result;
        }
        static bool Text(JToken value,int maximum)=>value?.Type==JTokenType.String&&!string.IsNullOrWhiteSpace((string)value)&&((string)value).Length<=maximum&&!((string)value).Any(c=>char.IsControl(c)||c is '<' or '>');
        static ModelImportException Invalid()=>new("The included animation package is invalid. Reinstall a verified app build.");
        internal static BundledMotions FromDirectory(string json,string streamingAssets)
        {
            string root=Path.GetFullPath(streamingAssets);
            return new BundledMotions(json,(hash,expected)=>{using var stream=File.OpenRead(Path.Combine(root,(RelativeDirectory+hash+".motion").Replace('/',Path.DirectorySeparatorChar)));return ReadBytes(stream,expected);});
        }
        internal static BundledMotions FromApk(string json,string apk)
        {
            string path=Path.GetFullPath(apk);
            return new BundledMotions(json,(hash,expected)=>{
                using var file=File.OpenRead(path);using var zip=new ZipArchive(file,ZipArchiveMode.Read);
                var entries=zip.Entries.Where(x=>x.FullName=="assets/"+RelativeDirectory+hash+".motion").Take(2).ToArray();
                if(entries.Length!=1||entries[0].Length!=expected)throw Invalid();using var stream=entries[0].Open();return ReadBytes(stream,expected);
            });
        }
        public static BundledMotions FromApplication()
        {
            var asset=Resources.Load<TextAsset>(ResourcePath);if(!asset)return null;
            string json;try{json=asset.text;}finally{Resources.UnloadAsset(asset);}
            return Application.platform==RuntimePlatform.Android?FromApk(json,Application.dataPath):FromDirectory(json,Application.streamingAssetsPath);
        }
        static byte[] ReadBytes(Stream stream,int expected)
        {
            if(stream.CanSeek&&stream.Length!=expected)throw Invalid();var bytes=new byte[expected];int offset=0;
            while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw Invalid();offset+=count;}
            if(stream.ReadByte()!=-1)throw Invalid();return bytes;
        }
        internal byte[] Read(string hash)
        {
            var entry=catalogue.entries.FirstOrDefault(x=>x.hash==hash)??throw Invalid();var bytes=read(entry.hash,entry.bytes);
            if(bytes.Length!=entry.bytes||ModelLibrary.Hash(bytes)!=entry.hash)throw Invalid();var pack=MotionPack.Read(bytes);
            if(pack.RigHash!=entry.rigHash||pack.CurveValues!=entry.curveValues||Math.Abs(pack.Duration-entry.duration)>.0001f)throw Invalid();return bytes;
        }
        public void Verify(){foreach(var entry in catalogue.entries)Read(entry.hash);}
    }
}
