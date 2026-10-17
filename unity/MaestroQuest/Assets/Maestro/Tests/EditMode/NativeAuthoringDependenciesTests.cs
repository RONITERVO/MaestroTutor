// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class NativeAuthoringDependenciesTests {
        static string A=>new('a',32);static string B=>new('b',32);
        static string[] Dependencies(string id,JObject args){Assert.That(BehaviourCatalog.TryCall(id,1,args,out var call,out var error),Is.True,error);return call.Definition.Module.NativeEntities(call.Arguments).Distinct().ToArray();}
        [Test] public void TypedCreationAcquiresOnlyTheExistingCopySource(){
            foreach(var kind in CreateObjectCapability.Kinds){var args=kind.Adapter.Public(kind.Provider.Example);if(kind.Name=="copy")args["target"]=A;
                Assert.That(Dependencies("object.create",args),Is.EquivalentTo(kind.Name=="copy"?new[]{A}:Array.Empty<string>()),kind.Name);
            }
        }
        [Test] public void ConnectionUsesObjectEndpointsWithoutTreatingNumericDriveTargetsAsObjects(){
            var args=new ConnectionCapability().Example;args["target"]=A;args["connected"]=B;
            Assert.That(Dependencies("object.connection.edit",args),Is.EquivalentTo(new[]{A,B}));
            args=new JObject{["operation"]="remove",["target"]=A,["revision"]=1};Assert.That(Dependencies("object.connection.edit",args),Is.EqualTo(new[]{A}));
        }
        [TestCase("object.container.transfer")] [TestCase("object.field.transfer")] [TestCase("object.material.transfer")]
        public void TransfersAcquireBothDistinctEndpoints(string id){
            var args=BehaviourCatalog.Action(id).Module.Example;args["source"]["target"]=A;args["destination"]["target"]=B;
            Assert.That(Dependencies(id,args),Is.EquivalentTo(new[]{A,B}));
        }
        [Test] public void CaptureAndExplicitStructureBaselinesAcquireTheirReferencedMembers(){
            var args=new StructureSaveCapability().Example;args["source"]["members"][0]["target"]=A;
            Assert.That(Dependencies("structure.save",args),Is.EqualTo(new[]{A}));
            var source=(JObject)args["source"];source["kind"]="definition";source.Remove("members");source["slots"]=new JArray(new JObject{["slot"]="piece",["placement"]=new JObject{["target"]=B,["position"]=new JObject{["x"]=0,["y"]=1,["z"]=0},["rotation"]=new JObject{["x"]=0,["y"]=0,["z"]=0,["w"]=1},["scale"]=1}});
            Assert.That(Dependencies("structure.save",args),Is.EqualTo(new[]{B}));
        }
        [Test] public void HidingAHandleDoesNotAcquireItsMembers(){
            var args=new ConstructionManipulationCapability().Example;args["members"]=new JArray(A,B);
            Assert.That(Dependencies("room.selection.manipulate",args),Is.EquivalentTo(new[]{A,B}));args["visible"]=false;
            Assert.That(Dependencies("room.selection.manipulate",args),Is.Empty);
        }
        [Test] public void ScannedLayerCreationHasNoObjectDependencyButRebindRequiresItsExactLayer(){
            Assert.That(Dependencies("drawing.layer.edit",new ScanDrawingCapability().Example),Is.Empty);
            var args=new JObject{["operation"]="rebind",["target"]=A,["revision"]=1,["stateId"]=B,["anchorId"]=B,["x"]=0,["y"]=0,["angle"]=0};
            Assert.That(Dependencies("drawing.layer.edit",args),Is.EqualTo(new[]{A}));
        }
        [TestCase("structure.forget")] [TestCase("appearance.save")] [TestCase("appearance.remove")]
        [TestCase("environment.profile.save")] [TestCase("environment.profile.remove")]
        [TestCase("visibility.layer.save")] [TestCase("visibility.layer.remove")]
        public void UnboundSavedDefinitionsDoNotRequireNativeObjects(string id){
            var args=BehaviourCatalog.Action(id).Module.Example;if(args["members"]!=null)args["members"]=new JArray();Assert.That(Dependencies(id,args),Is.Empty);
        }
    }
}
