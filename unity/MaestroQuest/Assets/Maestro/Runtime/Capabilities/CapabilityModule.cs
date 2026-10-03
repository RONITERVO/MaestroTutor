// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    /// <summary>Trusted native extension point. User programs contain data, never modules or engine code.</summary>
    public abstract class CapabilityModule
    {
        internal virtual IEnumerable<CapabilityStepAdapter> StepAdapters=>Array.Empty<CapabilityStepAdapter>();
        public abstract string Id {get;}
        public virtual int Version=>1;
        public abstract string Label {get;}
        public virtual string Description=>null;
        public abstract JObject InputSchema {get;}
        public virtual JObject OutputSchema=>CapabilitySchema.Object(new JObject());
        public virtual JObject Example=>null;
        public virtual string Domain=>"room";
        public abstract string Duration {get;}
        // Trusted native I/O deadline, never a user-program argument. Animation loading stays at 30s.
        internal virtual float CompletionTimeoutSeconds=>30;
        internal virtual int MaximumCreatedObjects(JObject arguments)=>((JObject)OutputSchema["properties"]).Properties().Count(p=>(string)p.Value["x-resource"]=="object");
        public virtual string Ownership=>Channels.Count==0?"none":"exclusiveTargetAndProp";
        public virtual IReadOnlyList<string> Channels=>Array.Empty<string>();
        public virtual IReadOnlyList<string> Requirements=>Array.Empty<string>();
        public virtual bool Validate(JObject arguments,out string error) {error=null;return true;}
        public virtual bool RequiresQuietRoom(JObject arguments)=>false;
        public virtual BehaviourCatalog.Claim[] Claims(JObject arguments)=>Channels.Select(c=>new BehaviourCatalog.Claim((string)arguments["target"],c)).ToArray();
        public abstract bool CanRun(CapabilityContext context,JObject arguments,out string error);
        public abstract bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error);
    }
    /// <summary>Validated named call. Mutable caller JSON can never change the authorized effect.</summary>
    public sealed class CapabilityCall
    {
        readonly JObject arguments;
        public readonly BehaviourCatalog.ActionDefinition Definition;
        public string NodeId {get;set;}
        public JObject Arguments=>(JObject)arguments.DeepClone();
        public string[] Resources=>CapabilityArguments.Resources(arguments,Definition.InputSchema);
        public BehaviourCatalog.Claim[] Claims=>Definition.Module.Claims(Arguments);
        public bool Instant=>Definition.Duration=="instant";
        public bool RequiresQuietRoom=>Definition.Module.RequiresQuietRoom(Arguments);
        public bool AwaitCompletion=>Definition.Duration=="completion";
        internal CapabilityCall(BehaviourCatalog.ActionDefinition definition,JObject arguments) {Definition=definition;this.arguments=(JObject)arguments.DeepClone();}
        public CapabilityCall Copy()=>new(Definition,arguments) {NodeId=NodeId};

    }
    public sealed class CapabilityContext
    {
        readonly RoomEditor editor;readonly AnimationWorkshop workshop;readonly bool followsWorkspace;
        public readonly WorkspaceHost Workspace;
        public RoomEditor Editor=>followsWorkspace?Workspace?.Current?.Editor:editor;
        public AnimationWorkshop Workshop=>followsWorkspace?(Editor?Editor.GetComponent<AnimationWorkshop>():null):workshop;
        public WorkspaceImport ArchiveImport=>Workspace?Workspace.Import:Editor?Editor.GetComponent<WorkspaceImport>():null;
        public WorkspaceExport ArchiveExport=>Workspace?Workspace.Export:Editor?Editor.GetComponent<WorkspaceExport>():null;
        public CapabilityContext(RoomEditor editor,AnimationWorkshop workshop) {this.editor=editor;this.workshop=workshop;Workspace=editor?editor.GetComponentInParent<WorkspaceHost>():null;}
        public CapabilityContext(WorkspaceHost workspace){Workspace=workspace;followsWorkspace=true;}
        public bool Target(JObject arguments,out RoomItem item,out string error,bool allowSpatial=false,bool allowUpperBody=false) {
            item=Editor?Editor.Find((string)arguments["target"]):null;error=null;
            if(!item) {error="An action target was removed; choose another target";return false;}
            if(item.Grab.isSelected||Workshop&&Workshop.ControlsTarget((string)arguments["target"])) {error="Release the target and stop authoring before running its rule";return false;}
            var tutor=item.GetComponent<MaestroAvatar>();
            if(tutor&&tutor.ModelBusy) {error="Wait for Maestro to finish loading";return false;}
            if(!allowSpatial&&tutor&&tutor.GetComponent<AvatarSpatialMotion>()?.Active==true) {error="Stop Maestro's current movement before starting a conflicting action";return false;}
            if(!allowUpperBody&&tutor&&tutor.UpperBodyActive) {error="An upper-body gesture is already running";return false;}
            return true;
        }
    }
    /// <summary>One execution's lifetime. Start failures may return an operation so Stop can clean up partial work.</summary>
    public abstract class CapabilityOperation
    {
        public abstract float Seconds {get;}
        public virtual RuleActionState State(out string error) {error=null;return RuleActionState.Ready;}
        public virtual void Tick() {}
        public virtual bool Complete(out string error) {error=null;return true;}
        public virtual void Stop(bool preservePlacement) {}
        // Describes effects that Stop cannot retract (for example a dispatched write).
        public virtual string InterruptionStatus=>null;
        public virtual JObject Result=>new JObject();
    }
    internal sealed class CompletedCapability : CapabilityOperation
    {
        readonly JObject result;
        public CompletedCapability(JObject result=null) {this.result=result==null?new JObject():(JObject)result.DeepClone();}
        public override float Seconds=>0;
        public override JObject Result=>(JObject)result.DeepClone();
    }
}
