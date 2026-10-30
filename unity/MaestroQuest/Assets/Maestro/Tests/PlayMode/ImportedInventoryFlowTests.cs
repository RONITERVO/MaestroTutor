// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator RefreshInventory(string capability,Action<JObject,JObject> observed)
        {
            var actions=new RoomExecutions(editor);string run=(string)actions.Observe()["nextRunId"];
            var request=new JObject{["operation"]="start",["runId"]=run,["call"]=new JObject{["id"]=capability,["version"]=1,["arguments"]=new JObject{["operation"]="refresh"}}};
            Assert.That(actions.Execute(request,out var error),Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>((string)runtime.Scheduler.Invocation(run)?["phase"] is "completed" or "failed" or "cancelled"),"Library refresh did not drain");
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
            var output=(JObject)receipt["output"];Assert.That(CapabilityArguments.Validate(output,BehaviourCatalog.Action(capability).OutputSchema,out error),Is.True,error);
            observed(request,output);
        }
        JObject InventoryPage(string fact,int offset)
        {
            Assert.That(BehaviourCatalog.TryRead(fact,1,new JObject{["offset"]=offset},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            return (JObject)value.Value;
        }
        [UnityTest] public IEnumerator MediaRefreshRetainsHealthyPagesAndWarnsOnEveryPartialOrAllDamagedPage()
        {
            foreach(bool image in new[]{true,false}){
                string capability=image?"image.import":"audio.import",fact=image?"image.library":"audio.library";
                string path=Path.Combine(editor.SaveDirectory,image?"images":"audio"),extension=image?".image":".wav";
                Assert.That((bool)InventoryPage(fact,0)["ready"],Is.False,"A passive read does not inspect files");
                JObject empty=null;yield return RefreshInventory(capability,(_,output)=>empty=output);
                Assert.That((bool)empty["inventory"]["complete"],Is.True);Assert.That((int)empty["library"]["total"],Is.Zero);Assert.That((string)empty["library"]["error"],Is.Empty);
                var hashes=new List<string>();
                for(int n=0;n<3;n++){
                    string name=new string('"',80);
                    if(image){var asset=ImageLibrary.Inspect(name,ImageFiles.OrientPng(ImageFiles.Png(),n+2));hashes.Add(asset.Hash);var save=editor.Images.SaveAsync(asset);yield return PhysicalCreationTestWait.Until(()=>save.IsCompleted,"Image inventory fixture did not save");Assert.That(save.Exception,Is.Null);}
                    else{var bytes=SoundFile();bytes[44]=(byte)n;var asset=AudioLibrary.Inspect(name,bytes);hashes.Add(asset.Hash);var save=editor.Sounds.SaveAsync(asset);yield return PhysicalCreationTestWait.Until(()=>save.IsCompleted,"Sound inventory fixture did not save");Assert.That(save.Exception,Is.Null);}
                }
                string damaged=Path.Combine(path,new string('0',64)+extension);File.WriteAllBytes(damaged,new byte[]{7});
                JObject request=null,partial=null;yield return RefreshInventory(capability,(call,output)=>{request=call;partial=output;});
                Assert.That((bool)partial["inventory"]["ready"],Is.True);Assert.That((bool)partial["inventory"]["complete"],Is.False);Assert.That((bool)partial["inventory"]["enumerationComplete"],Is.True);Assert.That((int)partial["inventory"]["issueCount"],Is.EqualTo(1));
                Assert.That((int)partial["library"]["total"],Is.EqualTo(3));Assert.That(partial["library"]["entries"].Count(),Is.EqualTo(1));Assert.That((int)partial["library"]["next"],Is.EqualTo(1));
                Assert.DoesNotThrow(()=>ProgramValue.Literal(partial),"A full partial refresh receipt still fits the shared value budget");
                string warning=(string)partial["library"]["error"];Assert.That(warning,Is.Not.Empty);Assert.That((string)partial["inventory"]["error"],Is.EqualTo(warning));
                var later=InventoryPage(fact,1);Assert.That(later["entries"].Count(),Is.EqualTo(2));Assert.That((int)later["next"],Is.EqualTo(-1));
                Assert.That(new[]{(string)partial["library"]["entries"][0]["id"]}.Concat(later["entries"].Select(e=>(string)e["id"])),Is.EqualTo(hashes.OrderBy(x=>x,StringComparer.Ordinal)));
                foreach(int offset in new[]{0,1,3,32}){var page=InventoryPage(fact,offset);Assert.That((bool)page["ready"],Is.True);Assert.That((int)page["total"],Is.EqualTo(3));Assert.That((string)page["error"],Is.EqualTo(warning));}
                foreach(string hash in hashes)File.WriteAllBytes(Path.Combine(path,hash+extension),new byte[]{8});
                Assert.That((int)InventoryPage(fact,0)["total"],Is.EqualTo(3),"Facts read the observed snapshot without passive I/O");
                var replay=new RoomExecutions(editor);Assert.That(replay.Execute(request,out var error),Is.True,error);
                Assert.That(JToken.DeepEquals(runtime.Scheduler.Invocation((string)request["runId"])["output"],partial),Is.True,"Replaying a retained request must keep its historical scan evidence");
                JObject allBad=null;yield return RefreshInventory(capability,(_,output)=>allBad=output);
                Assert.That((bool)allBad["library"]["ready"],Is.True);Assert.That((int)allBad["library"]["total"],Is.Zero);Assert.That((int)allBad["library"]["next"],Is.EqualTo(-1));Assert.That((bool)allBad["inventory"]["complete"],Is.False);Assert.That((int)allBad["inventory"]["issueCount"],Is.EqualTo(4));
                foreach(int offset in new[]{0,1,32}){var page=InventoryPage(fact,offset);Assert.That((bool)page["ready"],Is.True);Assert.That(page["entries"].Count(),Is.Zero);Assert.That((string)page["error"],Is.Not.Empty);}
                Assert.That(File.ReadAllBytes(damaged),Is.EqualTo(new byte[]{7}),"Inspection preserves damaged files");
            }
        }
        [UnityTest] public IEnumerator MediaRefreshReportsUnavailableRootWithoutClaimingHealthyEmpty()
        {
            foreach(bool image in new[]{true,false}){
                string path=Path.Combine(editor.SaveDirectory,image?"images":"audio"),held=path+"-held";bool existed=Directory.Exists(path);
                if(existed)Directory.Move(path,held);File.WriteAllText(path,"not a directory");
                try{
                    JObject output=null;yield return RefreshInventory(image?"image.import":"audio.import",(_,value)=>output=value);
                    Assert.That((bool)output["library"]["ready"],Is.False);Assert.That((bool)output["inventory"]["ready"],Is.False);Assert.That((bool)output["inventory"]["complete"],Is.False);Assert.That((bool)output["inventory"]["enumerationComplete"],Is.False);
                    Assert.That((int)output["library"]["total"],Is.Zero);Assert.That((string)output["library"]["error"],Is.Not.Empty);Assert.That((string)InventoryPage(image?"image.library":"audio.library",32)["error"],Is.EqualTo((string)output["library"]["error"]));
                    Assert.That(File.ReadAllText(path),Is.EqualTo("not a directory"));
                }finally{File.Delete(path);if(existed)Directory.Move(held,path);}
            }
        }
    }
    public sealed partial class AnimationWorkshopTests
    {
        JObject InventoryModelRequest(int offset)=>new(){["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject{["id"]="model.library.inspect",["version"]=1,["arguments"]=new JObject{["offset"]=offset}}};
        [UnityTest] public IEnumerator ModelBrowsePagesKeepHealthyMetadataAndDistinguishDamagedUnavailableAndEmpty()
        {
            AuthorRuntime();string path=Path.Combine(directory,"models");Directory.CreateDirectory(path);
            for(int n=1;n<=9;n++){string hash=n.ToString("x64");File.WriteAllBytes(Path.Combine(path,hash+".glb"),new byte[28]);File.WriteAllText(Path.Combine(path,hash+".txt"),"Metadata "+n);}
            string damaged=Path.Combine(path,new string('0',64)+".glb");File.WriteAllBytes(damaged,new byte[]{1});
            foreach(int offset in new[]{0,8,32}){
                Assert.That(authorActions.Execute(InventoryModelRequest(offset),out var error),Is.True,error);yield return ModelFinished();
                var output=authorActions.Observe()["selected"]["output"];Assert.That((int)output["total"],Is.EqualTo(9));Assert.That((int)output["offset"],Is.EqualTo(offset));Assert.That(output["entries"].Count(),Is.EqualTo(offset==0?8:offset==8?1:0));
                Assert.That((bool)output["inventory"]["ready"],Is.True);Assert.That((bool)output["inventory"]["complete"],Is.False);Assert.That((int)output["inventory"]["issueCount"],Is.EqualTo(1));Assert.That((string)output["inventory"]["error"],Is.Not.Empty);
                Assert.That(CapabilityArguments.Validate(output,BehaviourCatalog.Action("model.library.inspect").OutputSchema,out error),Is.True,error);
            }
            Assert.That(avatar.CustomModel,Is.Null,"Metadata browsing never loads or validates a humanoid");
            for(int n=1;n<=9;n++)File.WriteAllBytes(Path.Combine(path,n.ToString("x64")+".glb"),new byte[]{2});
            Assert.That(authorActions.Execute(InventoryModelRequest(0),out var reason),Is.True,reason);yield return ModelFinished();var allBad=authorActions.Observe()["selected"]["output"];
            Assert.That((int)allBad["total"],Is.Zero);Assert.That((bool)allBad["inventory"]["ready"],Is.True);Assert.That((bool)allBad["inventory"]["complete"],Is.False);Assert.That((int)allBad["inventory"]["issueCount"],Is.EqualTo(10));
            string held=path+"-held";Directory.Move(path,held);
            try{
                File.WriteAllText(path,"not a directory");
                Assert.That(authorActions.Execute(InventoryModelRequest(0),out reason),Is.True,reason);yield return ModelFinished();var unavailable=authorActions.Observe()["selected"]["output"];
                Assert.That((bool)unavailable["inventory"]["ready"],Is.False);Assert.That((bool)unavailable["inventory"]["complete"],Is.False);Assert.That((string)unavailable["inventory"]["error"],Is.Not.Empty);
                File.Delete(path);
                Assert.That(authorActions.Execute(InventoryModelRequest(0),out reason),Is.True,reason);yield return ModelFinished();var empty=authorActions.Observe()["selected"]["output"];
                Assert.That((int)empty["total"],Is.Zero);Assert.That((bool)empty["inventory"]["ready"],Is.True);Assert.That((bool)empty["inventory"]["complete"],Is.True);Assert.That((string)empty["inventory"]["error"],Is.Empty);
            }finally{if(File.Exists(path))File.Delete(path);Directory.Move(held,path);}
            Assert.That(File.ReadAllBytes(damaged),Is.EqualTo(new byte[]{1}));
        }
        [UnityTest] public IEnumerator StoppedModelBrowseReleasesItsWriteWhileLibraryOwnerRemainsHeld()
        {
            AuthorRuntime();Assert.That(editor.Models.TryCaptureArchive(out var owner),Is.True);
            try{
                var request=InventoryModelRequest(0);Assert.That(authorActions.Execute(request,out var error),Is.True,error);
                Assert.That(editor.WriteGate.CanFreeze(out _),Is.False,"The accepted scan owns a workspace write while waiting");
                Assert.That(authorActions.Execute(new JObject{["operation"]="cancel",["runId"]=request["runId"].DeepClone()},out error),Is.True,error);
                yield return PhysicalCreationTestWait.Until(()=>editor.WriteGate.CanFreeze(out _),"Cancelled scan did not release its workspace write while the library remained held");
                var receipt=authorActions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("cancelled"));Assert.That(receipt["output"],Is.Null);
            }finally{owner.Dispose();}
            yield return null;Assert.That(authorActions.Observe()["selected"]["output"],Is.Null,"Draining cancellation must not publish a later result");
        }
    }
}
