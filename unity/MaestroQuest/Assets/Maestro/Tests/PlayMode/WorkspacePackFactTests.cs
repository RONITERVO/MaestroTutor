// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        // The raw host JSON is not sufficient: empty arrays must survive the registered
        // fact's declared type, workspace reader and actual agent/catalog wire boundary.
        Func<string,int,JObject> PackPageReader()
        {
            string client=Guid.NewGuid().ToString("N");agent.Receive(new JObject {["clientId"]=client}.ToString());
            string session=agent.Observe().session;int sequence=0;
            return (id,offset)=>{
                var args=new JObject {["offset"]=offset};var definition=BehaviourCatalog.Fact(id);
                Assert.That(host.Runtime.TryRead(id,1,args,out var typed),Is.True);
                Assert.That(typed.Type,Is.EqualTo(definition.Type));Assert.That(definition.ValidValue(typed),Is.True);
                var expected=JObject.FromObject(typed.Value);Assert.That(ProgramValue.MeasureCharacters(expected),Is.LessThanOrEqualTo(ProgramValue.MaximumCharacters));
                string scene=JsonUtility.ToJson(host.Current.Editor.Snapshot());int revision=host.Current.Editor.Revision;
                var execution=agent.Observe().execution.DeepClone();
                var query=new JObject {["operation"]="inspect",["category"]="facts",["capability"]=id,["version"]=1,["arguments"]=args};
                var wire=new JObject {["clientId"]=client,["session"]=session,["request"]=new JObject {
                    ["version"]=2,["session"]=session,["sequence"]=++sequence,["sceneRevision"]=revision,
                    ["conditions"]=new JArray(),["commands"]=new JArray(new JObject {["action"]="catalog",["catalog"]=query})}};
                agent.Receive(wire.ToString());
                for(int repeat=0;repeat<3;repeat++){
                    var state=agent.Observe();Assert.That(state.session,Is.EqualTo(session));Assert.That(state.ack,Is.EqualTo(sequence));Assert.That(state.ok,Is.True,state.status);
                    Assert.That((bool)state.catalog["available"],Is.True,(string)state.catalog["status"]);
                    Assert.That((string)state.catalog["status"],Is.Not.EqualTo(RoomCapabilityCatalog.FactReadFailureStatus));
                    Assert.That(JToken.DeepEquals(state.catalog["value"],expected),Is.True,state.catalog.ToString());
                    Assert.That(ProgramValue.Literal(state.catalog["value"],definition.Type).Type,Is.EqualTo(definition.Type));
                    Assert.That(JToken.DeepEquals(state.execution,execution),Is.True,"Passive pages cannot alter action history.");
                }
                Assert.That(host.Current.Editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(host.Current.Editor.Snapshot()),Is.EqualTo(scene));
                return expected;
            };
        }
        static void EmptyPackPage(JObject page,int offset,int total)
        {
            Assert.That((int)page["offset"],Is.EqualTo(offset));Assert.That((int)page["total"],Is.EqualTo(total));
            Assert.That((int)page["next"],Is.EqualTo(-1));Assert.That((JArray)page["entries"],Is.Empty);
        }
        [UnityTest] public IEnumerator BundledWorldInstanceFactsReadEmptyPagesThroughAgentAndRuntime()
        {
            Open();yield return ReadyReviewOwners();var read=PackPageReader();string world=host.Current.Editor.WorldIdentity.worldId;
            foreach(int offset in new[]{0,WorldPackDocument.MaximumInstances}){
                var page=read("workspace.pack.instance",offset);EmptyPackPage(page,offset,0);Assert.That((string)page["worldId"],Is.EqualTo(world));
            }
            Assert.That(File.Exists(Path.Combine(host.Current.Editor.SaveDirectory,WorldPackDocument.FileName)),Is.False,"An empty read must not fabricate provenance.");
        }
        [UnityTest] public IEnumerator BundledWorldCatalogFactsReadActualEntryAndEndPages()
        {
            Open();yield return ReadyReviewOwners();var read=PackPageReader();var page=read("workspace.packs",0);
            var sources=WorldPackCatalog.Available;Assert.That(sources.Count,Is.GreaterThan(0));Assert.That((int)page["total"],Is.EqualTo(sources.Count));
            Assert.That(((JArray)page["entries"]).Count,Is.EqualTo(1));var first=page["entries"][0];
            Assert.That((string)first["id"],Is.EqualTo(sources[0].Id));Assert.That((int)first["version"],Is.EqualTo(sources[0].Version));
            Assert.That((string)first["hash"],Is.EqualTo(sources[0].Hash));Assert.That((string)first["name"],Is.EqualTo(sources[0].Name));Assert.That((string)first["entryId"],Is.EqualTo(sources[0].EntryId));
            EmptyPackPage(read("workspace.packs",sources.Count),sources.Count,sources.Count);
            EmptyPackPage(read("workspace.packs",32),32,sources.Count);
        }
        [UnityTest] public IEnumerator BundledWorldInstanceFactsReadInstalledOriginAndEndPages()
        {
            // Expand the real included source into canonical detached documents. The
            // asset-free baseline keeps this typing regression independent of large model I/O.
            var prepared=WorldPackPreparation.Prepare(WorldPackCatalog.Available.Single(e=>e.Id=="maestro.observatory"),WorkspaceDefaults.Snapshot());
            string data=Path.Combine(directory,"room");
            foreach(var pair in prepared.Snapshot.Documents){string path=Path.Combine(data,pair.Key.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pair.Value);}
            string provenancePath=Path.Combine(data,WorldPackDocument.FileName);byte[] before=File.ReadAllBytes(provenancePath);
            var provenance=WorldPackDocument.Decode(before);var instance=provenance.instances.Single();
            Open();yield return ReadyReviewOwners();var read=PackPageReader();var page=read("workspace.pack.instance",0);
            Assert.That((string)page["worldId"],Is.EqualTo(provenance.worldId));Assert.That((int)page["offset"],Is.Zero);Assert.That((int)page["total"],Is.EqualTo(1));Assert.That((int)page["next"],Is.EqualTo(-1));
            Assert.That(((JArray)page["entries"]).Count,Is.EqualTo(1));Assert.That(JToken.DeepEquals(page["entries"][0],new JObject {
                ["instanceId"]=instance.instanceId,["packId"]=instance.packId,["packVersion"]=instance.packVersion,["packHash"]=instance.sourceHash,
                ["entryId"]=instance.entryId,["objects"]=instance.objects.Length,["resources"]=instance.resources.Length}),Is.True);
            foreach(int offset in new[]{1,WorldPackDocument.MaximumInstances}){
                var empty=read("workspace.pack.instance",offset);EmptyPackPage(empty,offset,1);Assert.That((string)empty["worldId"],Is.EqualTo(provenance.worldId));
            }
            CollectionAssert.AreEqual(before,File.ReadAllBytes(provenancePath));
        }
    }
}
