// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed class AudioReferenceRetentionTests
    {
        const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string Id="cccccccccccccccccccccccccccccccc";
        GameObject root;RoomEditor editor;string directory;
        [UnitySetUp]public IEnumerator Before(){
            directory=Path.Combine(Path.GetTempPath(),"MaestroAudioReferences-"+Guid.NewGuid().ToString("N"));
            root=new GameObject("Audio reference test");root.AddComponent<XRInteractionManager>();var room=root.AddComponent<RoomInteraction>();var physics=root.AddComponent<RoomPhysicsWorld>();
            RoomItem Included(string name){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root.transform,false);var item=obj.AddComponent<RoomItem>();item.Configure(new[]{obj.GetComponent<Collider>()});room.Register(item);return item;}
            var book=Included("book");var avatar=Included("maestro");editor=root.AddComponent<RoomEditor>();
            Assert.That(editor.UsesAudio(Hash),Is.True,"An uninitialized editor cannot prove absence");Assert.That(editor.HistoricalAudio(Hash),Is.True);
            editor.Initialize(room,book,avatar,directory,physics);yield return null;
        }
        [UnityTearDown]public IEnumerator After(){
            if(root)UnityEngine.Object.Destroy(root);yield return null;
            var completion=editor.LifecycleCompletion;float deadline=Time.realtimeSinceStartup+10;
            while(!completion.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(completion.IsCompleted,Is.True,"Audio reference fixture lifecycle save did not drain");Assert.That(completion.IsFaulted,Is.False);
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void Assign(string hash){Assert.That(editor.EditAudio(Id,editor.AudioRevision(Id),hash==null?null:new RoomAudioDefinition{id=Id,name="Unbound clip",kind="clip",assetHash=hash,seconds=.5f},out var error),Is.True,error);}
        IEnumerator Saved(){float deadline=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(editor.TemporarySavePending,Is.False,"Temporary audio snapshot did not finish");Assert.That(editor.TemporarySaveError,Is.Null);}
        [UnityTest]public IEnumerator CurrentSavedAndHistoricalAudioRootsSurviveTemporaryDiscard(){
            Assert.That(editor.UsesAudio(Hash),Is.False);Assign(Hash);Assert.That(editor.UsesAudio(Hash),Is.True);Assert.That(editor.Snapshot().objects.All(o=>(o.audioEmitters?.Length??0)==0),Is.True,"Unbound sources are still references");
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return Saved();Assign(Other);
            Assert.That(editor.ReadAudio(Id).assetHash,Is.EqualTo(Other));Assert.That(editor.UsesAudio(Hash),Is.True,"Saved temporary base remains a root");Assert.That(editor.UsesAudio(Other),Is.True);
            Assert.That(editor.HistoricalAudio(Hash),Is.True);Assign(null);Assert.That(editor.UsesAudio(Other),Is.False);Assert.That(editor.HistoricalAudio(Other),Is.True);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.ReadAudio(Id).assetHash,Is.EqualTo(Hash));Assert.That(editor.UsesAudio(Hash),Is.True);
            Assert.That(editor.UsesAudio(Other),Is.False);Assert.That(editor.HistoricalAudio(Other),Is.False,"Discard drops only the temporary fork's history");yield return null;
        }
        [UnityTest]public IEnumerator DispatchedTemporarySnapshotRetainsAudioRemovedFromLiveFork(){
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return Saved();Assign(Hash);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);Assign(null);
            // No frame has polled completion yet: neither current nor saved
            // journal contains this clip. Only the detached in-flight snapshot does.
            Assert.That(editor.TemporarySavePending,Is.True);Assert.That(editor.ReadAudio(Id),Is.Null);Assert.That(editor.RecoverySavedSnapshot().audioSources,Is.Empty);
            Assert.That(editor.UsesAudio(Hash),Is.True,"Dispatched snapshot must retain its original clip through completion");
            yield return Saved();Assert.That(editor.ReadAudio(Id),Is.Null);Assert.That(editor.RecoverySavedSnapshot().audioSources.Single().assetHash,Is.EqualTo(Hash));Assert.That(editor.UsesAudio(Hash),Is.True);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.ReadAudio(Id).assetHash,Is.EqualTo(Hash));editor.Undo();
            Assert.That(editor.ReadAudio(Id),Is.Null);Assert.That(editor.UsesAudio(Hash),Is.False);Assert.That(editor.HistoricalAudio(Hash),Is.True,"Saved snapshot Undo retains its AfterAudio in Redo");
            editor.Redo();Assert.That(editor.ReadAudio(Id).assetHash,Is.EqualTo(Hash));yield return null;
        }
    }
}
