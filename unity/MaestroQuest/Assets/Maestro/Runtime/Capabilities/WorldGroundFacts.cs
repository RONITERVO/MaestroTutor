// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
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
        static ProgramDataType ResultType()=>OutputType(Object(new JObject{
                ["worldId"]=Text(null),["regionId"]=Text(null),["found"]=new JObject{["type"]="boolean"},
                ["availability"]=Choice("ready","pending","unavailable"),
                ["dependencyCount"]=Number(0,64,true),
                ["dependencies"]=List(Object(new JObject{["target"]=Text(null,32),["state"]=Choice("pending","unavailable"),
                    ["boundsKnown"]=new JObject{["type"]="boolean"}}),0,4),
                ["position"]=PointSchema(),["normal"]=Vector(),
                ["limits"]=Object(new JObject{["slopeDegrees"]=Number(0,90),["stepMetres"]=Number(0,1)}),["reason"]=Text(null,256)}));
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.ground",ResultType(),
            "Accepted ground near a point",
            "Inspect accepted authored terrain in this world and region. Input position and output position are authored coordinates in metres; normal uses authored axes. radius is the supported horizontal footprint in metres. The query samples the highest accepted surface within half a metre above/below the point, then eight footprint edges, using the same 25-degree slope and 10-centimetre step policy as walking. Scanned physical surfaces, ordinary props, held/dynamic ground and sculpt previews are not authored ground. availability is ready only when no potentially relevant saved ground is missing; pending means its native activation/loading is queued or running, and unavailable means at least one dependency is inactive, dormant or failed. dependencyCount counts all relevant incomplete objects; dependencies shows the first four stable object IDs in order, not the entire set; boundsKnown=false conservatively includes unknown extents. Known distant ground is excluded using a conservative envelope of all support rays. Incomplete availability forces found=false even if a lower resident surface exists. With ready and found=false, no resident surface satisfies the query policy. found=false means position/normal are unavailable placeholders. This reads collision geometry, not a NavMesh approximation. This passive read does not activate dependencies, test body/head clearance, reserve a route, move the viewer, start physics or change terrain. Read world.viewpoint and invoke world.viewpoint.set for an explicit relocation; clearance and current ground are rechecked at execution. Current worlds have one region and 25-metre coordinate bounds.",
            Object(new JObject{["position"]=PointSchema(),["radius"]=Number(.05,.5)}),
            new JObject{["position"]=new JObject{["x"]=0,["y"]=0,["z"]=0},["radius"]=.2},
            (context,args)=>context.Editor?.ObserveGround(new Vector3((float)args["position"]["x"],(float)args["position"]["y"],(float)args["position"]["z"]),(float)args["radius"]) is JObject value?ProgramValue.Literal(value,ResultType()):null,
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
            Physics.SyncTransforms();
            var dependencies=GroundDependencies(position,radius,.5f,.5f);
            string availability=dependencies.Count==0?"ready":dependencies.Any(x=>(string)x["state"]=="unavailable")?"unavailable":"pending";
            Vector3 support=default,normal=default;
            string error=availability=="pending"?"Ground is still loading; inspect dependencies and retry after activation":
                availability=="unavailable"?"Ground is unavailable; inspect dependencies before choosing this location":null;
            bool found=false;
            if(availability=="ready"){
                groundObservation.Capture(transform);
                found=groundObservation.Support(frame.PointToWorld(position),radius,.5f,.5f,out support,out normal,out error);
            }else groundObservation.Clear();
            if(found&&!RoomViewpoint.ValidPosition(frame.PointToRoom(support))){found=false;error="The accepted surface is outside supported world coordinates";}
            return new JObject{["worldId"]=identity.worldId,["regionId"]=identity.regionId,["found"]=found,["availability"]=availability,["dependencyCount"]=dependencies.Count,["dependencies"]=new JArray(dependencies.Take(4)),
                ["position"]=WorkspaceViewpoint.Point(found?frame.PointToRoom(support):Vector3.zero),
                ["normal"]=WorkspaceViewpoint.Point(found?frame.DirectionToRoom(normal):Vector3.zero),
                ["limits"]=new JObject{["slopeDegrees"]=RoomGroundQuery.MaximumSlope,["stepMetres"]=RoomGroundQuery.MaximumStep},["reason"]=error??""};
        }
    }
}
