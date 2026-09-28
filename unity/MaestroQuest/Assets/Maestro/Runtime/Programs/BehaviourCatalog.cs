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
            public readonly CapabilityModule Module;
            public readonly string SearchText;
            public string Id=>Module.Id;
            public string Label=>Module.Label;
            public string Description=>Module.Description;
            public int Version=>Module.Version;
            public JObject InputSchema=>Module.InputSchema;
            public JObject OutputSchema=>Module.OutputSchema;
            public JObject Example=>Module.Example;
            public string Duration=>Module.Duration;
            public string Ownership=>Module.Ownership;
            public IReadOnlyList<string> Channels=>Module.Channels;
            public IReadOnlyList<string> Requirements=>Module.Requirements;
            public bool TryCall(JObject arguments,out CapabilityCall call,out string error) {
                call=null;if(!CapabilityArguments.Validate(arguments,InputSchema,out error)||!Module.Validate(arguments,out error))return false;
                call=new CapabilityCall(this,arguments);return true;
            }
            public JObject ToJson() {
                var value=new JObject {["id"]=Id,["version"]=Version,["label"]=Label,["input"]=InputSchema,
                    ["duration"]=Duration,["ownership"]=Ownership,["channels"]=new JArray(Channels),["requirements"]=new JArray(Requirements)};
                if(Description!=null)value["description"]=Description;
                if(Example!=null)value["example"]=Example;
                if(((JObject)OutputSchema["properties"]).Count>0)value["output"]=OutputSchema;return value;
            }
            public ActionDefinition(CapabilityModule module) {
                Module=module??throw new ArgumentNullException(nameof(module));
                SearchText=Id+" "+Label+" "+Description+" "+string.Join(" ",Requirements)+" "+string.Join(" ",InputSchema.Descendants().OfType<JProperty>().Where(p=>p.Name=="title"||p.Name=="x-requirements"||p.Name=="x-channels").Select(p=>p.Value.ToString()));
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
            public readonly string Activity,RoomSessionId;
            public readonly bool? PhysicsReady, PhysicsRunning;
            public FactContext(string activity=null, bool? physicsReady=null, bool? physicsRunning=null,string roomSessionId=null)
            { Activity=activity;PhysicsReady=physicsReady;PhysicsRunning=physicsRunning;RoomSessionId=roomSessionId; }
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
        public static readonly IReadOnlyList<ActionDefinition> Actions=Array.AsReadOnly(CapabilityModules.All.Select(module=>new ActionDefinition(module)).ToArray());
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
            new FactDefinition("room.sessionId",ProgramType.Text,"Current room session",context=>context.RoomSessionId==null?null:new ProgramValue(context.RoomSessionId)),
            new FactDefinition("maestro.state",ProgramType.Text,"Maestro state",context=>context.Activity==null?null:new ProgramValue(context.Activity)),
            new FactDefinition("physics.running",ProgramType.Boolean,"Physics running",context=>context.PhysicsRunning.HasValue?new ProgramValue(context.PhysicsRunning.Value):null),
            new FactDefinition("physics.ready",ProgramType.Boolean,"Room surfaces ready",context=>context.PhysicsReady.HasValue?new ProgramValue(context.PhysicsReady.Value):null),
        });
        public static readonly IReadOnlyDictionary<string,ProgramType> FactTypes=new ReadOnlyDictionary<string,ProgramType>(Facts.ToDictionary(x=>x.Id,x=>x.Type));
        static readonly Dictionary<RuleEventKind,EventDefinition> events=Events.ToDictionary(x=>x.Kind);
        static readonly Dictionary<string,FactDefinition> facts=Facts.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        static readonly Dictionary<string,ActionDefinition> actionIds=Actions.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        public static ActionDefinition Action(string id)=>id!=null&&actionIds.TryGetValue(id,out var value)?value:null;
        public static ActionDefinition Action(RuleActionKind kind) {
            string id=LegacyCapabilityAdapters.Id(kind);var provider=LegacyCapabilityAdapters.Provider(id);
            return provider==null?Action(id):new ActionDefinition(provider);
        }
        public static bool TryCall(string id,int version,JObject arguments,out CapabilityCall call,out string error)
        {
            call=null;var action=Action(id);error="Unknown capability or unsupported capability version";
            return action!=null&&action.Version==version&&action.TryCall(arguments,out call,out error);
        }
        // Explicit adapter for old tray controls and source round-trip checks, never scheduler dispatch.
        public static bool TryInvocation(string id,int version,JObject arguments,out RuleStep step,out string error) {
            step=null;return TryCall(id,version,arguments,out var call,out error)&&call.TryStep(out step,out error);
        }
        public static string[] ActionChannels(RuleActionKind kind)=>Action(kind)?.Channels.ToArray()??Array.Empty<string>();
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
        public static bool HasAction(RuleActionKind kind)=>Action(kind)!=null;
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
                ["actionIds"]=new JArray(LegacyCapabilityAdapters.ActionIds),
                ["invocations"]=LegacyCapabilityAdapters.Invocations(),
                ["actionLabels"]=new JArray(LegacyCapabilityAdapters.ActionIds.Select(id=>LegacyCapabilityAdapters.Provider(id)?.Label??Action(id).Label)),
                ["eventIds"]=new JArray(Events.OrderBy(x=>(int)x.Kind).Select(x=>x.Id)),
            } },
        };
    }
}
