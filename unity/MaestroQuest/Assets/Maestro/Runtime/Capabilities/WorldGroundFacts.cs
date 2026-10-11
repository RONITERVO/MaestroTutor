// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class WorldGroundFacts
    {
        internal const string Feature="terrainTraversal.v1";
        static JObject PointSchema()=>Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.ground",
            ProgramDataType.Read(JObject.Parse("{\"record\":{\"worldId\":\"text\",\"regionId\":\"text\",\"found\":\"boolean\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"normal\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"limits\":{\"record\":{\"slopeDegrees\":\"number\",\"stepMetres\":\"number\"}},\"reason\":\"text\"}}")),
            "Accepted ground near a point",
            "Inspect accepted authored terrain in this world and region. Input position and output position are authored coordinates in metres; normal uses authored axes. radius is the supported horizontal footprint in metres. The query samples the highest accepted surface within half a metre above/below the point, then eight footprint edges, using the same 25-degree slope and 10-centimetre step policy as walking. Scanned physical surfaces, ordinary props, held/dynamic ground and sculpt previews are not authored ground. found=false means position/normal are unavailable placeholders. This reads collision geometry, not a NavMesh approximation. It does not test body/head clearance, reserve a route, move the viewer, start physics or change terrain. Read world.viewpoint and invoke world.viewpoint.set for an explicit relocation; clearance and current ground are rechecked at execution. Current worlds have one region and 25-metre coordinate bounds.",
            Object(new JObject{["position"]=PointSchema(),["radius"]=Number(.05,.5)}),
            new JObject{["position"]=new JObject{["x"]=0,["y"]=0,["z"]=0},["radius"]=.2},
            (context,args)=>context.Editor?.ObserveGround(new Vector3((float)args["position"]["x"],(float)args["position"]["y"],(float)args["position"]["z"]),(float)args["radius"]) is JObject value?ProgramValue.Literal(value):null,
            features:new[]{Feature});
    }
}
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        readonly RoomGroundQuery groundObservation=new();
        internal JObject ObserveGround(Vector3 position,float radius)
        {
            var identity=WorldIdentity;var frame=Frame;
            if(identity==null||!frame.Valid||Mathf.Abs(frame.MetresPerUnit-1)>.00001f||Vector3.Dot(transform.up,Vector3.up)<.99999f||
                !RoomViewpoint.ValidPosition(position)||!float.IsFinite(radius)||radius<.05f||radius>.5f)return null;
            Physics.SyncTransforms();groundObservation.Capture(transform);
            bool found=groundObservation.Support(frame.PointToWorld(position),radius,.5f,.5f,out var support,out var normal,out var error);
            if(found&&!RoomViewpoint.ValidPosition(frame.PointToRoom(support))){found=false;error="The accepted surface is outside supported world coordinates";}
            return new JObject{["worldId"]=identity.worldId,["regionId"]=identity.regionId,["found"]=found,
                ["position"]=WorkspaceViewpoint.Point(found?frame.PointToRoom(support):Vector3.zero),
                ["normal"]=WorkspaceViewpoint.Point(found?frame.DirectionToRoom(normal):Vector3.zero),
                ["limits"]=new JObject{["slopeDegrees"]=RoomGroundQuery.MaximumSlope,["stepMetres"]=RoomGroundQuery.MaximumStep},["reason"]=error??""};
        }
    }
}
