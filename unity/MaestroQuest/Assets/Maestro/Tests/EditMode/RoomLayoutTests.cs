// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomLayoutTests
    {
        static JObject Args()=>new() { ["placements"]=new JArray(Enumerable.Range(0,16).Select(i=>new JObject { ["target"]=i.ToString("x32"),["position"]=new JObject { ["x"]=i*.1,["y"]=1,["z"]=0},["rotation"]=new JObject { ["x"]=0,["y"]=0,["z"]=0,["w"]=1},["scale"]=1}))};
        [Test] public void LayoutReservesEveryLiteralMemberAndRefusesDuplicatesOrInvalidCoordinates() {
            var args=Args();Assert.That(BehaviourCatalog.TryCall("object.layout.apply",1,args,out var call,out var error),Is.True,error);
            Assert.That(call.Resources.Length,Is.EqualTo(16));Assert.That(call.Claims.Length,Is.EqualTo(16));Assert.That(call.TryStep(out _,out _),Is.False);
            var source=BehaviourProgram.FromInvocation(new JObject { ["id"]=call.Definition.Id,["version"]=1,["arguments"]=args});Assert.That(BehaviourProgram.TryParse(source,out _,out error),Is.True,error);
            args["placements"][15]["target"]=args["placements"][0]["target"].DeepClone();Assert.That(BehaviourCatalog.TryCall("object.layout.apply",1,args,out _,out _),Is.False);
            args=Args();args["placements"][0]["position"]=new JObject { ["x"]=25,["y"]=25,["z"]=25};Assert.That(BehaviourCatalog.TryCall("object.layout.apply",1,args,out _,out _),Is.False);
            args=Args();args["placements"][0]["rotation"]["w"]=0;Assert.That(BehaviourCatalog.TryCall("object.layout.apply",1,args,out _,out _),Is.False);
            args=Args();args["placements"][0]["target"]="maestro";Assert.That(BehaviourCatalog.TryCall("object.layout.apply",1,args,out _,out _),Is.False);
            args=Args();((JArray)args["placements"]).Add(args["placements"][0].DeepClone());Assert.That(BehaviourCatalog.TryCall("object.layout.apply",1,args,out _,out _),Is.False);
        }
    }
}
