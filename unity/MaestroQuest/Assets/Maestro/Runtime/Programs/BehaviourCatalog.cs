// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Maestro.Quest.Rules;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
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
                if(Module.MinimumProgramVersion>2)value["minimumProgramVersion"]=Module.MinimumProgramVersion;
                if(Module.Domain!="room")value["domain"]=Module.Domain;
                if(Description!=null)value["description"]=Description;
                if(Example!=null)value["example"]=Example;
                if(((JObject)OutputSchema["properties"]).Count>0)value["output"]=OutputSchema;return value;
            }
            public ActionDefinition(CapabilityModule module) {
                Module=module??throw new ArgumentNullException(nameof(module));
                if(module.Domain is not ("room" or "workspace"))throw new ArgumentException("Unknown capability execution domain.");
                SearchText=Id+" "+Label+" "+Description+" "+string.Join(" ",Requirements)+" "+string.Join(" ",InputSchema.Descendants().OfType<JProperty>().Where(p=>p.Name=="title"||p.Name=="x-enum-labels"||p.Name=="x-requirements"||p.Name=="x-channels").Select(p=>p.Value.ToString()));
            }
        }
        public sealed class EventDefinition
        {
            public readonly string Id, Label, Activity;
            public readonly int Version=1;
            public readonly RuleEventKind? Kind;
            public readonly bool ObjectEvent;
            public readonly string Description;
            readonly JObject fields,input,example;
            readonly string[] extraFeatures=Array.Empty<string>();
            readonly Func<IProgramEventWorld,JObject,float,IProgramEventWatch> watch;
            public bool HasSubscription=>watch!=null;
            public JObject Input=>input==null?null:(JObject)input.DeepClone();
            public JObject Fields=>fields==null?null:(JObject)fields.DeepClone();
            public EventDefinition(string id, RuleEventKind kind, string label, string activity=null, bool objectEvent=false)
            { Id=id;Kind=kind;Label=label;Activity=activity;ObjectEvent=objectEvent;
                Description=objectEvent?"An object interaction occurred. The primary text value is its object ID; source filters accept that exact ID or empty for any object.":"Maestro entered "+activity+". The primary text value is the state name. Source must be empty. The initial activity snapshot establishes a baseline without emitting an event."; }
            // New native events do not require a legacy tray enum or a signal route.
            public EventDefinition(string id,string label,string description,JObject fields,bool objectEvent=false,JObject input=null,JObject example=null,Func<IProgramEventWorld,JObject,float,IProgramEventWatch> watch=null,string[] features=null,int version=1)
            { if(version<1)throw new ArgumentOutOfRangeException(nameof(version));Version=version;Id=id;Label=label;Description=description;ObjectEvent=objectEvent;this.fields=(JObject)fields.DeepClone();this.input=input==null?null:(JObject)input.DeepClone();this.example=example==null?null:(JObject)example.DeepClone();this.watch=watch;extraFeatures=features==null?Array.Empty<string>():(string[])features.Clone(); }
            public bool ValidArguments(int version,JObject arguments,out string error) {
                error="Unknown event subscription version or arguments";return HasSubscription&&version==Version&&CapabilityArguments.Validate(arguments,input,out error,"event arguments");
            }
            public ProgramType ArgumentType(string path,JObject arguments=null) {
                var field=CapabilitySchema.Field(input,path,arguments);if((bool?)field?["x-static"]==true)return ProgramType.Void;
                return ((string)field?["type"]) switch {"string"=>ProgramType.Text,"number" or "integer"=>ProgramType.Number,"boolean"=>ProgramType.Boolean,_=>ProgramType.Void};
            }
            public bool TryWatch(IProgramEventWorld world,JObject arguments,float now,out IProgramEventWatch result,out string error) {
                result=null;if(!ValidArguments(Version,arguments,out error))return false;
                try {result=watch(world,(JObject)arguments.DeepClone(),now);return true;}catch(ProgramFault fault) {error=fault.Message;return false;}
            }
            public ProgramType FieldType(string name)=>((string)fields?["properties"]?[name]?["type"]) switch {
                "string"=>ProgramType.Text,"number" or "integer"=>ProgramType.Number,"boolean"=>ProgramType.Boolean,_=>ProgramType.Void
            };
            public bool ValidFields(JObject value)=>fields==null?value==null||value.Count==0:CapabilityArguments.Validate(value,fields,out _,"event fields");
            public JObject ToJson() {
                var result=new JObject {["id"]=Id,["version"]=Version,["label"]=Label,["activity"]=Activity,["objectEvent"]=ObjectEvent,["valueType"]="text",["description"]=Description,
                    ["features"]=fields==null?new JArray("eventPrograms.v1"):new JArray("eventPrograms.v1","eventFields.v1")};
                if(fields!=null)result["fields"]=Fields;
                if(HasSubscription) {((JArray)result["features"]).Add("eventSubscriptions.v1");result["input"]=Input;result["example"]=example.DeepClone();}
                var required=((JArray)result["features"]).Values<string>().Concat(extraFeatures).Concat(input?["x-features"] is JArray features?features.Values<string>():Array.Empty<string>());
                result["features"]=new JArray(required.Distinct());
                return result;
            }
        }
        public readonly struct FactContext
        {
            public readonly string Activity,RoomSessionId;
            public readonly bool? PhysicsReady, PhysicsRunning;
            public readonly IProgramEventWorld World;
            public readonly RoomEditor Editor;
            public readonly WorkspaceHost Workspace;
            public FactContext(string activity=null, bool? physicsReady=null, bool? physicsRunning=null,string roomSessionId=null,IProgramEventWorld world=null,RoomEditor editor=null,WorkspaceHost workspace=null)
            { Activity=activity;PhysicsReady=physicsReady;PhysicsRunning=physicsRunning;RoomSessionId=roomSessionId;World=world;Editor=editor;Workspace=workspace??(editor?editor.GetComponentInParent<WorkspaceHost>():null); }
        }
        public sealed class FactDefinition
        {
            public readonly string Id, Label, Description, Domain;
            public readonly int Version=1;
            public readonly ProgramDataType Type;
            readonly JObject input,example;
            readonly string[] features;
            readonly Func<FactContext,JObject,ProgramValue?> read;
            public JObject Input=>input==null?null:(JObject)input.DeepClone();
            public bool Parameterized=>input!=null;
            public FactDefinition(string id,ProgramType type,string label,string description,Func<FactContext,ProgramValue?> read)
                :this(id,type,label,description,null,null,(context,args)=>read(context)) {}
            public FactDefinition(string id,ProgramDataType type,string label,string description,JObject input,JObject example,Func<FactContext,JObject,ProgramValue?> read,string domain="room",string[] features=null,int version=1)
            {if(version<1)throw new ArgumentOutOfRangeException(nameof(version));Version=version;Id=id;Type=type;Label=label;Description=description;Domain=domain;this.input=input==null?null:(JObject)input.DeepClone();this.example=example==null?null:(JObject)example.DeepClone();this.read=read;this.features=features==null?Array.Empty<string>():(string[])features.Clone();}
            static JToken TypeJson(ProgramDataType type)=>type.Kind==ProgramType.Record?new JObject {["record"]=new JObject(type.Fields.Select(p=>new JProperty(p.Key,TypeJson(p.Value))))}:type.Kind==ProgramType.List?new JObject {["list"]=TypeJson(type.Item)}:new JValue(type.ToString().ToLowerInvariant());
            public JObject ToJson() {
                var value=new JObject {["id"]=Id,["version"]=Version,["type"]=TypeJson(Type),["label"]=Label,["description"]=Description};
                if(Domain!="room")value["domain"]=Domain;
                if(input!=null){value["input"]=Input;value["example"]=example.DeepClone();}
                var required=(input==null?Array.Empty<string>():new[]{"factQueries.v1"}).Concat(features).Distinct().ToArray();
                if(required.Length>0)value["features"]=new JArray(required);return value;
            }
            public bool ValidArguments(int version,JObject arguments,out string error) {
                error="Unknown fact version or arguments";return version==Version&&(input==null?arguments==null:arguments!=null&&CapabilityArguments.Validate(arguments,input,out error,"fact arguments"));
            }
            public ProgramType ArgumentType(string path,JObject arguments=null) {
                var field=CapabilitySchema.Field(input,path,arguments);if((bool?)field?["x-static"]==true)return ProgramType.Void;
                return ((string)field?["type"]) switch {"string"=>ProgramType.Text,"number" or "integer"=>ProgramType.Number,"boolean"=>ProgramType.Boolean,_=>ProgramType.Void};
            }
            public bool ValidValue(ProgramValue value) {
                try {return value.Type==Type&&value.Value!=null&&ProgramValue.Literal(JToken.FromObject(value.Value),Type).Type==Type;}catch(ProgramFault){return false;}
            }
            public bool TryRead(FactContext context,out ProgramValue value)=>TryRead(context,Version,null,out value);
            public bool TryRead(FactContext context,int version,JObject arguments,out ProgramValue value) {
                value=default;if(!ValidArguments(version,arguments,out _))return false;
                var result=read(context,arguments);if(!result.HasValue||!ValidValue(result.Value))return false;value=result.Value;return true;
            }
        }
        public static readonly IReadOnlyList<ActionDefinition> Actions=Array.AsReadOnly(CapabilityModules.All.Select(module=>new ActionDefinition(module)).ToArray());
        public static readonly IReadOnlyList<EventDefinition> Events=Array.AsReadOnly(new[] {
            MediumContactFacts.Event(),ConnectionCapability.BreakEvent(),ContainerPouringFacts.Poured(),ContainerScoopingFacts.Scooped(),WorldWeatherCapability.Collected(),AnchorProximitySubscription.Definition(),
            PhysicsMotionSubscription.Definition(),AudioInstanceSubscription.Definition(),
            CalendarSubscription.Definition(),CatchObjectCapability.ContactEvent(),
            new EventDefinition("maestro.speaking.enter",RuleEventKind.Speaking,"Speaking","speaking"),
            new EventDefinition("maestro.listening.enter",RuleEventKind.Listening,"Listening","listening"),
            new EventDefinition("maestro.thinking.enter",RuleEventKind.Thinking,"Thinking","thinking"),
            new EventDefinition("maestro.idle.enter",RuleEventKind.Idle,"Idle","idle"),
            new EventDefinition("object.tapped",RuleEventKind.ItemTapped,"Item tapped",objectEvent:true),
            new EventDefinition("object.grabbed",RuleEventKind.ItemGrabbed,"Item grabbed",objectEvent:true),
            new EventDefinition("object.released",RuleEventKind.ItemReleased,"Item released",objectEvent:true),
            new EventDefinition("object.collided","Object contact began",
                "A physics contact began while room physics was running. Value is the source object ID. otherId is a registered room object ID or empty; otherKind distinguishes object, scannedRoom, controller and environment. speed is relative speed in metres/second; x/y/z are one contact point captured in authored room coordinates when the contact begins. A queued point stays in that room after virtual-world movement; it is not a persistent anchor to a real surface. Compound colliders can produce separate contacts. This is not a continuous contact or precise impact-energy measurement. Fields do not authorize editing new objects.",
                CapabilitySchema.Object(new JObject {
                    ["otherId"]=CapabilitySchema.Text("^[a-zA-Z0-9_]{0,32}$",32),
                    ["otherKind"]=CapabilitySchema.Choice("object","scannedRoom","controller","environment"),
                    ["speed"]=CapabilitySchema.Number(0,1000000),
                    ["x"]=CapabilitySchema.Number(-1000000,1000000),["y"]=CapabilitySchema.Number(-1000000,1000000),["z"]=CapabilitySchema.Number(-1000000,1000000)
                }),objectEvent:true,version:2),
            new EventDefinition("object.proximity.changed","Object distance crossed a boundary",
                "Sampled change between two explicit room-object transform origins in world metres, not mesh distance, contact, visibility or navigation. Source filter must be empty; choose source and target in subscription arguments. Samples at most 10 times/second while this wait is active. Initial distance is a baseline, never an event. Enter at distance <= radius; exit at distance >= radius + hysteresis. Transition selects enter, exit or either. Value is the source object ID; fields include the other ID, inside and measured distance. No missed crossings replay after an action, timeout, pause or reload. Missing/disabled objects fail the wait; observations never authorize edits. Physics need not run: grabs and animations also change positions.",
                CapabilitySchema.Object(new JObject {["otherId"]=CapabilitySchema.Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["inside"]=new JObject {["type"]="boolean"},["distance"]=CapabilitySchema.Number(0,1000000)}),
                input:CapabilitySchema.Object(new JObject {
                    ["source"]=CapabilitySchema.Resource(CapabilitySchema.Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),["target"]=CapabilitySchema.Resource(CapabilitySchema.Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),
                    ["radius"]=CapabilitySchema.Number(.05,10),["hysteresis"]=CapabilitySchema.Number(.01,2),["transition"]=CapabilitySchema.Choice("enter","exit","either")}),
                example:new JObject {["source"]="maestro",["target"]="book",["radius"]=.5,["hysteresis"]=.05,["transition"]="either"},
                watch:(world,args,now)=>new ProximitySubscription(world,args,now)),
        });
        public static readonly IReadOnlyList<FactDefinition> Facts=Array.AsReadOnly(new[] {
            AudioPlaybackCapabilities.Fact(),AudioPlaybackCapabilities.ActiveFact(),ImageImportCapability.ChatItem(),ImageImportCapability.ChatLibrary(),ImageImportCapability.Rendering(),ImageImportCapability.Selection(),ImageImportCapability.Library(),AudioImportCapability.Selection(),AudioImportCapability.Library(),AudioFacts.List(),AudioFacts.Definition(),AudioFacts.Emitters(),AudioFacts.Emitter(),AudioFacts.Playback(),NativeObjectFacts.Definition(),NativeObjectFacts.Presence(),LayoutCapability.Placement(),ConstructionSelectionCapability.Fact(),ConstructionManipulationCapability.Fact(),ConstructionSnappingCapability.Fact(),ConstructionSnappingCapability.PreviewFact(),StructureFacts.List(),StructureFacts.Definition(),StructureFacts.Slot(),StructureFacts.State(),CreateTemplateCapability.Fact(),LaunchObjectCapability.Trajectory(),CatchObjectCapability.Fact(),DrawingToolCapability.Fact(),DrawingTipCapability.Fact(),ContainerCapability.Fact(),MaterialStoreCapability.Fact(),HeightFieldCapability.Fact(),HeightFieldCapability.Samples(),SculptTipCapability.Fact(),SculptToolCapability.Fact(),MaterialPackToolCapability.SettingsFact(),MaterialPackToolCapability.CaptureFact(),SculptResolveCapability.Fact(),SculptResolveCapability.Path(),MaterialScoopFacts.Live(),ContainerPouringFacts.Live(),ContainerScoopingFacts.Live(),MediumContactFacts.Object(),MediumContactFacts.Input(),MediumFacts.World(),MediumFacts.Body(),SnapPointCapability.Fact(),SnapPointCapability.ListFact(),ConnectionCapability.Fact(),ConnectionCapability.TuningFact(),ConnectionCapability.SliderFact(),ConnectionCapability.TravelFact(),ConnectionCapability.FrameFact(),ConnectionCapability.State(),WindowCapability.Fact(),ModelGeometryCapability.Fact(),DrawingSurfaceFacts.Overview(),DrawingSurfaceFacts.Definition(),DrawingSurfaceFacts.Strokes(),DrawingSurfaceFacts.Stroke(),DrawingEditCapability.SummaryFact(),DrawingEditCapability.PointsFact(),DrawingResolveCapability.Fact(),RecipeEditFacts.Overview(),RecipeEditFacts.Part(),RecipeEditFacts.Track(),LatheProfileFact.Definition(),SweepPathFact.Definition(),CollisionCapability.Overview(),CollisionCapability.ShapeFact(),RecipePartAnimationFacts.Pose(),ObjectAttachmentFacts.Anchor(),ObjectAttachmentFacts.Attachment(),NativeObjectFacts.Position(),PhysicsSettingsCapability.Fact(),WaterTraversalCapability.Fact(),WaterTraversalCapability.Path(),AvatarMovementSettingsCapability.Fact(),AvatarWalkSettingsCapability.Fact(),AvatarWalkSettingsCapability.Clips(),ControllerConfigurationCapability.Fact(),ControllerModeCapability.Fact(),WorldPresentationCapability.Fact(),LayerPresentationCapability.Fact(),AnimationAuthoringFacts.Summary(),AnimationAuthoringFacts.Frame(),AnimationAuthoringFacts.Joint(),AnimationRecordingCapability.Fact(),AnimationPosingCapability.Fact(),AnimationPosingCapability.Joint(),AvatarModelCapability.Fact(), AvatarModelCapability.IncludedFact(),ModelImportCapability.Fact(),ModelImportCapability.Motions(),ModelImportCapability.Archive(),MotionBatchCapability.Session(),IncludedMotionsCapability.Fact(),MotionBatchCapability.File(),
            WorkspaceRetentionFacts.Status(),WorkspaceRetentionFacts.Entry(),WorkspaceRetentionFacts.Removal(),
            WorkspaceEvidenceFacts.Status(),WorkspaceEvidenceFacts.Entry(),
            WorkspaceHistoryFacts.Status(),
            WorkspaceSelectionFacts.Selection(),
            WorkspaceActivationFacts.Current(),
            WorkspaceActivationFacts.Activation(),
            WorkspaceReviewFacts.Status(),
                WorkspacePreviousFacts.Previous(),
            WorkspaceRecoveryFacts.Status(),WorkspaceRecoveryFacts.Candidate(),WorkspaceRecoveryFacts.Preview(),
            CalendarSubscription.Fact(),RuntimeDiagnosticFacts.Frames(),RuntimeDiagnosticFacts.Models(),RuntimeDiagnosticFacts.ModelReservation(),RuntimeDiagnosticFacts.Images(),RuntimeDiagnosticFacts.ImageReservation(),RuntimeDiagnosticFacts.ImageOwner(),RuntimeDiagnosticFacts.Audio(),RuntimeDiagnosticFacts.AudioReservation(),RuntimeDiagnosticFacts.AudioOwner(),RuntimeDiagnosticFacts.Motions(),RuntimeDiagnosticFacts.Acoustics(),RuntimeDiagnosticFacts.CollisionResources(),RuntimeDiagnosticFacts.CollisionResource(),
            new FactDefinition("room.sessionId",ProgramType.Text,"Current room session","Current room session ID, including while using the saved room. Begin and Discard replace this identity. Read scene.temporaryRoom.active to distinguish temporary play from the saved room. Reading this ID does not begin, keep or discard a room.",context=>context.RoomSessionId==null?null:new ProgramValue(context.RoomSessionId)),
            new FactDefinition("maestro.state",ProgramType.Text,"Maestro state","Current observed tutor state: speaking, listening, thinking or idle. Unavailable before a reliable activity snapshot, during audio suspension or when the room runtime is paused.",context=>context.Activity==null?null:new ProgramValue(context.Activity)),
            RoomEnvironmentCapability.Fact(),RoomToolsCapability.Fact(),RoomToolRecoveryCapability.Fact(),WorldViewpointCapability.Fact(),WorldLightingCapability.Fact(),WorldTimeCapability.Fact(),WorldTimeCapability.Illumination(),WorldWeatherCapability.Fact(),WorldWeatherCapability.Exposure(),WorldWeatherCapability.Rain(),WorldIdentityFacts.Fact(),RegionFacts.Region(),RegionFacts.Regions(),RegionFacts.Members(),RegionFacts.Retention(),RegionFacts.Binding(),WorldGroundFacts.Fact(),ScanDrawingCapability.Fact(),RoomScanFacts.Status(),RoomScanFacts.Page(),RoomScanFacts.Surface(),PhysicsSimulationCapability.Fact(),PhysicsEnvironmentCapability.Fact(),EnvironmentProfileFacts.Binding(),EnvironmentProfileFacts.Profile(),EnvironmentProfileFacts.Profiles(),AppearanceFacts.Definitions(),AppearanceFacts.Definition(),AppearanceFacts.Members(),AppearanceFacts.Bindings(),AppearanceFacts.Targets(),VisibilityLayerFacts.Layer(),VisibilityLayerFacts.Layers(),VisibilityLayerFacts.Members(),VisibilityLayerFacts.Binding(),
            new FactDefinition("physics.running",ProgramType.Boolean,"Physics running","Whether room physics is currently running. False is an observed value; it is not an unavailable reading.",context=>context.PhysicsRunning.HasValue?new ProgramValue(context.PhysicsRunning.Value):null),
            new FactDefinition("physics.ready",ProgramType.Boolean,"Room surfaces ready","Whether the selected physics environment is ready (aligned scan with real collisions on, accepted virtual ground with them off). This does not start physics or guarantee a particular navigation path.",context=>context.PhysicsReady.HasValue?new ProgramValue(context.PhysicsReady.Value):null),
        });
        public static readonly IReadOnlyDictionary<string,ProgramDataType> FactTypes=new ReadOnlyDictionary<string,ProgramDataType>(Facts.ToDictionary(x=>x.Id,x=>x.Type));
        static readonly Dictionary<RuleEventKind,EventDefinition> events=Events.Where(x=>x.Kind.HasValue).ToDictionary(x=>x.Kind.Value);
        static readonly Dictionary<string,EventDefinition> eventIds=Events.ToDictionary(x=>x.Id,StringComparer.Ordinal);
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
        public static FactDefinition Fact(string id)=>id!=null&&facts.TryGetValue(id,out var value)?value:null;
        public static EventDefinition Event(string id)=>id!=null&&eventIds.TryGetValue(id,out var value)?value:null;
        public static EventDefinition Event(RuleEventKind kind)=>events.TryGetValue(kind,out var value)?value:null;
        public static bool TryRead(string id, FactContext context, out ProgramValue value)
        {
            return TryRead(id,1,null,context,out value);
        }
        public static bool TryRead(string id,int version,JObject arguments,FactContext context,out ProgramValue value) {
            value=default;return id!=null&&facts.TryGetValue(id,out var fact)&&fact.TryRead(context,version,arguments,out value);
        }
        public static JObject Manifest()=>new JObject {
            ["version"]=1,
            ["limits"]=new JObject{["programResources"]=BehaviourProgram.MaximumResources,["programRecordFields"]=ProgramDataType.MaximumRecordFields},
            ["actions"]=new JArray(Actions.Select(x=>x.ToJson())),
            ["events"]=new JArray(Events.Select(x=>x.ToJson())),
            ["facts"]=new JArray(Facts.Select(x=>x.ToJson())),
            ["adapters"]=new JObject { ["ruleStep"]=new JObject {
                ["actionIds"]=new JArray(LegacyCapabilityAdapters.ActionIds),
                ["invocations"]=LegacyCapabilityAdapters.Invocations(),
                ["actionLabels"]=new JArray(LegacyCapabilityAdapters.ActionIds.Select(id=>LegacyCapabilityAdapters.Provider(id)?.Label??Action(id).Label)),
                ["eventIds"]=new JArray(Events.Where(x=>x.Kind.HasValue).OrderBy(x=>(int)x.Kind.Value).Select(x=>x.Id)),
            } },
        };
    }
}
