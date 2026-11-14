// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    /// <summary>One immutable, offline avatar. Capture Unity paths on the main thread;
    /// bytes are read and validated on a worker, then use the ordinary model library.</summary>
    public sealed class BundledAvatar
    {
        internal const string ResourcePath="Avatars/IncludedMaestro";
        public const string RelativePath="MaestroContent/IncludedMaestro.glb";
        public string Hash {get;}
        public string Name {get;}
        public int Bytes {get;}
        public int WalkClipIndex {get;}
        public string Attribution {get;}
        readonly Func<byte[]> read;
        BundledAvatar(string json,Func<int,byte[]> reader)
        {
            if(json==null||Encoding.UTF8.GetByteCount(json)>8192)throw Invalid();
            using var text=new StringReader(json);using var input=new JsonTextReader(text){MaxDepth=4,DateParseHandling=DateParseHandling.None};
            var value=JObject.Load(input,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            var fields=new[]{"version","sha256","name","bytes","attribution","walkClipIndex"};
            if(input.Read()||value.Count!=fields.Length||!fields.All(value.ContainsKey)||value["version"]?.Type!=JTokenType.Integer||(long)value["version"]!=1||
                value["sha256"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)value["sha256"])||!Text(value["name"],100)||!Text(value["attribution"],2048)||
                value["walkClipIndex"]?.Type!=JTokenType.Integer||(long)value["walkClipIndex"] < -1||(long)value["walkClipIndex"]>31||
                value["bytes"]?.Type!=JTokenType.Integer||(long)value["bytes"]<28||(long)value["bytes"]>ModelInspection.MaximumBytes)throw Invalid();
            Hash=(string)value["sha256"];Name=(string)value["name"];Bytes=(int)value["bytes"];Attribution=(string)value["attribution"];WalkClipIndex=(int)value["walkClipIndex"];read=()=>reader(Bytes);
        }
        static bool Text(JToken value,int maximum)=>value?.Type==JTokenType.String&&((string)value).Length is >0&&((string)value).Length<=maximum&&!((string)value).Any(c=>char.IsControl(c)||c is '<' or '>');
        static ModelImportException Invalid()=>new("The included avatar package is invalid. Reinstall a verified app build.");
        internal static BundledAvatar FromDirectory(string json,string streamingAssets)
        {
            string path=Path.Combine(Path.GetFullPath(streamingAssets),RelativePath.Replace('/',Path.DirectorySeparatorChar));
            return new BundledAvatar(json,expected=>{using var stream=File.OpenRead(path);return ReadBytes(stream,expected);});
        }
        internal static BundledAvatar FromApk(string json,string apk)
        {
            string path=Path.GetFullPath(apk);
            return new BundledAvatar(json,expected=>{
                using var file=File.OpenRead(path);using var archive=new ZipArchive(file,ZipArchiveMode.Read);
                var entries=archive.Entries.Where(x=>x.FullName=="assets/"+RelativePath).Take(2).ToArray();
                if(entries.Length!=1||entries[0].Length!=expected)throw Invalid();
                using var stream=entries[0].Open();return ReadBytes(stream,expected);
            });
        }
        public static BundledAvatar FromApplication()
        {
            var manifest=Resources.Load<TextAsset>(ResourcePath);if(!manifest)return null;
            string json;try{json=manifest.text;}finally{Resources.UnloadAsset(manifest);}
            return Application.platform==RuntimePlatform.Android?FromApk(json,Application.dataPath):FromDirectory(json,Application.streamingAssetsPath);
        }
        static byte[] ReadBytes(Stream stream,int expected)
        {
            if(stream.CanSeek&&stream.Length!=expected)throw Invalid();var bytes=new byte[expected];int offset=0;
            while(offset<expected){int count=stream.Read(bytes,offset,expected-offset);if(count==0)throw Invalid();offset+=count;}
            if(stream.ReadByte()!=-1)throw Invalid();return bytes;
        }
        public ModelAsset Read()
        {
            var bytes=read();if(bytes.Length!=Bytes||ModelLibrary.Hash(bytes)!=Hash)throw Invalid();
            return ModelLibrary.Inspect(Name,bytes);
        }
    }
}
