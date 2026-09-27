// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Maestro.Quest.Rules;
using Maestro.Quest.Creation;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Programs
{
    /// <summary>Native vocabulary authority. The checked-in web manifest is generated from these registrations.
    /// Numeric rule identities are private adapters for existing physical controls and handlers.</summary>
    public static class BehaviourCatalog
    {
        public sealed class ActionDefinition
        {
            public readonly string Id, Label, Description;
            public int Version=>1;
            public JObject InputSchema=>CapabilityArguments.Schema(Kind);
            public JObject OutputSchema=>CapabilityArguments.OutputSchema(Kind);
            public JObject Example {
                get {
                    if(!RuleDocument.IsCreation(Kind))return null;
                    var step=new RuleStep {action=Kind,objectName=Kind==RuleActionKind.CreateRecipe?"Box robot":"Ball",creationColor=new Color(.2f,.6f,.9f,1)};
                    if(Kind==RuleActionKind.CreateRecipe) {step.creationRecipe=RecipeTemplates.BoxRobot(true);step.creationRecipe.playing=false;}
                    return CapabilityArguments.FromStep(step);
                }
            }
            public bool TryArguments(JObject arguments,out RuleStep step,out string error)=>CapabilityArguments.TryStep(Kind,arguments,out step,out error);
            public readonly RuleActionKind Kind;
            public readonly string Duration, Ownership;
            public readonly IReadOnlyList<string> Channels, Requirements;
            public JObject ToJson() {
                var value=new JObject {["id"]=Id,["version"]=Version,["label"]=Label,["input"]=InputSchema,
                    ["duration"]=Duration,["ownership"]=Ownership,["channels"]=new JArray(Channels),["requirements"]=new JArray(Requirements)};
                if(Description!=null)value["description"]=Description;
                if(Example!=null)value["example"]=Example;
                if(((JObject)OutputSchema["properties"]).Count>0)value["output"]=OutputSchema;return value;
            }
            public ActionDefinition(string id, RuleActionKind kind, string label, string requirements="",string description=null)
            {
                Id=id;Kind=kind;Label=label;Description=description;Duration=RuleDocument.IsInstant(kind)?"instant":"timed";
                Ownership=kind==RuleActionKind.Wait||RuleDocument.IsCreation(kind)?"none":kind==RuleActionKind.UpperBodyGesture||RuleDocument.IsSpatial(kind)?"exclusiveChannels":"exclusiveTargetAndProp";
                Channels=Array.AsReadOnly(ActionChannels(kind));
                Requirements=Array.AsReadOnly(requirements.Split(' ',StringSplitOptions.RemoveEmptyEntries));
            }
        }
        public sealed class EventDefinition
        {
            public readonly string Id, Label, Activity;
            public readonly RuleEventKind Kind;
            public readonly bool ObjectEvent;
            public EventDefinition(string id, RuleEventKind kind, string label, string activity=null, bool objectEvent=false)
            { Id=id;Kind=kind;Label=label;Activity=activity;ObjectEvent=objectEvent; }
        }
        public readonly struct FactContext
        {
            public readonly string Activity;
            public readonly bool? PhysicsReady, PhysicsRunning;
            public FactContext(string activity=null, bool? physicsReady=null, bool? physicsRunning=null)
            { Activity=activity;PhysicsReady=physicsReady;PhysicsRunning=physicsRunning; }
        }
        public sealed class FactDefinition
        {
            public readonly string Id, Label;
            public readonly ProgramType Type;
            readonly Func<FactContext,ProgramValue?> read;
            public FactDefinition(string id, ProgramType type, string label, Func<FactContext,ProgramValue?> read)
            { Id=id;Type=type;Label=label;this.read=read; }
            public bool TryRead(FactContext context, out ProgramValue value)
            {
                var result=read(context);value=result??default;
                return result.HasValue&&value.Type==Type;
            }
        }
        public static readonly IReadOnlyList<ActionDefinition> Actions=Array.AsReadOnly(new[] {
            new ActionDefinition("animation.recording.play",RuleActionKind.RecordedAnimation,"Recorded animation","target.exists target.unheld authoring.inactive recording.available"),
            new ActionDefinition("avatar.gesture.play",RuleActionKind.Gesture,"Gesture","target.exists target.unheld authoring.inactive avatar.available"),
            new ActionDefinition("avatar.gesture.upperBody",RuleActionKind.UpperBodyGesture,"Upper-body gesture","target.exists target.unheld authoring.inactive avatar.available"),
            new ActionDefinition("time.wait",RuleActionKind.Wait,"Wait"),
            new ActionDefinition("object.create.primitive",RuleActionKind.CreatePrimitive,"Create shape","room.capacity storage.writable","Create a ball, block or cylinder at x/y/z in room metres (within 25 m of the origin). Scale is a multiplier (1 is the usual tray shape); RGB is 0–1. Returns objectId after saving. The ball is bouncy; other shapes are solid, mass 0.5 kg. Each creation is one Undo edit. Stop leaves created objects in the room."),
            new ActionDefinition("object.create.recipe",RuleActionKind.CreateRecipe,"Create recipe object","room.capacity storage.writable recipe.valid","Create editable geometry and optional animation tracks from a bounded recipe. Returns objectId after saving; one room Undo edit. Set recipe.playing=false to create it idle and use animation.recipe.play on the returned ID for program-controlled playback. Setting playing=true explicitly starts the saved recipe animation. Geometry uses metres in room axes; scale is 0.1–4. The new assembly uses fixed physics."),
            new ActionDefinition("object.position.set",RuleActionKind.MoveObject,"Move object","target.exists target.unheld authoring.inactive storage.writable","Place an existing object at x/y/z in room metres, within 25 m of origin. Keeps rotation and scale. Saves before completion and adds one Undo edit. This is immediate placement: velocity resets, then normal gravity resumes. Other objects keep running."),
            new ActionDefinition("object.scale.set",RuleActionKind.ResizeObject,"Resize object","target.exists target.unheld authoring.inactive storage.writable","Set uniform scale while retaining current position, rotation, model and recipe. User objects allow 0.1–4, the book 0.65–1.8, and Maestro 0.3–1.5. Saves before completion, one Undo edit. Resizing resets velocity; normal gravity resumes."),
            new ActionDefinition("object.color.set",RuleActionKind.PaintObject,"Paint object","target.exists target.unheld authoring.inactive storage.writable","Set RGB tint, each 0–1, on a user-created object. Preserves its live position and velocity. Recipes, drawings and imported models keep their geometry. The included book and Maestro cannot be painted. Saves before completion, one Undo edit."),
            new ActionDefinition("object.delete",RuleActionKind.DeleteObject,"Delete object","target.exists target.unheld authoring.inactive storage.writable","Remove a user-created object after saving the room. One Undo edit restores its saved geometry and animation data. The included book and Maestro cannot be deleted. Stop or receipt replay does not undo deletion; later actions targeting the removed ID fail."),
            new ActionDefinition("object.physics.impulse",RuleActionKind.PhysicsImpulse,"Push object","target.exists target.unheld authoring.inactive rigidBody.dynamic physics.running geometry.ready","Apply x/y/z impulse in Newton-seconds along room axes (right/up/forward). Mass affects the velocity change; existing speed limits apply. Completion means the push was applied; gravity and collisions keep moving the object."),
            new ActionDefinition("object.physics.stop",RuleActionKind.PhysicsStop,"Stop object motion","target.exists target.unheld authoring.inactive rigidBody.dynamic physics.running geometry.ready","Clear linear and angular velocity once. This does not freeze or pin the object; gravity and collisions continue afterward."),
            new ActionDefinition("object.recording.throw",RuleActionKind.ThrowRecording,"Throw recording","target.exists target.unheld authoring.inactive recording.twoFrames rigidBody.dynamic physics.running"),
            new ActionDefinition("avatar.look.user",RuleActionKind.LookAtUser,"Look at user","target.exists target.unheld authoring.inactive avatar.spatialReady"),
            new ActionDefinition("avatar.follow.user",RuleActionKind.FollowUser,"Follow user","target.exists target.unheld authoring.inactive avatar.spatialReady physics.running navigation.floorReady"),
            new ActionDefinition("animation.embedded.play",RuleActionKind.ImportedClip,"Imported clip","target.exists target.unheld authoring.inactive model.loaded embeddedClip.available"),
            new ActionDefinition("animation.library.play",RuleActionKind.LibraryMotion,"Library motion","target.exists target.unheld authoring.inactive model.loaded motion.available rig.compatible"),
            new ActionDefinition("animation.recipe.play",RuleActionKind.RecipeAnimation,"Recipe animation","target.exists target.unheld authoring.inactive recipe.tracksAvailable"),
        });
        public static readonly IReadOnlyList<EventDefinition> Events=Array.AsReadOnly(new[] {
            new EventDefinition("maestro.speaking.enter",RuleEventKind.Speaking,"Speaking","speaking"),
            new EventDefinition("maestro.listening.enter",RuleEventKind.Listening,"Listening","listening"),
            new EventDefinition("maestro.thinking.enter",RuleEventKind.Thinking,"Thinking","thinking"),
            new EventDefinition("maestro.idle.enter",RuleEventKind.Idle,"Idle","idle"),
            new EventDefinition("object.tapped",RuleEventKind.ItemTapped,"Item tapped",objectEvent:true),
            new EventDefinition("object.grabbed",RuleEventKind.ItemGrabbed,"Item grabbed",objectEvent:true),
            new EventDefinition("object.released",RuleEventKind.ItemReleased,"Item released",objectEvent:true),
        });
        public static readonly IReadOnlyList<FactDefinition> Facts=Array.AsReadOnly(new[] {
            new FactDefinition("maestro.state",ProgramType.Text,"Maestro state",context=>context.Activity==null?null:new ProgramValue(context.Activity)),
            new FactDefinition("physics.running",ProgramType.Boolean,"Physics running",context=>context.PhysicsRunning.HasValue?new ProgramValue(context.PhysicsRunning.Value):null),
            new FactDefinition("physics.ready",ProgramType.Boolean,"Room surfaces ready",context=>context.PhysicsReady.HasValue?new ProgramValue(context.PhysicsReady.Value):null),
        });
        public static readonly IReadOnlyDictionary<string,ProgramType> FactTypes=new ReadOnlyDictionary<string,ProgramType>(Facts.ToDictionary(x=>x.Id,x=>x.Type));
        static readonly Dictionary<RuleActionKind,ActionDefinition> actions=Actions.ToDictionary(x=>x.Kind);
        static readonly Dictionary<RuleEventKind,EventDefinition> events=Events.ToDictionary(x=>x.Kind);
        static readonly Dictionary<string,FactDefinition> facts=Facts.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        static readonly Dictionary<string,ActionDefinition> actionIds=Actions.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        public static ActionDefinition Action(string id)=>id!=null&&actionIds.TryGetValue(id,out var value)?value:null;
        public static ActionDefinition Action(RuleActionKind kind)=>actions.TryGetValue(kind,out var value)?value:null;
        public static bool TryInvocation(string id,int version,JObject arguments,out RuleStep step,out string error)
        {
            step=null;var action=Action(id);error="Unknown capability or unsupported capability version";
            return action!=null && action.Version==version && action.TryArguments(arguments,out step,out error);
        }
        public static string[] ActionChannels(RuleActionKind kind)=>kind switch {
            RuleActionKind.Wait or RuleActionKind.CreatePrimitive or RuleActionKind.CreateRecipe=>Array.Empty<string>(),
            RuleActionKind.UpperBodyGesture=>new[] {"upperBody"},
            RuleActionKind.LookAtUser=>new[] {"gaze"},
            RuleActionKind.FollowUser=>new[] {"locomotion","gaze"},
            _=>new[] {"wholeTarget"}
        };
        public readonly struct Claim
        {
            public readonly string Target,Channel;
            public Claim(string target,string channel) {Target=target;Channel=channel;}
            public bool Conflicts(Claim other)=>Target==other.Target&&(Channel==other.Channel||Channel=="wholeTarget"||other.Channel=="wholeTarget");
        }
        public static Claim[] Claims(RuleStep step) {
            var claims=ActionChannels(step.action).Select(channel=>new Claim(step.targetId,channel)).ToList();
            if(!string.IsNullOrEmpty(step.propId))claims.Add(new Claim(step.propId,"wholeTarget"));
            return claims.ToArray();
        }
        public static bool HasAction(RuleActionKind kind)=>actions.ContainsKey(kind);
        public static EventDefinition Event(RuleEventKind kind)=>events.TryGetValue(kind,out var value)?value:null;
        public static bool TryRead(string id, FactContext context, out ProgramValue value)
        {
            value=default;return id!=null&&facts.TryGetValue(id,out var fact)&&fact.TryRead(context,out value);
        }
        public static JObject Manifest()=>new JObject {
            ["version"]=1,
            ["actions"]=new JArray(Actions.Select(x=>x.ToJson())),
            ["events"]=new JArray(Events.Select(x=>new JObject { ["id"]=x.Id,["label"]=x.Label,["activity"]=x.Activity,["objectEvent"]=x.ObjectEvent })),
            ["facts"]=new JArray(Facts.Select(x=>new JObject { ["id"]=x.Id,["type"]=x.Type.ToString().ToLowerInvariant(),["label"]=x.Label })),
            ["adapters"]=new JObject { ["ruleStep"]=new JObject {
                ["actionIds"]=new JArray(Actions.OrderBy(x=>(int)x.Kind).Select(x=>x.Id)),
                ["eventIds"]=new JArray(Events.OrderBy(x=>(int)x.Kind).Select(x=>x.Id)),
            } },
        };
    }
}
