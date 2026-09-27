// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class ObjectCapabilityData
    {
        public static Vector3 Position(JObject arguments)=>new((float)arguments["x"],(float)arguments["y"],(float)arguments["z"]);
        public static Color Color(JObject arguments)=>new((float)arguments["red"],(float)arguments["green"],(float)arguments["blue"],1);
        public static RoomRecipe Recipe(JObject arguments)=>JsonUtility.FromJson<RoomRecipe>(arguments["recipe"].ToString());
        public static JObject RobotExample() {var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;return JObject.Parse(JsonUtility.ToJson(recipe));}
    }
    internal sealed class WaitCapability : CapabilityModule
    {
        public override string Id=>"time.wait";
        public override string Label=>"Wait";
        public override string Duration=>"timed";
        public override JObject InputSchema=>Object(new JObject {["seconds"]=Number(.1,30)});
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {error=null;return true;}
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=new WaitOperation((float)arguments["seconds"]);error=null;return true;
        }
        sealed class WaitOperation : CapabilityOperation {
            readonly float seconds;public WaitOperation(float seconds) {this.seconds=seconds;}
            public override float Seconds=>seconds;
        }
    }
    internal sealed class CreatePrimitiveCapability : CapabilityModule
    {
        public override string Id=>"object.create.primitive";
        public override string Label=>"Create shape";
        public override string Description=>"Create a ball, block or cylinder at x/y/z in room metres (within 25 m of the origin). Scale is a multiplier (1 is the usual tray shape); RGB is 0–1. Returns objectId after saving. The ball is bouncy; other shapes are solid, mass 0.5 kg. Each creation is one Undo edit. Stop leaves created objects in the room.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"room.capacity","storage.writable"};
        public override JObject InputSchema=>Object(new JObject {["shape"]=Choice("ball","block","cylinder"),["name"]=Text("^.{0,80}$",80),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25),["scale"]=Number(.1,4),["red"]=Number(0,1),["green"]=Number(0,1),["blue"]=Number(0,1)});
        public override JObject OutputSchema=>Object(new JObject {["objectId"]=Resource(Text("^[a-f0-9]{32}$",32))});
        public override JObject Example=>new JObject {["name"]="Ball",["x"]=.3f,["y"]=1.3f,["z"]=.65f,["scale"]=1 ,["shape"]="ball",["red"]=.2f,["green"]=.6f,["blue"]=.9f};
        public override bool Validate(JObject arguments,out string error) {
            error="Position must be within 25 metres of the room origin";if(ObjectCapabilityData.Position(arguments).sqrMagnitude>625)return false;
            error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error)=>context.Editor.CanCreatePrimitive(out error);
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.CreatePrimitive(Enum.Parse<RoomObjectKind>((string)arguments["shape"],true),(string)arguments["name"],ObjectCapabilityData.Position(arguments),(float)arguments["scale"],ObjectCapabilityData.Color(arguments),out var id,out error))return false;
            operation=new CompletedCapability(new JObject {["objectId"]=id});return true;
        }
    }
    internal sealed class CreateRecipeCapability : CapabilityModule
    {
        public override string Id=>"object.create.recipe";
        public override string Label=>"Create recipe object";
        public override string Description=>"Create editable geometry and optional animation tracks from a bounded recipe. Returns objectId after saving; one room Undo edit. Set recipe.playing=false to create it idle and use animation.recipe.play on the returned ID for program-controlled playback. Setting playing=true explicitly starts the saved recipe animation. Geometry uses metres in room axes; scale is 0.1–4. The new assembly uses fixed physics.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"room.capacity","storage.writable","recipe.valid"};
        public override JObject InputSchema=>Object(new JObject {["name"]=Text("^.{0,80}$",80),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25),["scale"]=Number(.1,4),["recipe"]=RecipeSchema()});
        public override JObject OutputSchema=>Object(new JObject {["objectId"]=Resource(Text("^[a-f0-9]{32}$",32))});
        public override JObject Example=>new JObject {["name"]="Box robot",["x"]=.3f,["y"]=1.3f,["z"]=.65f,["scale"]=1,["recipe"]=ObjectCapabilityData.RobotExample()};
        public override bool Validate(JObject arguments,out string error) {
            error="Position must be within 25 metres of the room origin";if(ObjectCapabilityData.Position(arguments).sqrMagnitude>625)return false;
            error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error)=>context.Editor.CanCreateRecipe(ObjectCapabilityData.Recipe(arguments),out error);
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.CreateRecipe((string)arguments["name"],ObjectCapabilityData.Position(arguments),(float)arguments["scale"],ObjectCapabilityData.Recipe(arguments),out var id,out error))return false;
            operation=new CompletedCapability(new JObject {["objectId"]=id});return true;
        }
    }
    internal sealed class MoveObjectCapability : CapabilityModule
    {
        public override string Id=>"object.position.set";
        public override string Label=>"Move object";
        public override string Description=>"Place an existing object at x/y/z in room metres, within 25 m of origin. Keeps rotation and scale. Saves before completion and adds one Undo edit. This is immediate placement: velocity resets, then normal gravity resumes. Other objects keep running.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override JObject InputSchema=>Object(new JObject {["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        public override bool Validate(JObject arguments,out string error) {
            error="Position must be within 25 metres of the room origin";if(ObjectCapabilityData.Position(arguments).sqrMagnitude>625)return false;
            error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out _,out error))return false;
            return context.Editor.CanEditObject((string)arguments["target"],false,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.MoveObject((string)arguments["target"],ObjectCapabilityData.Position(arguments),out error))return false;operation=new CompletedCapability();return true;
        }
    }
    internal sealed class ResizeObjectCapability : CapabilityModule
    {
        public override string Id=>"object.scale.set";
        public override string Label=>"Resize object";
        public override string Description=>"Set uniform scale while retaining current position, rotation, model and recipe. User objects allow 0.1–4, the book 0.65–1.8, and Maestro 0.3–1.5. Saves before completion, one Undo edit. Resizing resets velocity; normal gravity resumes.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override JObject InputSchema=>Object(new JObject {["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),["scale"]=Number(.1,4)});
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out _,out error))return false;
            var limits=RoomDocument.ScaleLimits(context.Editor.Read((string)arguments["target"]).kind);float scale=(float)arguments["scale"];
            if(scale<limits.minimum||scale>limits.maximum) {error="Scale must be between "+limits.minimum+" and "+limits.maximum+" for this object";return false;}
            return context.Editor.CanEditObject((string)arguments["target"],false,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.ResizeObject((string)arguments["target"],(float)arguments["scale"],out error))return false;operation=new CompletedCapability();return true;
        }
    }
    internal sealed class PaintObjectCapability : CapabilityModule
    {
        public override string Id=>"object.color.set";
        public override string Label=>"Paint object";
        public override string Description=>"Set RGB tint, each 0–1, on a user-created object. Preserves its live position and velocity. Recipes, drawings and imported models keep their geometry. The included book and Maestro cannot be painted. Saves before completion, one Undo edit.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override JObject InputSchema=>Object(new JObject {["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["red"]=Number(0,1),["green"]=Number(0,1),["blue"]=Number(0,1)});
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out _,out error))return false;
            return context.Editor.CanEditObject((string)arguments["target"],true,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.PaintObject((string)arguments["target"],ObjectCapabilityData.Color(arguments),out error))return false;operation=new CompletedCapability();return true;
        }
    }
    internal sealed class DeleteObjectCapability : CapabilityModule
    {
        public override string Id=>"object.delete";
        public override string Label=>"Delete object";
        public override string Description=>"Remove a user-created object after saving the room. One Undo edit restores its saved geometry and animation data. The included book and Maestro cannot be deleted. Stop or receipt replay does not undo deletion; later actions targeting the removed ID fail.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override JObject InputSchema=>Object(new JObject {["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32))});
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out _,out error))return false;
            return context.Editor.CanEditObject((string)arguments["target"],true,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.DeleteObject((string)arguments["target"],out error))return false;operation=new CompletedCapability();return true;
        }
    }
    internal sealed class PhysicsImpulseCapability : CapabilityModule
    {
        public override string Id=>"object.physics.impulse";
        public override string Label=>"Push object";
        public override string Description=>"Apply x/y/z impulse in Newton-seconds along room axes (right/up/forward). Mass affects the velocity change; existing speed limits apply. Completion means the push was applied; gravity and collisions keep moving the object.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","rigidBody.dynamic","physics.running","geometry.ready"};
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override JObject InputSchema=>Object(new JObject {["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["x"]=Number(-20,20),["y"]=Number(-20,20),["z"]=Number(-20,20)});
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out var item,out error))return false;
            var rigid=item.GetComponent<RigidRoomItem>();if(!rigid) {error="This object has no rigid-body physics";return false;}
            return rigid.CanReceivePhysicsAction(out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;var item=context.Editor.Find((string)arguments["target"]);var rigid=item.GetComponent<RigidRoomItem>();
            var impulse=ObjectCapabilityData.Position(arguments);if(item.transform.parent)impulse=item.transform.parent.TransformDirection(impulse);
            if(!rigid.ApplyImpulse(impulse,out error))return false;
            operation=new CompletedCapability();return true;
        }
    }
    internal sealed class PhysicsStopCapability : CapabilityModule
    {
        public override string Id=>"object.physics.stop";
        public override string Label=>"Stop object motion";
        public override string Description=>"Clear linear and angular velocity once. This does not freeze or pin the object; gravity and collisions continue afterward.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","rigidBody.dynamic","physics.running","geometry.ready"};
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override JObject InputSchema=>Object(new JObject {["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32))});
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out var item,out error))return false;
            var rigid=item.GetComponent<RigidRoomItem>();if(!rigid) {error="This object has no rigid-body physics";return false;}
            return rigid.CanReceivePhysicsAction(out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;var item=context.Editor.Find((string)arguments["target"]);var rigid=item.GetComponent<RigidRoomItem>();
            if(!rigid.ClearMotion(out error))return false;
            operation=new CompletedCapability();return true;
        }
    }
}
