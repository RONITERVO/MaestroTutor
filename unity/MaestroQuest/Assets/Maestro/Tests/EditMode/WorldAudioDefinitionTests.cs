// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WorldAudioDefinitionTests
    {
        static readonly string Sound=new string('b',32),Piece=new string('a',32);
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,audioSources=new[]{new RoomAudioDefinition {id=Sound,name="Greeting"}},objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData {id=Piece,kind=RoomObjectKind.Block,audioEmitters=new[]{new RoomAudioEmitter {source=Sound}}}}};
        [Test] public void ClipSchemaAndFixedFactsPreserveOnlyTheChosenSource(){
            var d=new RoomAudioDefinition{id=Sound,kind="clip",assetHash=new string('c',64),seconds=.5f};Assert.IsTrue(d.Validate(out var error),error);
            Assert.Throws<ArgumentException>(()=>AudioTone.Render(d));
            var value=AudioSchema.Encode(d);Assert.IsNull(value["tone"]);Assert.IsTrue(CapabilityArguments.Validate(value,AudioSchema.Source(),out error),error);
            var decoded=AudioSchema.ReadSource(value);decoded.id=Sound;Assert.AreEqual(JsonUtility.ToJson(d),JsonUtility.ToJson(decoded));
            var fact=AudioSchema.ObserveSource(d);Assert.IsNotNull(fact["tone"]);Assert.AreEqual(d.assetHash,(string)fact["clip"]["assetHash"]);
            value["clip"]["assetHash"]="https://example.invalid/sound.wav";Assert.IsFalse(CapabilityArguments.Validate(value,AudioSchema.Source(),out _));
            d.kind="tone";Assert.IsFalse(d.Validate(out _));d.kind="clip";d.frequency=880;Assert.IsFalse(d.Validate(out _));
        }
        [Test] public void AudioEditsUndoAndForkKeepStableSourcesAndMonotonicRevisions()
        {
            var journal=new RoomJournal(Room());int first=journal.AudioRevision(Sound);var fork=journal.Fork();var edited=fork.ReadAudio(Sound);edited.name="Other greeting";
            Assert.IsTrue(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,audioEdits:new AudioDefinitionEdits {Replacements=new[]{edited}}),error);
            edited.name="External mutation";Assert.AreEqual("Greeting",journal.ReadAudio(Sound).name);Assert.AreEqual("Other greeting",fork.ReadAudio(Sound).name);
            Assert.IsTrue(journal.ApplySnapshot(fork.Snapshot(),out error),error);Assert.Greater(journal.AudioRevision(Sound),first);
            int accepted=journal.AudioRevision(Sound);Assert.IsTrue(journal.Undo());Assert.AreEqual("Greeting",journal.ReadAudio(Sound).name);Assert.Greater(journal.AudioRevision(Sound),accepted);
            Assert.IsTrue(journal.Redo());Assert.AreEqual("Other greeting",journal.ReadAudio(Sound).name);
            var detached=journal.Snapshot();detached.objects[2].audioEmitters=new[]{new RoomAudioEmitter {source=Sound,gain=.9f}};Assert.AreEqual(.25f,journal.Read(Piece).audioEmitters[0].gain);
        }
        [Test] public void RemovingAReferencedSourceIsAtomicAndUndoRestoresItsEmitter()
        {
            var journal=new RoomJournal(Room());var edits=new AudioDefinitionEdits {Removals=new[]{Sound}};
            Assert.IsFalse(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,audioEdits:edits));Assert.IsFalse(journal.CanUndo);Assert.IsNotNull(journal.ReadAudio(Sound));
            var owner=journal.Read(Piece);owner.audioEmitters=Array.Empty<RoomAudioEmitter>();Assert.IsTrue(journal.Apply(new[]{owner},Array.Empty<string>(),out var error,audioEdits:edits),error);
            Assert.IsNull(journal.ReadAudio(Sound));Assert.IsTrue(journal.Undo());Assert.AreEqual(Sound,journal.Read(Piece).audioEmitters.Single().source);Assert.IsNotNull(journal.ReadAudio(Sound));
        }
        [Test] public void SourceAndEmitterBoundsRejectMalformedAndFutureValues()
        {
            var room=Room();Assert.IsTrue(room.Validate(out var error),error);room.version=20;Assert.IsFalse(room.Validate(out _));room.version=RoomDocument.CurrentVersion;
            var emitter=room.objects[2].audioEmitters[0];emitter.joint="Head";Assert.IsFalse(room.Validate(out _));emitter.joint="";emitter.part="missing";Assert.IsFalse(room.Validate(out _));emitter.part="";
            emitter.maxDistance=.2f;Assert.IsFalse(room.Validate(out _));emitter.maxDistance=15;emitter.gain=float.NaN;Assert.IsFalse(room.Validate(out _));emitter.gain=.25f;
            room.audioSources[0].kind="url";Assert.IsFalse(room.Validate(out _));room.audioSources[0].kind="tone";room.audioSources[0].seconds=31;Assert.IsFalse(room.Validate(out _));room.audioSources[0].seconds=.25f;
            emitter.version=2;Assert.IsFalse(room.Validate(out _));emitter.version=1;emitter.role="conversation";Assert.IsFalse(room.Validate(out _));
        }
        [Test] public void RoomAudioRoundTripsAndFutureDataIsPreservedReadOnly()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroWorldAudio-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                var room=Room();var storage=new RoomStorage(directory);Assert.IsTrue(storage.Save(room,out var error),error);var loaded=storage.Load(out error);Assert.IsNull(error);Assert.AreEqual(Sound,loaded.objects[2].audioEmitters.Single().source);
                room.audioSources[0].version=2;string text=JsonUtility.ToJson(room);File.WriteAllText(Path.Combine(directory,RoomStorage.FileName),text);storage=new RoomStorage(directory);Assert.IsNull(storage.Load(out _));Assert.IsTrue(storage.ReadOnly);Assert.AreEqual(text,File.ReadAllText(Path.Combine(directory,RoomStorage.FileName)));
            } finally {Directory.Delete(directory,true);}
        }
        [TestCase("sine")][TestCase("triangle")][TestCase("noise")]
        public void ToneRecipesHaveDeterministicBoundedSamplesAndSilentEndpoints(string wave)
        {
            var source=new RoomAudioDefinition {id=Sound,wave=wave,frequency=300,endFrequency=800};var a=AudioTone.Render(source);var b=AudioTone.Render(source.Copy());
            Assert.AreEqual(6000,a.Length);Assert.AreEqual(a,b);Assert.AreEqual(0,a[0]);Assert.AreEqual(0,a[^1]);Assert.IsTrue(a.All(x=>float.IsFinite(x)&&Math.Abs(x)<=.5f));Assert.IsTrue(a.Any(x=>Math.Abs(x)>.1f));
            source.attack=.2f;source.release=.2f;Assert.Throws<ArgumentException>(()=>AudioTone.Render(source));
        }
        [Test] public void AudioSemanticSchemasRejectInvalidEnvelopeDistanceAndDualAnchors()
        {
            var sound=new AudioSourceCapability();var args=sound.Example;args["definition"]["name"]="Happy puppy";Assert.IsTrue(CapabilityArguments.Validate(args,sound.InputSchema,out var error),error);
            args["definition"]["tone"]["attack"]=.2;args["definition"]["tone"]["release"]=.2;Assert.IsFalse(CapabilityArguments.Validate(args,sound.InputSchema,out _));
            var emitter=new AudioEmitterCapability();args=emitter.Example;args["definition"]["distance"]["maximum"]=.1;Assert.IsFalse(CapabilityArguments.Validate(args,emitter.InputSchema,out _));args["definition"]["distance"]["maximum"]=15;args["definition"]["part"]="speaker";Assert.IsFalse(CapabilityArguments.Validate(args,emitter.InputSchema,out _));
        }
        [Test] public void AudioExamplesHaveValidSharedSchemasAndMovementDoesNotOwnTheirChannel()
        {
            foreach(var module in new CapabilityModule[]{new AudioSourceCapability(),new AudioEmitterCapability(),new AudioPlayCapability()}) {
                Assert.IsTrue(CapabilityArguments.Validate(module.Example,module.InputSchema,out var error),module.Id+": "+error);
                Assert.IsTrue(module.Validate(module.Example,out error),module.Id+": "+error);
            }
            var play=new AudioPlayCapability();var claim=play.Claims(play.Example).Single();Assert.IsFalse(claim.Conflicts(new BehaviourCatalog.Claim("maestro","wholeTarget")));
            Assert.IsTrue(claim.Conflicts(play.Claims(play.Example).Single()));
        }
    }
}
