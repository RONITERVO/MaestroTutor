// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class DrawingContractTests
    {
        [Test] public void DrawingCommandsShareTypedContractsAndExactResourceOwnership()
        {
            foreach(var entry in JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/drawing-contract.json")))){
                var call=entry["call"];Assert.That(BehaviourCatalog.TryCall((string)call["id"],1,(JObject)call["arguments"],out var parsed,out var error),Is.EqualTo((bool)entry["valid"]),(string)entry["name"]+": "+error);
                if((bool)entry["valid"])Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidCall((JObject)call),Is.True);
                if((bool)entry["valid"])Assert.That(parsed.Resources,Is.EqualTo((string)call["id"]=="object.drawing.edit"?new[]{(string)call["arguments"]["target"]}:System.Array.Empty<string>()));
            }
        }
        [Test] public void NativeDrawingDomainRejectsInvisibleAndOutOfRadiusShapes()
        {
            var args=(JObject)BehaviourCatalog.Action("object.create").InputSchema["oneOf"][3]["examples"][0];var first=args["points"][0];args["points"]=new JArray(first.DeepClone(),first.DeepClone());Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out _,out _),Is.False);
            args["points"][1]["x"]=10;args["points"][1]["y"]=10;Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out _,out _),Is.False);
            args["points"][1]["y"]=0;Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out _,out _),Is.True);
            var edit=new JObject {["operation"]="splice",["target"]=new string('a',32),["revision"]=1,["index"]=0,["deleteCount"]=0,["points"]=new JArray()};Assert.That(BehaviourCatalog.TryCall("object.drawing.edit",1,edit,out _,out _),Is.False);
        }
    }
}
