// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        JObject ArchiveFact(string query="",int offset=0)=>Fact("model.import.archive",new JObject {["requestId"]=imports.SelectionRequestId,["query"]=query,["offset"]=offset});
        JObject ArchiveArgs(string operation,int index=0){var args=new JObject {["operation"]=operation,["requestId"]=imports.SelectionRequestId,["version"]=ArchiveFact()["version"].DeepClone()};if(operation=="member")args["index"]=index;return args;}
        IEnumerator ChooseArchive(params byte[][] models){yield return ImportAction(new JObject {["operation"]="select"});modelChoice.ChooseArchive(models);yield return ImportPhase("archive");}
        [UnityTest] public IEnumerator ZipMembersSharePhysicalPreviewAgentChoicesAndExplicitAvatarAcceptance()
        {
            ImportRuntime();string original=editor.Read("maestro").modelHash;yield return ChooseArchive(ModelFixture.Mixamo(),new byte[32],ModelFixture.Create());var ready=ArchiveFact();
            Assert.That(imports.HasPreview,Is.False);Assert.That(modelChoice.MemberChoices,Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);Assert.That((int)ArchiveFact("model-1")["total"],Is.EqualTo(1));
            var board=new GameObject("ZIP model tray");board.transform.SetParent(root.transform,false);board.AddComponent<ImportTools>().Build(imports,root.GetComponent<RoomInteraction>());
            var controls=board.GetComponentsInChildren<RuleToolAction>();Assert.That(controls.Any(x=>x.AccessibleName=="Use Maestro"),Is.False);
            Assert.That(imports.UseMaestroAsync().Result,Is.False);Assert.That(imports.SaveMotionsAsync().Result,Is.False);
            var stale=ArchiveArgs("member",0);controls.Single(x=>x.AccessibleName=="Next file").Command();Assert.That(authorActions.Execute(ImportRequest(stale),out _),Is.False);Assert.That(modelChoice.MemberChoices,Is.Zero);
            controls.Single(x=>x.AccessibleName=="Preview").Command();yield return ImportPhase("archive");Assert.That((string)ImportFact()["error"],Is.Not.Empty);Assert.That(imports.HasArchive,Is.True);Assert.That(modelChoice.Releases,Is.Zero);
            var action=ImportRequest(ArchiveArgs("member",0));Assert.That(authorActions.Execute(action,out var error),Is.True,error);yield return ModelFinished();var receipt=authorActions.Observe().DeepClone();yield return ImportPhase("preview");var preview=ImportFact();var browsing=ArchiveFact();
            Assert.That(board.GetComponentsInChildren<RuleToolAction>().Any(x=>x.AccessibleName=="Use Maestro"),Is.True);
            Assert.That(imports.HasPreview,Is.True);Assert.That(modelChoice.MemberChoices,Is.EqualTo(2));Assert.That(editor.Read("maestro").modelHash,Is.EqualTo(original));Assert.That(avatar.IsImportedClipPlaying,Is.False);
            Assert.That(authorActions.Execute(action,out error),Is.True,error);Assert.That(modelChoice.MemberChoices,Is.EqualTo(2));
            string capture=Environment.GetEnvironmentVariable("MAESTRO_ARCHIVE_IMPORT");if(!string.IsNullOrEmpty(capture)){Directory.CreateDirectory(capture);File.WriteAllText(Path.Combine(capture,"archive.json"),new JObject {["ready"]=ready,["receipt"]=receipt,["preview"]=preview,["archive"]=browsing}.ToString());}
            yield return ImportAction(ImportAccept("maestro"));yield return ImportPhase("completed");Assert.That(editor.Read("maestro").modelHash,Is.EqualTo((string)preview["preview"]["modelHash"]));Assert.That(imports.HasArchive,Is.False);Assert.That(modelChoice.Releases,Is.EqualTo(1));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            editor.Undo();yield return null;Assert.That(editor.Read("maestro").modelHash,Is.EqualTo(original));
        }
        [UnityTest] public IEnumerator ReturningToZipListInvalidatesOldChoicesAndReplacesOnlyThePreview()
        {
            ImportRuntime();yield return ChooseArchive(ModelFixture.Mixamo(),ModelFixture.Create());yield return ImportAction(ArchiveArgs("member",0));yield return ImportPhase("preview");var stale=ArchiveArgs("member",1);
            yield return ImportAction(ArchiveArgs("files"));Assert.That(imports.HasPreview,Is.False);Assert.That(imports.HasArchive,Is.True);Assert.That(modelChoice.Releases,Is.Zero);Assert.That(authorActions.Execute(ImportRequest(stale),out _),Is.False);
            imports.NextArchiveMember();imports.PreviewArchiveMember();yield return ImportPhase("preview");Assert.That((string)ImportFact()["preview"]["name"],Is.EqualTo("model-1.glb"));
            imports.Cancel();Assert.That(imports.HasArchive,Is.False);Assert.That(imports.HasPreview,Is.False);Assert.That(modelChoice.Releases,Is.EqualTo(1));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
        [UnityTest] public IEnumerator ZipPagesStayBoundedAtMaximumCountAndPauseOrCancelCannotChooseAnotherModel()
        {
            ImportRuntime();yield return ImportAction(new JObject {["operation"]="select"});modelChoice.ChooseArchive(Enumerable.Repeat(new byte[32],1024).ToArray());foreach(var entry in (JArray)modelChoice.Result["members"])entry["name"]=new string('\u2028',120);yield return ImportPhase("archive");
            // Querying pages is pure: no member bytes, preview or cursor changes.
            var last=ArchiveFact("",1021);Assert.That(((JArray)last["entries"]).Count,Is.EqualTo(3));Assert.That((int)last["entries"][2]["index"],Is.EqualTo(1023));Assert.That(last.ToString(Newtonsoft.Json.Formatting.None).Length,Is.LessThan(1024));Assert.That(modelChoice.MemberChoices,Is.Zero);
            var args=ArchiveArgs("member",1023);imports.SendMessage("OnApplicationPause",true);Assert.That(authorActions.Execute(ImportRequest(args),out _),Is.False);imports.SendMessage("OnApplicationPause",false);
            modelChoice.HoldMember=true;yield return ImportAction(args);Assert.That((string)ImportFact()["phase"],Is.EqualTo("copying"));imports.Cancel();Assert.That(modelChoice.Releases,Is.EqualTo(1));Assert.That(imports.HasArchive,Is.False);
            Assert.That(authorActions.Execute(ImportRequest(args),out _),Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
    }
}
