// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class DrawingTipTests
    {
        static RoomObjectData Owner()=>new(){id=new string('a',32),kind=RoomObjectKind.Block,drawingTips=new[]{new DrawingTip()}};
        [Test] public void TipCopiesAreIndependentAndRejectMissingAnchorsInvalidInkAndFutureVersions()
        {
            var data=Owner();Assert.That(DrawingTip.ValidateCollection(data,out var error),Is.True,error);var copy=data.Copy();copy.drawingTips[0].enabled=false;Assert.That(data.drawingTips[0].enabled,Is.True);
            foreach(var tip in new[]{new DrawingTip{part="Missing"},new DrawingTip{version=3},new DrawingTip{radius=float.NaN},new DrawingTip{color=new Color(1,1,1,0)},new DrawingTip{position=Vector3.one*10}}){copy.drawingTips=new[]{tip};Assert.That(DrawingTip.ValidateCollection(copy,out _),Is.False);}
            copy.drawingTips=new[]{new DrawingTip(),new DrawingTip()};Assert.That(DrawingTip.ValidateCollection(copy,out _),Is.False);copy=data.Copy();copy.kind=RoomObjectKind.Maestro;Assert.That(DrawingTip.ValidateCollection(copy,out _),Is.False);
        }
        [Test] public void SavedTipsRequireNewRoomFormatAndUnknownTipVersionsPreserveTheOriginalFile()
        {
            var room=new RoomDocument {version=4,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},Owner()}};
            Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;Assert.That(room.Validate(out var error),Is.True,error);
            string directory=Path.Combine(Path.GetTempPath(),"tip-storage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {var store=new RoomStorage(directory);Assert.That(store.Save(room,out error),Is.True,error);var restored=store.Load(out error);Assert.That(restored.objects.Last().drawingTips.Single().radius,Is.EqualTo(.003f));
                room.objects.Last().drawingTips[0].version=3;string text=JsonUtility.ToJson(room),path=Path.Combine(directory,RoomStorage.FileName);File.WriteAllText(path,text);store=new RoomStorage(directory);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(text));
            }finally{Directory.Delete(directory,true);}
        }
        [TestCase("Front\n",false)][TestCase("Front",true)] public void SavedSurfaceIdentityMatchesTheSharedActionBoundary(string id,bool valid)
        {
            var data=Owner();data.surfaces=new[]{new DrawingSurface{id=id}};Assert.That(DrawingSurface.ValidateCollection(data,out _),Is.EqualTo(valid));
            data.surfaces[0].id="Front";data.surfaces[0].strokes=new[]{new SurfaceStroke{id=new string('A',32),points=new[]{Vector3.left*.01f,Vector3.right*.01f}}};Assert.That(DrawingSurface.ValidateCollection(data,out _),Is.False);
        }
        [Test] public void ChalkExpandsARealPartTipAndAllSharedVariantsRoundTrip()
        {
            var chalk=CreationTemplates.All.Single(t=>t.Id=="chalk");Assert.That(chalk.DrawingTips.Single().part,Is.EqualTo("Chalk"));var part=chalk.Recipe.parts.Single();var tip=chalk.DrawingTips.Single();Assert.That(Vector3.Distance(part.rotation*(tip.rotation*Vector3.forward),Vector3.forward),Is.LessThan(.0001f));Assert.That((part.rotation*tip.position).z,Is.EqualTo(.08f).Within(.0001f));tip.enabled=false;Assert.That(chalk.DrawingTips.Single().enabled,Is.True);
            var module=new DrawingTipCapability();var args=module.Example;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out var call,out var error),Is.True,error);Assert.That(call.Resources.Single(),Is.EqualTo((string)args["target"]));
            var program=BehaviourProgram.FromInvocation(new JObject{["id"]=module.Id,["version"]=1,["arguments"]=args});Assert.That(BehaviourProgram.TryParse(program,out _,out error),Is.True,error);
            args["definition"]["rotation"]["w"]=0;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out _,out _),Is.False);
        }
    }
}
