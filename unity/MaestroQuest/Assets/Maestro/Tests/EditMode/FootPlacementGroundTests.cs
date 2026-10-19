// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed partial class FootPlacementMathTests
    {
        [TestCase(0)][TestCase(12)][TestCase(-12)]
        public void BothSolesFitAcceptedSlopesWithoutChangingTheRootOrSourcePose(float degrees)
        {
            var ground=new GameObject("Ground");var box=ground.AddComponent<BoxCollider>();box.size=new(4,.1f,4);
            ground.transform.SetPositionAndRotation(new(0,-.05f,0),Quaternion.Euler(0,0,degrees));
            var rig=Measure();var source=new HumanoidFootRig.Pose(rig);var contacts=new HumanoidGroundPose();var query=new RoomGroundQuery();query.Add(box);
            bool Probe(Vector3 p,float b,out RaycastHit hit)=>query.Sample(p,b,b,out hit);
            var original=hips.localPosition;var upper=leftUpper.localRotation;var rootPosition=root.transform.position;
            try{
                Physics.SyncTransforms();
                for(int i=0;i<20;i++){
                    source.Restore();source.Capture();
                    Assert.That(Probe(new(-.1f,0,0),.25f,out var l),Is.True);Assert.That(Probe(new(.1f,0,0),.25f,out var r),Is.True);
                    Assert.That(contacts.Apply(rig,l,r,0,new(){enabled=true},1f/72,false,Probe,out var error),Is.True,error);
                }
                Probe(rig.Left.Contact,.25f,out var lh);Probe(rig.Right.Contact,.25f,out var rh);
                Assert.That(Vector3.Distance(rig.Left.Contact,lh.point),Is.LessThan(.001));
                Assert.That(Vector3.Distance(rig.Right.Contact,rh.point),Is.LessThan(.001));
                Assert.That(Vector3.Dot(rig.Left.Foot.TransformDirection(rig.Left.Up),lh.normal),Is.GreaterThan(.999));
                Assert.That(root.transform.position,Is.EqualTo(rootPosition));
                source.Restore();Assert.That(hips.localPosition,Is.EqualTo(original));Assert.That(leftUpper.localRotation,Is.EqualTo(upper));
            }finally{Object.DestroyImmediate(ground);}
        }
        [Test]public void CalibrationRejectsUnrelatedOrDegenerateLegChains()
        {
            leftUpper.SetParent(root.transform,true);Assert.That(Measure(),Is.Null);
            leftUpper.SetParent(hips,true);leftLower.localPosition=Vector3.zero;Assert.That(Measure(),Is.Null);
        }
        [TestCase(39)][TestCase(40)]
        public void PreviousRoomFilesLoadWithAuthoredFeetAndPreserveTheirOriginalBytes(int version)
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"maestro-feet-upgrade-"+System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            try{
                var room=new RoomDocument{version=version,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
                var wire=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(room));
                foreach(var item in (Newtonsoft.Json.Linq.JArray)wire["objects"])((Newtonsoft.Json.Linq.JObject)item).Remove("footPlacement");
                string original=wire.ToString(),path=System.IO.Path.Combine(directory,"room.v"+version+".json");System.IO.File.WriteAllText(path,original);
                var storage=new RoomStorage(directory);var loaded=storage.Load(out var error);
                Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.version,Is.EqualTo(RoomDocument.CurrentVersion));
                foreach(var item in loaded.objects)Assert.That(item.footPlacement.Default,Is.True);
                Assert.That(System.IO.File.ReadAllText(path),Is.EqualTo(original));
                Assert.That(storage.Save(loaded,out error),Is.True,error);
                Assert.That(new RoomStorage(directory).Load(out error),Is.Not.Null,error);
                Assert.That(System.IO.File.ReadAllText(path),Is.EqualTo(original));
            }finally{System.IO.Directory.Delete(directory,true);}
        }
        [Test]public void FeetPersistThroughCopyAndPublicPrototypeWithoutChangingExactModelBinding()
        {
            var data=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.ImportedModel,modelHash=new string('b',64),
                footPlacement=new(){enabled=true,modelHash=new string('b',64),leftClearance=.02f,rightClearance=-.01f}};
            var prototype=CreationPrototype.Capture(data);Assert.That(prototype.version,Is.EqualTo(9));
            var wire=CreationPrototypeSchema.Encode(prototype);Assert.That(CapabilityArguments.Validate(wire,CreationPrototypeSchema.Schema(),out var error),Is.True,error);
            var read=CreationPrototype.Read(wire);Assert.That(read.Validate(out error),Is.True,error);
            var copy=read.Instantiate("Character",Vector3.zero,Quaternion.identity,1);
            Assert.That(copy.footPlacement.modelHash,Is.EqualTo(data.modelHash));copy.footPlacement.leftClearance=.06f;
            Assert.That(data.footPlacement.leftClearance,Is.EqualTo(.02f));
            read.version=8;Assert.That(read.Validate(out _),Is.False);
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},data}};
            Assert.That(room.Validate(out error),Is.True,error);room.version=40;Assert.That(room.Validate(out _),Is.False);
            room.version=RoomDocument.CurrentVersion;data.modelHash=new string('c',64);
            Assert.That(room.Validate(out error),Is.True,"A changed model retains a visible unavailable calibration, never silently retargets it");
            data.kind=RoomObjectKind.Block;data.modelHash=null;Assert.That(room.Validate(out _),Is.False);
        }
    }
}
