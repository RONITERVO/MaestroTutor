// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class RoomScanFacts
    {
        internal const string Feature="roomScanLayout.v1";
        static readonly string[] Features={Feature};
        static ScannedRoom Room(BehaviourCatalog.FactContext c)=>c.Editor&&c.Editor.PhysicsWorld?c.Editor.PhysicsWorld.GetComponent<ScannedRoom>():null;
        static ProgramDataType Type(string json)=>ProgramDataType.Read(JObject.Parse(json));
        static JObject Id()=>Text("^[a-f0-9]{32}$",32);
        static readonly ProgramDataType PageType=Type("{\"list\":{\"record\":{\"id\":\"text\",\"label\":\"text\",\"plane\":\"boolean\",\"volume\":\"boolean\"}}}");
        internal static BehaviourCatalog.FactDefinition Status()=>new("room.scan",Type("{\"record\":{\"available\":\"boolean\",\"stateId\":\"text\",\"roomId\":\"text\",\"count\":\"number\",\"omitted\":\"number\",\"reason\":\"text\"}}"),
            "Loaded scanned-room layout","On-demand bounded Meta scan layout in current room coordinates. available=false means counts are placeholders, not an empty physical room. Requires a loaded tracked MR room with colliders, no setup/workspace hold and an unscaled room frame. Read stateId before room.scan.surfaces or room.scan.surface. Identity changes with observed geometry, room frame or setup; snapshots are cached only within one Unity frame. At most 128 surfaces from 256 anchors; larger or malformed scans are unavailable, never silently truncated. omitted counts anchors without plane/volume bounds (including mesh-only geometry). Bounds are not full polygons, openings, current obstacles, free space, physical alignment or persistence guarantees. Reads never scan, request permission, start physics or save. Detailed geometry is queried explicitly, not added to background room observations.",null,null,(c,a)=>{var room=Room(c);return room?ProgramValue.Literal(room.ObserveLayout(c.Editor.transform)):null;},features:Features);
        internal static BehaviourCatalog.FactDefinition Page()=>new("room.scan.surfaces",PageType,
            "Scanned surface identities","Four stable Meta anchor IDs and semantic labels per page, sorted by ID. Use the exact stateId from room.scan; changed/unavailable layouts refuse reads. offset=count returns an empty typed list; beyond count refuses. plane and volume describe available bounds, not proof that a surface is safe, visible or current. No raw mesh or camera image.",Object(new JObject{["stateId"]=Id(),["offset"]=Number(0,ScannedRoom.MaximumLayoutSurfaces,true)}),new JObject{["stateId"]=new string('0',32),["offset"]=0},
            (c,a)=>{var room=Room(c);var page=room?room.LayoutPage(c.Editor.transform,(string)a["stateId"],(int)a["offset"]):null;return page==null?null:ProgramValue.Literal(page,PageType);},features:Features);
        internal static BehaviourCatalog.FactDefinition Surface()=>new("room.scan.surface",Type("{\"record\":{\"id\":\"text\",\"label\":\"text\",\"pose\":{\"record\":{\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"rotation\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}}}},\"plane\":{\"record\":{\"present\":\"boolean\",\"center\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"size\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}}}},\"volume\":{\"record\":{\"present\":\"boolean\",\"center\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"size\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}}}}}}"),
            "Scanned surface pose and bounds","Read one exact anchor using current stateId and ID from room.scan.surfaces. pose is room-local position and quaternion. Plane/volume center and size are anchor-local metres; transform by pose to use room coordinates. Meta planes lie in XY at z=0 and face local +Z, unlike drawing patches' -Z. Absent bounds have present=false and zero placeholders. Rectangles/boxes are bounds only: no polygon, cutouts, global mesh or moving obstacle detection. A new scan may replace UUIDs; never silently attach saved content to a similar-looking surface. No placement, drawing attachment or save occurs.",Object(new JObject{["stateId"]=Id(),["id"]=Id()}),new JObject{["stateId"]=new string('0',32),["id"]=new string('1',32)},
            (c,a)=>{var room=Room(c);var value=room?room.LayoutSurface(c.Editor.transform,(string)a["stateId"],(string)a["id"]):null;return value==null?null:ProgramValue.Literal(value);},features:Features);
    }
}
