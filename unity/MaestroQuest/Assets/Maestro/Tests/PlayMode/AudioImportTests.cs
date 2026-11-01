// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        sealed class SoundPicker:IModelPicker,IDisposable {
            public string CacheRoot {get;}=Path.Combine(Path.GetTempPath(),"MaestroSoundChoice-"+Guid.NewGuid().ToString("N"));
            public bool ReadyToStart=>Current==null;internal JObject Current;internal string Id;internal int Released;
            public void Start(string id){Id=id;Current=new JObject {["id"]=id,["phase"]="selecting"};}
            public JObject Read(string id)=>Current?.DeepClone() as JObject;
            public void Release(string id){if(Id==id){Released++;Current=null;}}
            internal void Pick(byte[] bytes,string name="My water sound.wav"){string parent=Path.Combine(CacheRoot,Guid.NewGuid().ToString("D"));Directory.CreateDirectory(parent);string path=Path.Combine(parent,Guid.NewGuid().ToString("D"));File.WriteAllBytes(path,bytes);Current=new JObject {["id"]=Id,["phase"]="selected",["path"]=path,["name"]=name};}
            public void Dispose(){if(Directory.Exists(CacheRoot))Directory.Delete(CacheRoot,true);}
        }
        static byte[] SoundFile(){using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);int frames=24000;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*2);
            for(int i=0;i<frames;i++)writer.Write((short)(Math.Sin(i*2*Math.PI*440/24000)*5000));return memory.ToArray();
        }
        [UnityTest] public IEnumerator ExplicitSoundAcceptRepairsMetadataAndKeepsTheExistingSourcePlayable(){
            var asset=AudioLibrary.Inspect("Original bell",SoundFile());var save=editor.Sounds.SaveAsync(asset);yield return PhysicalCreationTestWait.Until(()=>save.IsCompleted,"Initial sound save did not drain");Assert.That(save.Exception,Is.Null);
            string info=Path.Combine(editor.SaveDirectory,"audio",asset.Hash+".txt");File.WriteAllBytes(info,new byte[1025]);var original=new RoomAudioDefinition {id=Guid.NewGuid().ToString("N"),name="Existing bell source",kind="clip",assetHash=asset.Hash,seconds=1};
            Assert.That(editor.EditAudio(original.id,0,original,out var error),Is.True,error);WorldEmitter("book",original.id);var audio=WorldAudio.For(editor);Assert.That(audio.Begin("book","sound",out var failed,out error),Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>failed.Closed,"The corrupt metadata must fail existing sound preparation");Assert.That(failed.Complete,Is.False);Assert.That(failed.Error,Is.Not.Empty);Assert.That(failed.Output,Is.Null);
            yield return PhysicalCreationTestWait.Until(()=>(int)AudioReservations.ObserveBudget()["sources"]==0,"Failed sound preparation did not release its reservation");
            using var picker=new SoundPicker();var importer=editor.GetComponent<AudioImportWorkshop>();importer.SetPickerForTests(picker);string request=importer.Select();picker.Pick(asset.Content,"Recovered bell.wav");
            yield return PhysicalCreationTestWait.Until(()=>(string)importer.Observe()["phase"]=="preview","Reimported sound preview did not finish");Assert.That((string)importer.Observe()["preview"]["assetHash"],Is.EqualTo(asset.Hash));JObject receipt=null;
            yield return AcceptReimportThroughSharedCatalog("audio.import",request,asset.Hash,value=>receipt=value);Assert.That((string)importer.Observe()["phase"],Is.EqualTo("completed"));Assert.That((string)receipt["sourceId"],Is.Not.EqualTo(original.id));Assert.That((int)AudioReservations.ObserveBudget()["owners"],Is.Zero,"Acceptance must not start playback");
            Assert.That(editor.ReadAudio(original.id).assetHash,Is.EqualTo(asset.Hash));Assert.That(editor.ReadAudio(original.id).name,Is.EqualTo(original.name));Assert.That(editor.ReadAudioEmitter("book","sound").source,Is.EqualTo(original.id));
            Assert.That(audio.Begin("book","sound",out var played,out error),Is.True,error);yield return PhysicalCreationTestWait.Until(()=>played.Output!=null||played.Closed,"The repaired original sound did not prepare");Assert.That(played.Output,Is.Not.Null,played.Error);Assert.That(played.Samples,Has.Length.EqualTo(24000));Assert.That(played.Samples.Max(),Is.GreaterThan(4000));audio.Cancel(played);
            var read=editor.Sounds.ReadAsync(asset.Hash);yield return PhysicalCreationTestWait.Until(()=>read.IsCompleted,"Repaired sound read did not drain");Assert.That(read.Exception,Is.Null);Assert.That(read.Result.Name,Is.EqualTo("Recovered bell.wav"));Assert.That(read.Result.Content,Is.EqualTo(asset.Content));
            var refresh=importer.Refresh(CancellationToken.None);yield return PhysicalCreationTestWait.Until(()=>refresh.IsCompleted,"Repaired sound refresh did not drain");Assert.That(refresh.Exception,Is.Null);Assert.That((bool)refresh.Result["library"]["ready"],Is.True);Assert.That((string)refresh.Result["library"]["entries"][0]["id"],Is.EqualTo(asset.Hash));
        }
        [UnityTest] public IEnumerator CompressedAudioFilePickerUsesCanonicalPreviewAndReleasesSelection(){
            foreach(string file in new[]{"stereo-vbr.mp3","stereo.ogg"}){
                using var picker=new SoundPicker();var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SetPickerForTests(picker);string id=workshop.Select();
                var bytes=File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/CompressedAudio",file+".bytes"));picker.Pick(bytes);double until=Time.realtimeSinceStartupAsDouble+10;
                while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                Assert.AreEqual("preview",(string)workshop.Observe()["phase"],workshop.Observe().ToString());Assert.AreEqual(1,picker.Released);string hash=(string)workshop.Observe()["preview"]["assetHash"];Assert.AreNotEqual(ModelLibrary.Hash(bytes),hash);
                var accepting=workshop.Accept(id,hash,CancellationToken.None);while(!accepting.IsCompleted)yield return null;Assert.IsFalse(accepting.IsFaulted,accepting.Exception?.ToString());Assert.IsNull(editor.GetComponent<WorldAudio>());
                editor.Undo();Assert.IsNull(editor.ReadAudio((string)accepting.Result["sourceId"]));var saved=editor.Sounds.ReadAsync(hash);while(!saved.IsCompleted)yield return null;Assert.IsFalse(saved.IsFaulted);Assert.AreEqual(24000,saved.Result.Inspection.Rate);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
            }
        }
        [UnityTest] public IEnumerator SoundPickerAcceptsExactPreviewOnceWithoutPlayingAndUndoKeepsFile(){
            using var picker=new SoundPicker();var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SetPickerForTests(picker);string id=workshop.Select();
            Assert.IsFalse(workshop.CanStart(true,out _));picker.Pick(SoundFile());double deadline=Time.realtimeSinceStartupAsDouble+5;
            while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var preview=workshop.Observe();Assert.AreEqual("preview",(string)preview["phase"],preview.ToString());Assert.IsEmpty(editor.AudioSources());Assert.IsFalse(workshop.CanAccept(id,new string('a',64),out _));
            string hash=(string)preview["preview"]["assetHash"];var accepting=workshop.Accept(id,hash,CancellationToken.None);while(!accepting.IsCompleted)yield return null;Assert.IsFalse(accepting.IsFaulted,accepting.Exception?.ToString());
            string source=(string)accepting.Result["sourceId"];Assert.AreEqual(hash,editor.ReadAudio(source).assetHash);Assert.IsNull(editor.GetComponent<WorldAudio>());Assert.IsFalse(workshop.CanAccept(id,hash,out _));Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
            Assert.IsTrue(BehaviourCatalog.TryRead("audio.source.definition",2,new JObject {["id"]=source},new BehaviourCatalog.FactContext(editor:editor),out var fact));Assert.AreEqual(hash,(string)((JObject)fact.Value)["definition"]["clip"]["assetHash"]);
            editor.Undo();Assert.IsNull(editor.ReadAudio(source));var read=editor.Sounds.ReadAsync(hash);while(!read.IsCompleted)yield return null;Assert.IsFalse(read.IsFaulted);
            var refresh=workshop.Refresh(CancellationToken.None);while(!refresh.IsCompleted)yield return null;Assert.IsFalse(refresh.IsFaulted,refresh.Exception?.ToString());Assert.AreEqual(hash,(string)workshop.Library(0)["entries"][0]["id"]);
        }
        [UnityTest] public IEnumerator SoundPickerWaitsForFocusAndCancellationNeverAccepts(){
            using var picker=new SoundPicker();var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SetPickerForTests(picker);string id=workshop.Select();
            workshop.SendMessage("OnApplicationFocus",false);picker.Pick(SoundFile());yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual("selecting",(string)workshop.Observe()["phase"]);Assert.IsFalse(workshop.CanAccept(id,new string('a',64),out _));
            workshop.SendMessage("OnApplicationFocus",true);workshop.Cancel(id);Assert.AreEqual("cancelled",(string)workshop.Observe()["phase"]);Assert.IsEmpty(editor.AudioSources());Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
            string next=workshop.Select();Assert.IsFalse(workshop.CanCancel(id,out _));picker.Pick(new byte[50]);double until=Time.realtimeSinceStartupAsDouble+5;
            while((string)workshop.Observe()["phase"]!="failed"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.AreEqual("failed",(string)workshop.Observe()["phase"]);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));Assert.IsEmpty(editor.AudioSources());
        }
        [UnityTest] public IEnumerator SoundPickerDisableCancelsSelectionAndCanReopenWithoutAcceptingStalePreview(){
            using var picker=new SoundPicker();var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SetPickerForTests(picker);string id=workshop.Select();
            picker.Pick(SoundFile());double deadline=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.AreEqual("preview",(string)workshop.Observe()["phase"]);string hash=(string)workshop.Observe()["preview"]["assetHash"];
            workshop.enabled=false;Assert.IsTrue(editor.WriteGate.CanFreeze(out _));Assert.IsFalse(workshop.CanAccept(id,hash,out _));Assert.IsEmpty(editor.AudioSources());
            workshop.enabled=true;Assert.IsTrue(workshop.CanStart(true,out var error),error);Assert.IsFalse(workshop.CanAccept(id,hash,out _));string next=workshop.Select();Assert.AreNotEqual(id,next);workshop.Cancel(next);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
        }
        [UnityTest] public IEnumerator SoundLibraryRefreshReturnsDiscoveredFilesAndWorstCaseMetadataFitsTheSharedValueBudget(){
            for(int n=0;n<3;n++){var bytes=SoundFile();bytes[44]=(byte)n;var saving=editor.Sounds.SaveAsync(AudioLibrary.Inspect(new string('"',80),bytes));while(!saving.IsCompleted)yield return null;Assert.IsFalse(saving.IsFaulted);}
            var workshop=editor.GetComponent<AudioImportWorkshop>();var refresh=workshop.Refresh(CancellationToken.None);while(!refresh.IsCompleted)yield return null;Assert.IsFalse(refresh.IsFaulted,refresh.Exception?.ToString());
            var receipt=refresh.Result;Assert.IsTrue((bool)receipt["library"]["ready"]);Assert.AreEqual(3,(int)receipt["library"]["total"]);Assert.AreEqual(1,receipt["library"]["entries"].Count());Assert.AreEqual(1,(int)receipt["library"]["next"]);
            Assert.IsTrue(CapabilityArguments.Validate(receipt,new AudioImportCapability().OutputSchema,out var error),error);Assert.DoesNotThrow(()=>ProgramValue.Literal(receipt));
            Assert.IsTrue(BehaviourCatalog.TryRead("audio.library",1,new JObject{["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out var fact));Assert.AreEqual(2,((JObject)fact.Value)["entries"].Count());Assert.AreEqual(2,(int)((JObject)fact.Value)["next"]);
            Assert.IsTrue(BehaviourCatalog.TryRead("audio.library",1,new JObject{["offset"]=2},new BehaviourCatalog.FactContext(editor:editor),out fact));Assert.AreEqual(1,((JObject)fact.Value)["entries"].Count());Assert.AreEqual(-1,(int)((JObject)fact.Value)["next"]);
        }
        [UnityTest] public IEnumerator ImportedSoundUsesNativePlaybackAndRejectsChangedFile(){
            var asset=AudioLibrary.Inspect("Water",SoundFile());var saving=editor.Sounds.SaveAsync(asset);while(!saving.IsCompleted)yield return null;Assert.IsFalse(saving.IsFaulted);
            var d=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N"),name="Water",kind="clip",assetHash=asset.Hash,seconds=1};Assert.IsTrue(editor.EditAudio(d.id,0,d,out var error),error);WorldEmitter("book",d.id);
            var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var voice,out error),error);double until=Time.realtimeSinceStartupAsDouble+5;while(!voice.Output&&!voice.Closed&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.IsNotNull(voice.Output,voice.Error);Assert.AreEqual(24000,voice.Samples.Length);Assert.That(voice.Samples.Max(),Is.GreaterThan(4000));audio.Cancel(voice);Assert.IsTrue(voice.Closed);yield return null;
            string path=Path.Combine(editor.SaveDirectory,"audio",asset.Hash+".wav");var changed=File.ReadAllBytes(path);changed[44]^=1;File.WriteAllBytes(path,changed);
            Assert.IsTrue(audio.Begin("book","sound",out voice,out error),error);until=Time.realtimeSinceStartupAsDouble+5;while(!voice.Closed&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.IsTrue(voice.Closed);Assert.IsFalse(voice.Complete);Assert.IsNotNull(voice.Error);Assert.IsNull(voice.Output);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));audio.enabled=false;
        }
    }
}
