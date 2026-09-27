// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Programs
{
    /// <summary>Native vocabulary authority. The checked-in web manifest is generated from these registrations.
    /// Numeric rule identities are private adapters for existing physical controls and handlers.</summary>
    public static class BehaviourCatalog
    {
        public sealed class ActionDefinition
        {
            public readonly string Id, Label;
            public int Version=>1;
            public JObject InputSchema=>CapabilityArguments.Schema(Kind);
            public bool TryArguments(JObject arguments,out RuleStep step,out string error)=>CapabilityArguments.TryStep(Kind,arguments,out step,out error);
            public readonly RuleActionKind Kind;
            public readonly string Duration, Ownership;
            public readonly IReadOnlyList<string> Channels, Requirements;
            public ActionDefinition(string id, RuleActionKind kind, string label, string requirements="")
            {
                Id=id;Kind=kind;Label=label;Duration="timed";
                // Current executor reserves whole targets. Future layer support
                // must change the actual handler before narrowing this contract.
                Ownership=kind==RuleActionKind.Wait?"none":"exclusiveTargetAndProp";
                Channels=Array.AsReadOnly(kind==RuleActionKind.Wait?Array.Empty<string>():new[] {"wholeTarget"});
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
            new ActionDefinition("time.wait",RuleActionKind.Wait,"Wait"),
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
        public static bool HasAction(RuleActionKind kind)=>actions.ContainsKey(kind);
        public static EventDefinition Event(RuleEventKind kind)=>events.TryGetValue(kind,out var value)?value:null;
        public static bool TryRead(string id, FactContext context, out ProgramValue value)
        {
            value=default;return id!=null&&facts.TryGetValue(id,out var fact)&&fact.TryRead(context,out value);
        }
        public static JObject Manifest()=>new JObject {
            ["version"]=1,
            ["actions"]=new JArray(Actions.Select(x=>new JObject { ["id"]=x.Id,["version"]=x.Version,["label"]=x.Label,["input"]=x.InputSchema,
                ["duration"]=x.Duration,["ownership"]=x.Ownership,["channels"]=new JArray(x.Channels),["requirements"]=new JArray(x.Requirements) })),
            ["events"]=new JArray(Events.Select(x=>new JObject { ["id"]=x.Id,["label"]=x.Label,["activity"]=x.Activity,["objectEvent"]=x.ObjectEvent })),
            ["facts"]=new JArray(Facts.Select(x=>new JObject { ["id"]=x.Id,["type"]=x.Type.ToString().ToLowerInvariant(),["label"]=x.Label })),
            ["adapters"]=new JObject { ["ruleStep"]=new JObject {
                ["actionIds"]=new JArray(Actions.OrderBy(x=>(int)x.Kind).Select(x=>x.Id)),
                ["eventIds"]=new JArray(Events.OrderBy(x=>(int)x.Kind).Select(x=>x.Id)),
            } },
        };
    }
}
