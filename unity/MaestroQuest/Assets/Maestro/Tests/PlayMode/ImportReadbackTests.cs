// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        [UnityTest] public IEnumerator SingleModelWith32MotionsKeepsExactOutcomeAndReadablePagedFacts()
        {
            ImportRuntime();yield return ChooseModel(DistinctMotionExport());yield return ImportAction(ImportAccept("motions"));
            var execution=authorActions.Observe().DeepClone();var exact=execution["selected"]["output"]["motionIds"].Values<string>().ToArray();Assert.That(exact.Length,Is.EqualTo(32));
            var summary=ImportFact();Assert.That((int)summary["accepted"]["motionCount"],Is.EqualTo(32));Assert.That(summary["accepted"]["motionIds"],Is.Null,"The summary reports a total; a separate bounded fact returns IDs");
            var ids=new System.Collections.Generic.List<string>();var pages=new JArray();
            for(int offset=0;offset<32;offset+=8){var input=new JObject {["requestId"]=imports.SelectionRequestId,["motionOffset"]=offset};var page=Fact("model.import.motions",input);Assert.That((int)page["motionCount"],Is.EqualTo(32));Assert.That((int)page["motionOffset"],Is.EqualTo(offset));Assert.That(((JArray)page["motionIds"]).Count,Is.EqualTo(8));ids.AddRange(page["motionIds"].Values<string>());pages.Add(new JObject {["arguments"]=input,["value"]=page});}
            Assert.That(ids,Is.EqualTo(exact));Assert.That(ids.All(id=>editor.Motions.Inspect(id)!=null),Is.True);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            string capture=Environment.GetEnvironmentVariable("MAESTRO_IMPORT_READBACK");if(!string.IsNullOrEmpty(capture)){Directory.CreateDirectory(capture);File.WriteAllText(Path.Combine(capture,"readback.json"),new JObject {["execution"]=execution,["summary"]=summary,["pages"]=pages}.ToString());}
            string old=imports.SelectionRequestId;yield return ImportAction(new JObject {["operation"]="select"});
            Assert.That(BehaviourCatalog.TryRead("model.import.motions",1,new JObject {["requestId"]=old,["motionOffset"]=0},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            yield return ImportAction(new JObject {["operation"]="cancel",["requestId"]=imports.SelectionRequestId});Assert.That(editor.Motions.List().Length,Is.EqualTo(32));
        }
        [UnityTest] public IEnumerator EscapedBatchFilenameDoesNotHideAnyOfIts32MotionIds()
        {
            BatchRuntime();var source=new SelectedMotionSource(DistinctMotionExport()){DisplayName=new string('\u2028',100)};Assert.That(batches.Prepare(source),Is.True);
            yield return BatchAction(BatchArgs("start"));yield return BatchPhase("completed");var ids=new System.Collections.Generic.List<string>();
            for(int offset=0;offset<32;offset+=8){var value=Fact("motion.import.batch.file",new JObject {["requestId"]=batches.SessionId,["index"]=0,["motionOffset"]=offset});Assert.That((int)value["motionCount"],Is.EqualTo(32));Assert.That((string)value["name"],Is.Not.Empty);ids.AddRange(value["motionIds"].Values<string>());}
            Assert.That(ids.Distinct().Count(),Is.EqualTo(32));Assert.That(ids.All(id=>editor.Motions.Inspect(id)!=null),Is.True);batches.Clear();
        }
        [UnityTest] public IEnumerator EscapedFailureAndFilenameStayReadableTogetherWithoutGrantingMotionIds()
        {
            BatchRuntime();var source=new SelectedMotionSource(new byte[8]){DisplayName=new string('\u2028',100),ReadError=new ModelImportException(new string('\u2029',128))};Assert.That(batches.Prepare(source),Is.True);
            var category=BatchArgs("category");category["category"]="A"+new string('\u2028',30)+"B";yield return BatchAction(category);yield return BatchAction(BatchArgs("start"));yield return BatchPhase("partial");Assert.That((string)BatchFact()["category"],Is.EqualTo((string)category["category"]),"Collection tags remain exact; only display names/errors are shortened");var file=BatchFile(0);Assert.That((string)file["state"],Is.EqualTo("failed"));Assert.That((string)file["error"],Is.Not.Empty);Assert.That((int)file["motionCount"],Is.Zero);Assert.That(((JArray)file["motionIds"]).Count,Is.Zero);batches.Clear();
        }
        [UnityTest] public IEnumerator EscapedModelPreviewNameRemainsReadableBeforeAndAfterMotionImport()
        {
            ImportRuntime();yield return ImportAction(new JObject {["operation"]="select"});modelChoice.Choose(DistinctMotionExport(),new string('\u2028',100));yield return ImportPhase("preview");
            Assert.That((int)ImportFact()["preview"]["clips"],Is.EqualTo(32));yield return ImportAction(ImportAccept("motions"));Assert.That((int)ImportFact()["accepted"]["motionCount"],Is.EqualTo(32));
            Assert.That(editor.Motions.List().Length,Is.EqualTo(32));Assert.That(avatar.IsImportedClipPlaying,Is.False);
        }
    }
}
