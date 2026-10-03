// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class ConstructionMember {public string target,slot;public int revision;}
    public sealed partial class RoomEditor
    {
        internal bool CaptureConstruction(ConstructionMember[] members,out CreationBatch batch,out string error) {
            batch=null;error="Choose 1–16 distinct created objects and slots";
            if(members==null||members.Length<1||members.Length>16||members.Any(m=>m==null)||members.Select(m=>m.target).Distinct().Count()!=members.Length||members.Select(m=>m.slot).Distinct().Count()!=members.Length)return false;
            if(RuntimeGate.Held||PhysicsWorld&&PhysicsWorld.Running){error="Pause room physics before capturing a construction";return false;}
            var drawing=GetComponent<SpatialDrawing>();if(drawing&&(drawing.IsDrawing||drawing.HasUnsavedStroke)){error="Finish or discard the drawing draft before capturing";return false;}
            var selected=members.Select(m=>m.target).ToHashSet();var objects=new RoomObjectData[members.Length];
            for(int i=0;i<members.Length;i++){
                var member=members[i];if(!CanEditObject(member.target,true,out error))return false;
                if(ObjectRevision(member.target)!=member.revision){error="An object changed; inspect its current revision before capturing";return false;}
                if(GetComponent<AnimationWorkshop>()?.ControlsTarget(member.target)==true){error="Finish animation authoring before capturing";return false;}
                var item=Find(member.target);if(!item.isActiveAndEnabled||item.GetComponent<RigidRoomItem>()?.GeometryReady==false){error="Wait for every construction member to be ready";return false;}
                objects[i]=Pose(Read(member.target),item.transform);
                if(objects[i].recipe?.playing==true){error="Pause recipe animations before capturing their authored geometry";return false;}
            }
            if(Snapshot().objects.Any(o=>(o.hinges??Array.Empty<RoomHinge>()).Any(h=>selected.Contains(o.id)!=selected.Contains(h.connected)))){
                error="Include both ends of every connected hinge; capture cannot silently drop an external connection";return false;
            }
            var slots=members.ToDictionary(m=>m.target,m=>m.slot);var anchor=objects[0];var inverse=Quaternion.Inverse(anchor.rotation);
            var links=objects.SelectMany(o=>(o.hinges??Array.Empty<RoomHinge>()).Select(h=>new BlueprintHinge {
                owner=slots[o.id],connected=slots[h.connected],definition=new HingeSettings {enabled=h.enabled,ownerFrame=h.ownerFrame.Copy(),connectedFrame=h.connectedFrame.Copy(),limits=h.limits.Copy(),drive=h.drive.Copy()}})).ToArray();
            var pieces=objects.Select(o=>new CreationPiece {slot=slots[o.id],name=o.name??"",position=inverse*(o.position-anchor.position),rotation=(inverse*o.rotation).normalized,scale=o.scale,
                source=new CreationSource {kind="prototype",prototype=CreationPrototype.Capture(o)}}).ToArray();
            batch=new CreationBatch {position=anchor.position,rotation=anchor.rotation,scale=1,blueprint=new CreationBlueprint {version=links.Length==0?1:2,pieces=pieces,hinges=links}};
            if(!batch.Prepare(out var candidate,out error)||!CreationPrototype.ValidateObjects(candidate,out error)){batch=null;return false;}
            error=null;return true;
        }
    }
    internal static class ConstructionModule
    {
        internal static JObject Definition(CreationBatch batch,string name) {
            var arguments=JObject.Parse(JsonUtility.ToJson(batch));
            var pieces=(JArray)arguments["blueprint"]["pieces"];
            for(int i=0;i<pieces.Count;i++)pieces[i]["source"]=new JObject {["kind"]="prototype",["prototype"]=CreationPrototypeSchema.Encode(batch.blueprint.pieces[i].source.prototype)};
            // Independent blueprints need no connected feature. A present empty hinges field would require it.
            if(batch.blueprint.version==1)((JObject)arguments["blueprint"]).Remove("hinges");
            JObject VectorType(bool q=false){var fields=new JObject {["x"]="number",["y"]="number",["z"]="number"};if(q)fields["w"]="number";return new JObject {["record"]=fields};}
            var list=new JObject {["list"]="text"};
            var main=new JObject {["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=new JArray()};
            var create=new JObject {["name"]="create",["returns"]=list,["parameters"]=new JArray(
                new JObject {["name"]="position",["type"]=VectorType()},new JObject {["name"]="rotation",["type"]=VectorType(true)},new JObject {["name"]="scale",["type"]="number"}),
                ["locals"]=new JArray(new JObject {["name"]="pieces",["type"]=list.DeepClone(),["initial"]=new JArray()}),
                ["body"]=new JArray(new JObject {["id"]="build",["op"]="invoke",["capability"]="object.batch.create",["version"]=1,["arguments"]=arguments,
                    ["bindings"]=new JObject {["position"]=new JObject {["var"]="position"},["rotation"]=new JObject {["var"]="rotation"},["scale"]=new JObject {["var"]="scale"}},["results"]=new JObject {["objectIds"]="pieces"}},
                    new JObject {["id"]="created",["op"]="return",["value"]=new JObject {["var"]="pieces"}})};
            var program=new JObject {["version"]=3,["dataVersion"]=1,["entry"]="main",["resources"]=new JArray(),["state"]=new JArray(),["events"]=new JArray(),["functions"]=new JArray(main,create)};
            return ProgramModuleLibrary.Definition(program.ToString(Newtonsoft.Json.Formatting.None),name,new[]{"create"});
        }
    }
}
