// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        sealed class LayoutSource:IRoomLayoutSource
        {
            public string RoomId=Guid.NewGuid().ToString("N");public ScannedSurface[] Entries=Array.Empty<ScannedSurface>();public int Omitted,Reads;public bool Available=true,Throw;
            public bool TryRead(Transform frame,out string roomId,out ScannedSurface[] entries,out int omitted,out string reason){Reads++;if(Throw)throw new IOException("SDK read failed");roomId=RoomId;entries=Entries;omitted=Omitted;reason=Available?null:"No current Meta room is loaded";return Available;}
        }
        static ScannedSurface LayoutEntry(int id)=>new(){Id=id.ToString("x32"),Label=id%2==0?"FLOOR":"WALL_FACE",Position=new Vector3(id*.125f,1.234567f,-.1234567f),Rotation=Quaternion.Euler(13,19,27),Plane=new Rect(-1,-.5f,2,1),Volume=id%2==0?new Bounds(new Vector3(.1f,.2f,-.3f),new Vector3(2,1,.2f)):null};
        IEnumerator LoadedLayout(ScannedRoom scan,SceneSource platform,LayoutSource layoutSource)
        {
            scan.SetLayoutSourceForTests(layoutSource);scan.Load();platform.PermissionResult.SetResult(true);yield return Until(()=>platform.Loads==1);platform.LoadResult.SetResult(true);yield return Until(()=>!scan.Busy);world.SetSurfaces(true,"Synthetic room for scan-layout tests");
        }
        bool ScanFact(string id,JObject args,out JToken token)
        {
            bool read=BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value);token=read?JToken.FromObject(value.Value):null;if(read)Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return read;
        }
        JObject ScanStatus(){Assert.That(ScanFact("room.scan",null,out var result),Is.True);return (JObject)result;}
        static JObject ScanArgs(JObject status,int offset)=>new(){["stateId"]=status["stateId"].DeepClone(),["offset"]=offset};
        static JObject SurfaceArgs(JObject status,string id)=>new(){["stateId"]=status["stateId"].DeepClone(),["id"]=id};
        [UnityTest] public IEnumerator ScanLayoutPagesAndBoundsUseRealCatalogWithoutSetupPhysicsOrSaves()
        {
            var scan=SharedEnvironment(out var platform);var unavailable=ScanStatus();Assert.That((bool)unavailable["available"],Is.False);Assert.That((string)unavailable["stateId"],Is.Empty);
            var source=new LayoutSource{Entries=Enumerable.Range(1,5).Reverse().Select(LayoutEntry).ToArray(),Omitted=1};yield return LoadedLayout(scan,platform,source);
            int revision=editor.ObjectRevision("maestro");var before=JsonUtility.ToJson(editor.Snapshot());var status=ScanStatus();Assert.That((bool)status["available"],Is.True);
            Assert.That(ScanFact("room.scan.surfaces",ScanArgs(status,0),out var page),Is.True);Assert.That(page.Count(),Is.EqualTo(4));Assert.That((string)page[0]["id"],Is.EqualTo(LayoutEntry(1).Id));
            Assert.That(ScanFact("room.scan.surfaces",ScanArgs(status,4),out var last),Is.True);Assert.That(last.Count(),Is.EqualTo(1));Assert.That(ScanFact("room.scan.surfaces",ScanArgs(status,5),out var empty),Is.True);Assert.That(empty.Count(),Is.Zero);Assert.That(ScanFact("room.scan.surfaces",ScanArgs(status,6),out _),Is.False);
            Assert.That(ScanFact("room.scan.surface",SurfaceArgs(status,LayoutEntry(2).Id),out var detail),Is.True);Assert.That((bool)detail["volume"]["present"],Is.True);Assert.That(ScanFact("room.scan.surface",SurfaceArgs(status,LayoutEntry(1).Id),out var planeOnly),Is.True);Assert.That((bool)planeOnly["volume"]["present"],Is.False);Assert.That((float)planeOnly["volume"]["size"]["x"],Is.Zero);
            Assert.That(source.Reads,Is.EqualTo(1),"One bounded source capture within a Unity frame");Assert.That(platform.Permissions,Is.EqualTo(1));Assert.That(platform.Loads,Is.EqualTo(1));Assert.That(platform.Scans,Is.Zero);Assert.That(world.Running,Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            string output=Environment.GetEnvironmentVariable("MAESTRO_SCAN_LAYOUT");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"scan-layout.json"),new JObject{["unavailable"]=unavailable,["status"]=status,["page"]=page,["last"]=last,["empty"]=empty,["detail"]=detail,["planeOnly"]=planeOnly,["boundary"]="Synthetic SDK source; real native scan coordinator and catalog. No headset or provider."}.ToString());}
        }
        [UnityTest] public IEnumerator ScanLayoutRejectsChangedGeometryIdentityAndSetupWithoutReturningOldBounds()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{LayoutEntry(1)}};yield return LoadedLayout(scan,platform,source);var status=ScanStatus();
            source.Entries[0].Position=Vector3.one;Assert.That(ScanFact("room.scan.surface",SurfaceArgs(status,LayoutEntry(1).Id),out var sameFrame),Is.True);Assert.That((float)sameFrame["pose"]["position"]["x"],Is.EqualTo(.125f),"Captured values are detached from provider objects");yield return null;
            Assert.That(ScanFact("room.scan.surface",SurfaceArgs(status,LayoutEntry(1).Id),out _),Is.False);var changed=ScanStatus();Assert.That((string)changed["stateId"],Is.Not.EqualTo((string)status["stateId"]));
            source.RoomId=Guid.NewGuid().ToString("N");yield return null;Assert.That(ScanFact("room.scan.surfaces",ScanArgs(changed,0),out _),Is.False);changed=ScanStatus();
            scan.ToggleSurfaces();Assert.That(ScanFact("room.scan.surfaces",ScanArgs(changed,0),out _),Is.False);var fresh=ScanStatus();Assert.That(ScanFact("room.scan.surface",SurfaceArgs(fresh,new string('f',32)),out _),Is.False);
        }
        [UnityTest] public IEnumerator ScanLayoutUnavailableDuringLifecycleVirtualModeAndWorkspaceHold()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{LayoutEntry(1)}};yield return LoadedLayout(scan,platform,source);var initial=ScanStatus();
            scan.SendMessage("OnApplicationFocus",false);Assert.That((bool)ScanStatus()["available"],Is.False);Assert.That(ScanFact("room.scan.surfaces",ScanArgs(initial,0),out _),Is.False);scan.SendMessage("OnApplicationFocus",true);world.SetSurfaces(true,"Synthetic restored alignment");Assert.That((bool)ScanStatus()["available"],Is.True);
            scan.SetVirtualView(true);Assert.That((bool)ScanStatus()["available"],Is.False);scan.SetVirtualView(false);world.SetSurfaces(true,"Synthetic restored alignment");
            using(editor.RuntimeGate.Hold("Workspace review")){Assert.That((bool)ScanStatus()["available"],Is.False);}world.SetSurfaces(true,"Synthetic restored alignment");
            scan.enabled=false;Assert.That((bool)ScanStatus()["available"],Is.False);scan.enabled=true;world.SetSurfaces(true,"Synthetic restored alignment");Assert.That((bool)ScanStatus()["available"],Is.True);
        }
        [UnityTest] public IEnumerator ScanLayoutBoundsAndIdentityLimitsFailClosedAndEmptyIsTyped()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource();yield return LoadedLayout(scan,platform,source);
            var empty=ScanStatus();Assert.That((bool)empty["available"],Is.True);Assert.That(ScanFact("room.scan.surfaces",ScanArgs(empty,0),out var page),Is.True);Assert.That(page.Count(),Is.Zero);
            var worst=LayoutEntry(2);worst.Label=new string('W',96);worst.Position=Vector3.one*999.12345f;source.Entries=Enumerable.Range(1,128).Select(LayoutEntry).ToArray();source.Entries[1]=worst;yield return null;var full=ScanStatus();Assert.That((int)full["count"],Is.EqualTo(128));Assert.That(ScanFact("room.scan.surface",SurfaceArgs(full,worst.Id),out _),Is.True);
            foreach(string failure in new[]{"too-many","duplicate","nan","bad-room","bad-id","bad-size","missing","throw"}){
                source.RoomId=Guid.NewGuid().ToString("N");source.Available=true;source.Throw=false;source.Entries=new[]{LayoutEntry(1)};
                switch(failure){case "too-many":source.Entries=Enumerable.Range(1,129).Select(LayoutEntry).ToArray();break;case "duplicate":source.Entries=new[]{LayoutEntry(1),LayoutEntry(1)};break;case "nan":source.Entries[0].Position=new Vector3(float.NaN,0,0);break;case "bad-room":source.RoomId="bad";break;case "bad-id":source.Entries[0].Id=new string('0',32);break;case "bad-size":source.Entries[0].Plane=new Rect(0,0,-1,2);break;case "missing":source.Available=false;break;case "throw":source.Throw=true;break;}
                yield return null;var failed=ScanStatus();Assert.That((bool)failed["available"],Is.False,failure);Assert.That((string)failed["stateId"],Is.Empty);Assert.That((int)failed["count"],Is.Zero);Assert.That(ScanFact("room.scan.surfaces",ScanArgs(full,0),out _),Is.False);
            }
        }
        [UnityTest] public IEnumerator ScanPoseConversionKeepsAnchorLocalBoundsAndRoomLocalPose()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource();yield return LoadedLayout(scan,platform,source);
            var frame=new GameObject("Query frame");frame.transform.SetParent(root.transform,false);frame.transform.SetPositionAndRotation(new Vector3(3,2,-7),Quaternion.Euler(0,33,0));
            var anchor=new GameObject("SDK plane frame");anchor.transform.SetParent(frame.transform,false);anchor.transform.localPosition=new Vector3(1,2,3);anchor.transform.localRotation=Quaternion.Euler(90,0,0);
            var entry=ScannedSurface.FromFrame(LayoutEntry(1).Id,"FLOOR",anchor.transform,frame.transform,new Rect(-1,-2,2,4),null);source.Entries=new[]{entry};var status=scan.ObserveLayout(frame.transform);Assert.That((bool)status["available"],Is.True);Assert.That(Vector3.Distance(entry.Position,new Vector3(1,2,3)),Is.LessThan(.0001f));Assert.That(Quaternion.Angle(entry.Rotation,anchor.transform.localRotation),Is.LessThan(.01f));Assert.That(entry.Plane.Value.width,Is.EqualTo(2));
            frame.transform.localScale=Vector3.one*2;Assert.That((bool)scan.ObserveLayout(frame.transform)["available"],Is.False);Assert.That(scan.LayoutSurface(frame.transform,(string)status["stateId"],entry.Id),Is.Null);
        }
    }
}
