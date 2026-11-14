// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Maestro.Quest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class QuestRoomProbeRestartTests
    {
        string directory,child,workspace,sourceId,childId;
        JObject owner,sourceOwner,request,receipt,ready;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Maestro-probe-restart-"+Guid.NewGuid().ToString("N"));
            child=Path.Combine(directory,"restart");workspace=Path.Combine(directory,"workspace");
            Directory.CreateDirectory(child);Directory.CreateDirectory(workspace);
            sourceId=Guid.NewGuid().ToString("N");childId=Guid.NewGuid().ToString("N");
            sourceOwner=Owner(sourceId);owner=Owner(childId);owner["restartOf"]=sourceId;
            request=new JObject{["version"]=1,["id"]=sourceId,["operation"]="restart",["clientId"]=new string('3',32),["nativeSession"]=new string('4',32),["appleId"]=new string('5',32),["treeId"]=new string('6',32),["checkpoint"]=new JObject{["room"]=Clean(),["behaviours"]=Clean()}};
            ready=new JObject{["version"]=1,["id"]=sourceId,["processId"]=4321,["processStartedAt"]=1000L,["workspaceDirectory"]=workspace,["restartOf"]=""};
            receipt=new JObject{["version"]=1,["id"]=sourceId,["method"]="terminate-after-checkpoint",["workspace"]=workspace,["source"]=new JObject{["id"]=sourceId,["processId"]=4321,["startedAt"]=1000L,["exitedAt"]=2000L,["exitCode"]=-1,["terminalExisted"]=false},["resumed"]=new JObject{["id"]=childId,["directory"]=child}};
            WriteRequest();SaveEvidence();
        }
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static JObject Owner(string id)=>new JObject{["version"]=1,["id"]=id,["firstLearner"]=true,["liveContinuity"]=true,["nativeRestart"]=true};
        static JObject Clean()=>new JObject{["phase"]="idle",["pending"]=false,["dirty"]=false,["dispatchHeld"]=false,["error"]=""};
        static void Write(string path,JObject value)=>File.WriteAllText(path,value.ToString(Newtonsoft.Json.Formatting.None),new UTF8Encoding(false));
        static string Hash(byte[] value){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
        void WriteRequest(){Write(Path.Combine(directory,"restart-request.json"),request);receipt["requestSha256"]=Hash(File.ReadAllBytes(Path.Combine(directory,"restart-request.json")));}
        void SaveEvidence(){Write(Path.Combine(directory,"owner.json"),sourceOwner);Write(Path.Combine(directory,"ready.json"),ready);Write(Path.Combine(directory,"restart.json"),receipt);Write(Path.Combine(child,"owner.json"),owner);}
        void Refused(){SaveEvidence();Assert.Throws<InvalidDataException>(()=>QuestRoomProbeRestart.Resolve(child,owner));}

        [Test] public void RestartUsesOriginalWorkspaceWithoutWritingCopyingOrChangingEvidence()
        {
            File.WriteAllText(Path.Combine(workspace,"preserved.txt"),"unchanged native workspace");
            var before=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>Hash(File.ReadAllBytes(x)));
            var result=QuestRoomProbeRestart.Resolve(child,owner);
            Assert.That(result.WorkspaceDirectory,Is.EqualTo(workspace));Assert.That(result.RestartOf,Is.EqualTo(sourceId));
            Assert.That(Directory.Exists(Path.Combine(child,"workspace")),Is.False);
            var after=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>Hash(File.ReadAllBytes(x)));
            Assert.That(after.Keys,Is.EquivalentTo(before.Keys));foreach(var pair in before)Assert.That(after[pair.Key],Is.EqualTo(pair.Value));
        }
        void ActionMode()
        {
            foreach(var value in new[]{owner,sourceOwner}){value["actionInterruption"]=true;value["firstLearner"]=false;value["liveContinuity"]=false;}
            request["version"]=2;request.Remove("appleId");request.Remove("treeId");request["runId"]=new string('5',32);request["taskId"]="room-task:assistant";
            WriteRequest();SaveEvidence();
        }
        [Test] public void ExplicitActionInterruptionUsesOriginalWorkspace()
        {
            ActionMode();var result=QuestRoomProbeRestart.Resolve(child,owner);
            Assert.That(result.WorkspaceDirectory,Is.EqualTo(workspace));Assert.That(result.RestartOf,Is.EqualTo(sourceId));
            Assert.That(QuestRoomProbeRestart.Resolve(directory,sourceOwner).WorkspaceDirectory,Is.EqualTo(workspace));
        }
        [TestCase("firstLearner")][TestCase("liveContinuity")]
        public void ActionModeCannotBeMixedWithLiveJourney(string field){ActionMode();owner[field]=true;Refused();}
        [TestCase("runId")][TestCase("taskId")]
        public void ActionModeRequiresExplicitActionAndTask(string field){ActionMode();request[field]="../wrong";WriteRequest();Refused();}
        [Test] public void ActionModeCannotUseCompletedSaveRequest(){ActionMode();request["version"]=1;WriteRequest();Refused();}
        [Test] public void ActionModeCannotChangeBetweenProcesses(){ActionMode();sourceOwner["actionInterruption"]=false;sourceOwner["firstLearner"]=true;sourceOwner["liveContinuity"]=true;Refused();}
        [Test] public void ActionModeRefusesExtraRequestFields(){ActionMode();request["appleId"]=new string('6',32);WriteRequest();Refused();}
        [Test] public void ActionModeMustOptIntoNativeRestart(){ActionMode();sourceOwner["nativeRestart"]=false;Assert.Throws<InvalidDataException>(()=>QuestRoomProbeRestart.Resolve(directory,sourceOwner));}
        [Test] public void ActionFlagCannotBeCoerced(){owner["actionInterruption"]="false";Refused();sourceOwner["actionInterruption"]="false";Assert.Throws<InvalidDataException>(()=>QuestRoomProbeRestart.Resolve(directory,sourceOwner));}

        [Test] public void OrdinaryAdmissionKeepsItsOwnWorkspaceAndDoesNotRequireRestartEvidence()
        {
            var ordinary=new JObject{["version"]=1,["id"]=sourceId,["nativeRestart"]=false};
            File.Delete(Path.Combine(directory,"restart.json"));
            var result=QuestRoomProbeRestart.Resolve(directory,ordinary);
            Assert.That(result.WorkspaceDirectory,Is.EqualTo(workspace));Assert.That(result.RestartOf,Is.Empty);
            ordinary.Remove("nativeRestart");Assert.That(QuestRoomProbeRestart.Resolve(directory,ordinary).WorkspaceDirectory,Is.EqualTo(workspace));
        }
        [TestCase("firstLearner")][TestCase("liveContinuity")][TestCase("nativeRestart")]
        public void BothOwnersMustExplicitlyEnableRestartMode(string field)
        {
            owner[field]=false;Refused();owner[field]=true;sourceOwner[field]=false;Refused();
        }
        [Test] public void RootRestartModeCannotBeEnabledForOtherJourneys()
        {
            sourceOwner["liveContinuity"]=false;
            Assert.Throws<InvalidDataException>(()=>QuestRoomProbeRestart.Resolve(directory,sourceOwner));
        }
        [TestCase("ready.json")][TestCase("state.json")][TestCase("terminal.json")][TestCase("workspace")][TestCase("learner-resume.json")]
        public void ChildCannotReuseOutputsOrImportACopiedWorkspace(string name)
        {
            Directory.CreateDirectory(Path.Combine(child,name));Refused();
        }
        [Test] public void AClosedSourceCannotBeRelabeledAsAnAbruptInterruption()
        {
            File.WriteAllText(Path.Combine(directory,"terminal.json"),"{}");Refused();
            File.Delete(Path.Combine(directory,"terminal.json"));receipt["source"]["terminalExisted"]=true;Refused();
        }
        [Test] public void AChildOutsideTheFixedDirectRestartDirectoryIsRefused()
        {
            var other=Path.Combine(directory,"other");Directory.CreateDirectory(other);receipt["resumed"]["directory"]=other;SaveEvidence();
            Assert.Throws<InvalidDataException>(()=>QuestRoomProbeRestart.Resolve(other,owner));
        }
        [TestCase("workspace")][TestCase("resumedDirectory")][TestCase("readyWorkspace")]
        public void EvidenceCannotRedirectTheOwnedPaths(string field)
        {
            var other=Path.Combine(directory,"other");Directory.CreateDirectory(other);
            if(field=="workspace")receipt["workspace"]=other;else if(field=="resumedDirectory")receipt["resumed"]["directory"]=other;else ready["workspaceDirectory"]=other;
            Refused();
        }
        [Test] public void MissingWorkspaceIsNotRecreatedByAdmission()
        {
            Directory.Delete(workspace);Refused();Assert.That(Directory.Exists(workspace),Is.False);
        }
        [TestCase("sourceOwner")][TestCase("receipt")][TestCase("ready")][TestCase("sourceProcess")][TestCase("resumed")][TestCase("request")]
        public void EveryReceiptMustBelongToTheSameOwnedRestart(string field)
        {
            var wrong=Guid.NewGuid().ToString("N");
            if(field=="sourceOwner")sourceOwner["id"]=wrong;else if(field=="receipt")receipt["id"]=wrong;else if(field=="ready")ready["id"]=wrong;else if(field=="sourceProcess")receipt["source"]["id"]=wrong;else if(field=="resumed")receipt["resumed"]["id"]=wrong;else{request["id"]=wrong;WriteRequest();}
            Refused();
        }
        [Test] public void RestartCannotReuseTheOriginalOwnerId()
        {
            owner["id"]=sourceId;receipt["resumed"]["id"]=sourceId;Refused();
        }
        [TestCase("processId")][TestCase("startedAt")]
        public void SourceProcessMustMatchTheOriginalReadyAttestation(string field)
        {
            receipt["source"][field]=(long)receipt["source"][field]+1;Refused();
        }
        [Test] public void SourceMustHaveAnObservedExitAfterItsStart()
        {
            receipt["source"]["exitedAt"]=999L;Refused();receipt["source"]["exitedAt"]=2000L;
            ((JObject)receipt["source"]).Remove("exitCode");Refused();
        }
        [Test] public void ChangedCheckpointBytesCannotReuseTheirPriorHash()
        {
            File.AppendAllText(Path.Combine(directory,"restart-request.json")," ");Refused();
        }
        [Test] public void OversizedCheckpointIsRejectedEvenWithAMatchingHash()
        {
            File.WriteAllBytes(Path.Combine(directory,"restart-request.json"),new byte[8193]);receipt["requestSha256"]=Hash(File.ReadAllBytes(Path.Combine(directory,"restart-request.json")));Refused();
        }
        [TestCase("room","pending")][TestCase("room","dirty")][TestCase("room","dispatchHeld")]
        [TestCase("behaviours","pending")][TestCase("behaviours","dirty")][TestCase("behaviours","dispatchHeld")]
        public void BothCheckpointStoresMustBeIdleCleanAndUnheld(string store,string field)
        {
            request["checkpoint"][store][field]=true;WriteRequest();Refused();
        }
        [TestCase("phase")][TestCase("error")]
        public void PendingOrFailedCheckpointCannotBePromotedToClean(string field)
        {
            request["checkpoint"]["room"][field]="saving";WriteRequest();Refused();
        }
        [Test] public void CheckpointIdentityAndBooleanTypesAreNotCoerced()
        {
            request["appleId"]=request["treeId"].DeepClone();WriteRequest();Refused();request["appleId"]=new string('5',32);
            request["checkpoint"]["room"]["pending"]="false";WriteRequest();Refused();request["checkpoint"]["room"]["pending"]=false;
            receipt["source"]["processId"]="4321";Refused();
        }
        [TestCase("clientId")][TestCase("nativeSession")][TestCase("appleId")][TestCase("treeId")]
        public void CheckpointRequiresCanonicalGuidIdentities(string field)
        {
            request[field]="arbitrary-text";WriteRequest();Refused();
            request[field]=new string('A',32);WriteRequest();Refused();
        }
        [Test] public void CheckpointRejectsUnknownStores()
        {
            request["checkpoint"]["extra"]=Clean();WriteRequest();Refused();
        }
        [Test] public void DuplicateJsonKeysCannotChangeCheckpointMeaning()
        {
            string path=Path.Combine(directory,"restart-request.json");
            File.WriteAllText(path,File.ReadAllText(path).Replace("\"version\":1","\"version\":1,\"version\":1"));
            receipt["requestSha256"]=Hash(File.ReadAllBytes(path));SaveEvidence();
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(()=>QuestRoomProbeRestart.Resolve(child,owner));
        }
        [Test] public void RootCannotBeAnotherRestartChildAndResumedPidIsNotNeededBeforeLaunch()
        {
            Assert.DoesNotThrow(()=>QuestRoomProbeRestart.Resolve(child,owner));
            sourceOwner["restartOf"]=Guid.NewGuid().ToString("N");Refused();
        }
    }
}
