// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {public sealed partial class RoomRulesTests {
 [UnityTest]public IEnumerator MaterialPrecisionSharedConfigureFactAndDiskKeepExactQuantities(){
  Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Measured material",new Vector3(6,2,4),1,Color.white,out var id,out var error),error);
  var executor=new RoomAgentExecutor(editor);long bits=BitConverter.DoubleToInt64Bits(.49999987822957331);
  foreach(int offset in new[]{-1,0,1}){
   double amount=BitConverter.Int64BitsToDouble(bits+offset),capacity=BitConverter.Int64BitsToDouble(bits+offset+1);
   var definition=new JObject{["capacityLitres"]=capacity,["amountLitres"]=amount,["material"]="Snow",["color"]=new JObject{["r"]=1,["g"]=1,["b"]=1,["a"]=1}};
   var call=new JObject{["id"]="object.material.edit",["version"]=1,["arguments"]=new JObject{["operation"]="configure",["target"]=id,["revision"]=editor.ObjectRevision(id),["definition"]=definition}};
   yield return ContainerAction(executor,call);
   var live=editor.Read(id).materialStores.Single();Assert.AreEqual(amount,live.amountLitres);Assert.AreEqual(capacity,live.capacityLitres);
   var fact=SculptFact("object.material",new JObject{["target"]=id});Assert.IsTrue((bool)fact["configured"]);
   Assert.AreEqual(amount,(double)fact["definition"]["amountLitres"]);Assert.AreEqual(capacity,(double)fact["definition"]["capacityLitres"]);
   var saved=new RoomStorage(directory).Load(out error);Assert.IsNotNull(saved,error);var persisted=saved.objects.Single(o=>o.id==id).materialStores.Single();
   Assert.AreEqual(amount,persisted.amountLitres);Assert.AreEqual(capacity,persisted.capacityLitres);
   int revision=editor.ObjectRevision(id);definition.Remove("amountLitres");definition.Add("amountLitres",BitConverter.Int64BitsToDouble(bits+offset+2));
   call["arguments"]["revision"]=revision;
   Assert.IsFalse(BehaviourCatalog.TryCall((string)call["id"],1,(JObject)call["arguments"],out _,out _));
   // The normal fixture helper requires valid inputs; send this intentionally
   // invalid call through the actual request boundary without that precondition.
   Assert.IsFalse(executor.Execute(ObjectEditRequest(call),out var rejection,out var created));Assert.IsNotEmpty(rejection);Assert.IsEmpty(created);
   Assert.AreEqual(revision,editor.ObjectRevision(id));Assert.AreEqual(amount,editor.Read(id).materialStores.Single().amountLitres);Assert.AreEqual(capacity,editor.Read(id).materialStores.Single().capacityLitres);
  }
 }
}}
