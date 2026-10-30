// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest]public IEnumerator ResourceMembersFactsReadDormantObjectsAtCapacityWithoutAcquiringViews() {
            string target=editor.Identity(block),profile=NewEnvironment();
            Assert.That(AssignEnvironment(target,profile,out var error),Is.True,error);
            yield return NativeAreaCompletion(editor.RetireNativeArea(NativeAreaFor(target)));
            int revision=editor.Revision;var reservations=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try {
                int count=(int)NativeObjectResources.ObserveBudget()["instances"];
                var fact=BehaviourCatalog.Fact("resource.members");var args=new JObject{["kind"]="environment",["id"]=profile,["cursor"]=""};
                Assert.That(RoomControls.Capabilities(editor),Does.Contain(ResourceMembersFact.Feature));
                Assert.That(fact.TryRead(new BehaviourCatalog.FactContext(editor:editor),1,args,out var value),Is.True);
                var page=JObject.FromObject(value.Value);Assert.That(((JArray)page["members"]).Values<string>().Single(),Is.EqualTo(target));
                Assert.That((int)page["revision"],Is.EqualTo(editor.EnvironmentRevision(profile)));
                Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);
                Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));
                Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(count));
                string path=Environment.GetEnvironmentVariable("MAESTRO_RESOURCE_MEMBERS_EVIDENCE");
                if(!string.IsNullOrEmpty(path))System.IO.File.WriteAllText(path,new JObject{["arguments"]=args,["page"]=page}.ToString());
            } finally {foreach(var lease in reservations)lease.Dispose();}
        }
    }
}
