// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class QuestRoomProbeTimeoutTests
    {
        static JObject Owner()=>new JObject{["fullJourneyTimeoutMinutes"]=20,["journey"]="Headless",["providerUsed"]=false,["providerScenario"]="",["resumedFrom"]="",["sparseObjectView"]=false,
            ["firstLearner"]=false,["liveContinuity"]=false,["nativeRestart"]=false,["actionInterruption"]=false,["transportOnly"]=false,["motionOnly"]=false,["steeringOnly"]=false,["identityOnly"]=false};
        [TestCase("Headless",false,15,900)] [TestCase("Headless",true,20,1200)] [TestCase("Book",false,20,1200)] [TestCase("Book",true,30,1800)]
        public void OrdinaryJourneyUsesRecordedBoundedBudget(string journey,bool sparse,int minutes,int seconds)
        {var owner=Owner();owner["journey"]=journey;owner["sparseObjectView"]=sparse;owner["fullJourneyTimeoutMinutes"]=minutes;Assert.That(QuestRoomProbe.ResolveTimeoutSeconds(owner,false),Is.EqualTo(seconds));}
        [TestCase(false,900)] [TestCase(true,3600)]
        public void MissingAndZeroOverridesPreserveDedicatedLimits(bool learner,int seconds)
        {var owner=new JObject();Assert.That(QuestRoomProbe.ResolveTimeoutSeconds(owner,learner),Is.EqualTo(seconds));owner["fullJourneyTimeoutMinutes"]=0;Assert.That(QuestRoomProbe.ResolveTimeoutSeconds(owner,learner),Is.EqualTo(seconds));}
        [TestCase(-1)] [TestCase(14)] [TestCase(31)] [TestCase(2147483647)]
        public void OutOfRangeBudgetsAreRefused(int minutes)
        {var owner=Owner();owner["fullJourneyTimeoutMinutes"]=minutes;Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(owner,false));}
        [Test] public void CoercedAndFractionalBudgetsAreRefused()
        {foreach(var token in new JToken[]{new JValue("20"),new JValue(20.5),new JValue(true),JValue.CreateNull()}){var owner=Owner();owner["fullJourneyTimeoutMinutes"]=token;Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(owner,false));}}
        [TestCase("providerUsed")] [TestCase("firstLearner")] [TestCase("liveContinuity")] [TestCase("nativeRestart")] [TestCase("actionInterruption")] [TestCase("transportOnly")] [TestCase("motionOnly")] [TestCase("steeringOnly")] [TestCase("identityOnly")]
        public void DedicatedModesCannotReceiveFullJourneyBudget(string flag)
        {var owner=Owner();owner[flag]=true;Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(owner,false));}
        [TestCase("providerUsed")] [TestCase("sparseObjectView")] [TestCase("firstLearner")] [TestCase("liveContinuity")] [TestCase("nativeRestart")] [TestCase("actionInterruption")] [TestCase("transportOnly")] [TestCase("motionOnly")] [TestCase("steeringOnly")] [TestCase("identityOnly")]
        public void OfflineFlagsMustBeExplicitBooleans(string flag)
        {var owner=Owner();owner.Remove(flag);Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(owner,false));owner[flag]="false";Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(owner,false));}
        [TestCase("providerScenario","ContextCreateEdit")] [TestCase("resumedFrom","previous")] [TestCase("restartOf","previous")] [TestCase("restartOf","")] [TestCase("journey","Other")]
        public void ProviderResumeAndUnknownJourneyAreRefused(string field,string value)
        {var owner=Owner();owner[field]=value;Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(owner,false));}
        [Test] public void InteractiveLearnerEnvironmentCannotReceiveFullBudget()
        {Assert.Throws<InvalidDataException>(()=>QuestRoomProbe.ResolveTimeoutSeconds(Owner(),true));}
    }
}
