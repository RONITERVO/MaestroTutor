// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
  static JObject AudioOffers(byte[] bytes,string hash=null)=>new(){["offerSet"]=new string('a',32),["offers"]=new JArray(new JObject{["assetHash"]=hash??ModelLibrary.Hash(bytes),["name"]="Generated water sound",["messageId"]="chat-sound",["origin"]="generated",["bytes"]=bytes.Length})};
  static void AudioSend(AudioImportWorkshop workshop,JObject wire,byte[] bytes){
   while(workshop.Chat.Request()!=null){var request=workshop.Chat.Request();int offset=(int)request["offset"];var chunk=(JObject)wire.DeepClone();chunk["chunk"]=new JObject{["requestId"]=(string)request["requestId"],["assetHash"]=(string)request["assetHash"],["offset"]=offset,["data"]=Convert.ToBase64String(bytes.Skip(offset).Take(ChatAudioTransfer.ChunkBytes).ToArray())};workshop.SyncChat("chat",chunk);}
  }
  [UnityTest] public IEnumerator ChatAudioUsesSamePreviewAcceptanceUndoAndPrivateLibrary(){
   var bytes=SoundFile();var wire=AudioOffers(bytes);var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SyncChat("chat",wire);
   Assert.IsTrue(BehaviourCatalog.TryRead("audio.chat.library",1,new JObject{["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out var offers));Assert.AreEqual("generated",(string)((JObject)offers.Value)["entries"][0]["origin"]);
   string hash=ModelLibrary.Hash(bytes),id=workshop.SelectChat((string)wire["offerSet"],hash);Assert.IsEmpty(editor.AudioSources());AudioSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
   Assert.AreEqual("preview",(string)workshop.Observe()["phase"],workshop.Observe().ToString());
   var accept=workshop.Accept(id,hash,CancellationToken.None);while(!accept.IsCompleted)yield return null;Assert.IsFalse(accept.IsFaulted,accept.Exception?.ToString());
   string appearance=(string)accept.Result["sourceId"];Assert.AreEqual(hash,editor.ReadAudio(appearance).assetHash);Assert.IsNull(editor.GetComponent<WorldAudio>());
   editor.Undo();Assert.IsNull(editor.ReadAudio(appearance));var saved=editor.Sounds.ReadAsync(hash);while(!saved.IsCompleted)yield return null;Assert.IsFalse(saved.IsFaulted);CollectionAssert.AreEqual(bytes,saved.Result.Content);
   Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
  }
  [UnityTest] public IEnumerator ChatAudioPreviewIsRevokedWhenSourceOrFocusChanges(){
   var bytes=SoundFile();var wire=AudioOffers(bytes);var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SyncChat("chat",wire);
   string hash=ModelLibrary.Hash(bytes),id=workshop.SelectChat((string)wire["offerSet"],hash);AudioSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<until)yield return null;Assert.AreEqual("preview",(string)workshop.Observe()["phase"]);
   workshop.SendMessage("OnApplicationFocus",false);Assert.IsFalse(workshop.CanAccept(id,hash,out _));Assert.AreEqual("cancelled",(string)workshop.Observe()["phase"]);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));workshop.SendMessage("OnApplicationFocus",true);
   workshop.SyncChat("chat",wire);id=workshop.SelectChat((string)wire["offerSet"],hash);wire["offerSet"]=new string('c',32);workshop.SyncChat("chat",wire);Assert.IsNull(workshop.Chat.Request());Assert.IsFalse(workshop.CanAccept(id,hash,out _));Assert.IsEmpty(editor.AudioSources());
  }
  [UnityTest] public IEnumerator ChatAudioHashMismatchNeverPublishesASource(){
   var bytes=SoundFile();var wire=AudioOffers(bytes,new string('d',64));var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SyncChat("chat",wire);workshop.SelectChat((string)wire["offerSet"],new string('d',64));AudioSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="failed"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
   Assert.AreEqual("failed",(string)workshop.Observe()["phase"]);Assert.IsEmpty(editor.AudioSources());Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
  }
  [UnityTest] public IEnumerator ChatAudioRejectsBadWaveAndSceneReplacementDrainsSelection(){
   var bytes=new byte[80];var wire=AudioOffers(bytes);var workshop=editor.GetComponent<AudioImportWorkshop>();workshop.SyncChat("chat",wire);workshop.SelectChat((string)wire["offerSet"],ModelLibrary.Hash(bytes));AudioSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="failed"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
   Assert.AreEqual("failed",(string)workshop.Observe()["phase"]);Assert.IsEmpty(editor.AudioSources());Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
   bytes=SoundFile();wire=AudioOffers(bytes);wire["offerSet"]=new string('c',32);workshop.SyncChat("chat",wire);string id=workshop.SelectChat((string)wire["offerSet"],ModelLibrary.Hash(bytes));Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
   workshop.SyncChat("replacement",null);Assert.IsNull(workshop.Chat.Request());Assert.IsFalse(workshop.CanAccept(id,ModelLibrary.Hash(bytes),out _));Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
  }
 }
}
