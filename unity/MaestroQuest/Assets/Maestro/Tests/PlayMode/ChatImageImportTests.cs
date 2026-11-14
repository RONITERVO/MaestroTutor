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
  static JObject ChatOffers(byte[] bytes,string hash=null)=>new(){["offerSet"]=new string('a',32),["offers"]=new JArray(new JObject{["imageHash"]=hash??ModelLibrary.Hash(bytes),["name"]="Generated tiles",["messageId"]="chat-image",["origin"]="generated",["bytes"]=bytes.Length})};
  static void ChatSend(ImageImportWorkshop workshop,JObject wire,byte[] bytes){var request=workshop.Chat.Request();wire=(JObject)wire.DeepClone();wire["chunk"]=new JObject{["requestId"]=(string)request["requestId"],["imageHash"]=(string)request["imageHash"],["offset"]=0,["data"]=Convert.ToBase64String(bytes)};workshop.SyncChat("chat",wire);}
  [UnityTest] public IEnumerator ChatImageUsesSamePreviewAcceptanceUndoAndPrivateLibrary(){
   var bytes=ImageFiles.Png();var wire=ChatOffers(bytes);var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SyncChat("chat",wire);
   Assert.IsTrue(BehaviourCatalog.TryRead("image.chat.library",1,new JObject{["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out var offers));Assert.AreEqual("generated",(string)((JObject)offers.Value)["entries"][0]["origin"]);
   string hash=ModelLibrary.Hash(bytes),id=workshop.SelectChat((string)wire["offerSet"],hash);Assert.IsEmpty(editor.Appearances());ChatSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
   Assert.AreEqual("preview",(string)workshop.Observe()["phase"],workshop.Observe().ToString());
   var accept=workshop.Accept(id,hash,CancellationToken.None);while(!accept.IsCompleted)yield return null;Assert.IsFalse(accept.IsFaulted,accept.Exception?.ToString());
   string appearance=(string)accept.Result["appearanceId"];Assert.AreEqual(hash,editor.ReadAppearance(appearance).style.imageHash);Assert.IsTrue(editor.Snapshot().objects.All(o=>o.appearanceBindings.Length==0));
   editor.Undo();Assert.IsNull(editor.ReadAppearance(appearance));var saved=editor.Images.ReadAsync(hash);while(!saved.IsCompleted)yield return null;Assert.IsFalse(saved.IsFaulted);CollectionAssert.AreEqual(bytes,saved.Result.Content);
   Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
  }
  [UnityTest] public IEnumerator ChatPreviewIsRevokedWhenSourceOrFocusChanges(){
   var bytes=ImageFiles.Png();var wire=ChatOffers(bytes);var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SyncChat("chat",wire);
   string hash=ModelLibrary.Hash(bytes),id=workshop.SelectChat((string)wire["offerSet"],hash);ChatSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<until)yield return null;Assert.AreEqual("preview",(string)workshop.Observe()["phase"]);
   workshop.SendMessage("OnApplicationFocus",false);Assert.IsFalse(workshop.CanAccept(id,hash,out _));Assert.AreEqual("cancelled",(string)workshop.Observe()["phase"]);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));workshop.SendMessage("OnApplicationFocus",true);
   workshop.SyncChat("chat",wire);id=workshop.SelectChat((string)wire["offerSet"],hash);wire["offerSet"]=new string('c',32);workshop.SyncChat("chat",wire);Assert.IsNull(workshop.Chat.Request());Assert.IsFalse(workshop.CanAccept(id,hash,out _));Assert.IsEmpty(editor.Appearances());
  }
  [UnityTest] public IEnumerator ChatImageHashMismatchNeverPublishesAnAppearance(){
   var bytes=ImageFiles.Png();var wire=ChatOffers(bytes,new string('d',64));var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SyncChat("chat",wire);workshop.SelectChat((string)wire["offerSet"],new string('d',64));ChatSend(workshop,wire,bytes);
   double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="failed"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
   Assert.AreEqual("failed",(string)workshop.Observe()["phase"]);Assert.IsEmpty(editor.Appearances());Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
  }
 }
}
