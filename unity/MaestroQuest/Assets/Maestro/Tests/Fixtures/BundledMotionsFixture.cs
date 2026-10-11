// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Tests
{
    public static class BundledMotionsFixture
    {
        public static string Manifest(params byte[][] models)
        {
            var packs=models.SelectMany(x=>MotionPack.Extract("fixture.glb",x)).ToArray();
            var catalogue=new MotionCatalogue {sources=packs.GroupBy(x=>x.SourceHash).Select(g=>new MotionSource {hash=g.Key,name="Fixture source",attribution="Project test content"}).ToArray(),
                entries=packs.GroupBy(x=>x.Hash).Select(g=>{var x=g.First();return new MotionEntry {id=x.Hash.Substring(0,32),hash=x.Hash,rigHash=x.RigHash,name=x.Name,duration=x.Duration,bytes=x.Bytes.Length,curveValues=x.CurveValues,tags=new[]{"included"},origins=g.Select(o=>new MotionOrigin {sourceHash=o.SourceHash,clipIndex=o.SourceClip}).ToArray()};}).ToArray()};
            return new JObject {["version"]=1,["packId"]="test.motions",["revision"]=1,["name"]="Test animations",["avatarHash"]=ModelLibrary.Hash(models[0]),["rigHash"]=packs[0].RigHash,["catalogue"]=JObject.FromObject(catalogue)}.ToString();
        }
        public static BundledMotions Write(string directory,params byte[][] models)
        {
            foreach(var model in models)foreach(var pack in MotionPack.Extract("fixture.glb",model)){string path=Path.Combine(directory,BundledMotions.RelativeDirectory+pack.Hash+".motion");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pack.Bytes);}
            return BundledMotions.FromDirectory(Manifest(models),directory);
        }
    }
}
