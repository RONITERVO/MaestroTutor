// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject GroundFact(Vector3 position,float radius=.2f){
            var args=new JObject{["position"]=WorkspaceViewpoint.Point(position),["radius"]=radius};
            Assert.That(BehaviourCatalog.TryRead("world.ground",1,args,new BehaviourCatalog.FactContext(editor:host.Current.Editor),out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        [UnityTest]public IEnumerator AcceptedGroundFactAndWalkedBookmarkSurviveAWorldReload(){
            PhysicalFloor();recoveryHeadTracked=true;room.Viewer.position=new Vector3(4,1.6f,4);
            var terrain=new RoomHeightField{cells=8,width=2,depth=2,maxHeight=.5f,heights=new float[81]};
            for(int z=0;z<=8;z++)for(int x=0;x<=8;x++)terrain.heights[z*9+x]=.05f+.05f*x;
            const string terrainId="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var doc=new RoomDocument{version=RoomDocument.CurrentVersion,viewpoint=new RoomViewpoint{active=true,position=new Vector3(4,.25f,4)},objects=new[]{
                new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(0,1,1)},
                new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},
                new RoomObjectData{id=terrainId,kind=RoomObjectKind.Assembly,name="Small hill",position=new Vector3(4,0,4),physics=ItemPhysics.Fixed,heightFields=new[]{terrain},
                    recipe=new RoomRecipe{parts=new[]{new RecipePart{id="base",position=Vector3.down*.05f,size=new Vector3(2,.1f,2)}}}}}};
            Assert.That(new RoomStorage(Path.Combine(directory,"room")).Save(doc,out var error),Is.True,error);Open();yield return ReadyHost();
            var editor=host.Current.Editor;Assert.That(editor.RuntimeGate.Held,Is.False);Assert.That(editor.TryFlush(out error),Is.True,error);
            string path=Path.Combine(editor.SaveDirectory,RoomStorage.FileName),bytes=File.ReadAllText(path);int revision=editor.Revision;
            var point=new Vector3(4,.25f,4);var supported=GroundFact(point);var missing=GroundFact(Vector3.zero);
            Assert.That((bool)supported["found"],Is.True);Assert.That((float)supported["position"]["y"],Is.EqualTo(.25f).Within(.001));
            Assert.That((float)supported["normal"]["x"],Is.LessThan(-.19f));Assert.That((bool)missing["found"],Is.False);
            Assert.That((bool)GroundFact(new Vector3(4.9f,.43f,4))["found"],Is.False,"The footprint cannot overhang the accepted terrain");
            Assert.That((string)supported["worldId"],Is.EqualTo(doc.world.worldId));Assert.That((string)supported["regionId"],Is.EqualTo(doc.world.regionId));
            var preview=terrain.Copy();for(int i=0;i<81;i++)preview.heights[i]=.49f;editor.Find(terrainId).GetComponent<HeightFieldView>().Preview(preview);
            Assert.That(JToken.DeepEquals(supported,GroundFact(point)),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_WORLD_GROUND");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"ground.json"),new JObject{
                ["arguments"]=new JObject{["position"]=WorkspaceViewpoint.Point(point),["radius"]=.2},["supported"]=supported,["missing"]=missing,
                ["capabilities"]=JObject.Parse(RoomAgentWire.Serialize(agent.Observe()))["capabilities"].DeepClone()}.ToString());}
            Assert.That(view.Enter(),Is.True);var physicalHead=room.Viewer.localToWorldMatrix;
            for(int i=0;i<10;i++)Assert.That(view.Move(Vector3.right*.02f),Is.True,view.MovementError);
            editor.GetComponent<WorkspaceViewpoint>().Capture();var expected=editor.Viewpoint.Copy();
            Assert.That(expected.position.x,Is.EqualTo(4.2f).Within(.001));Assert.That(expected.position.y,Is.EqualTo(.29f).Within(.002));
            Assert.That(room.Viewer.localToWorldMatrix,Is.EqualTo(physicalHead));Assert.That(editor.TryFlush(out error),Is.True,error);
            string savedDirectory=directory;var closing=host;UnityEngine.Object.Destroy(root);yield return null;while(!closing.Retirement.IsCompleted)yield return null;
            BuildShell(savedDirectory);PhysicalFloor();room.Viewer.SetPositionAndRotation(new Vector3(-3,1.7f,6),Quaternion.Euler(0,73,0));recoveryHeadTracked=true;Open();yield return ReadyHost();
            Assert.That(view.ReadViewpoint(out var restored),Is.True);Assert.That(Vector3.Distance(restored.position,expected.position),Is.LessThan(.001));
            var after=GroundFact(point);Assert.That((string)after["worldId"],Is.EqualTo(doc.world.worldId));Assert.That((bool)after["found"],Is.True);Assert.That((float)after["position"]["y"],Is.EqualTo(.25f).Within(.001));
            Assert.That(view.Active,Is.False);Assert.That(physics.Running,Is.False);Assert.That(view.Enter(),Is.True);Assert.That(view.Move(room.transform.TransformDirection(Vector3.left*.02f)),Is.True,view.MovementError);
        }
    }
}
