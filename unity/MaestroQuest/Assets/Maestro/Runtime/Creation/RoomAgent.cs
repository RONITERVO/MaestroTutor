// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Avatar;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class RoomAgentCommand
    {
        public string action, target, reference, name, kind, partId;
        public Vector3 position;
        public bool atPosition, visible;
        public float scale=1;
        public Color color=Color.white;
        public RoomRecipe recipe;
        public RuleRequest rule;
        public RoomMotionQuery motionQuery;
        [NonSerialized] public Newtonsoft.Json.Linq.JObject catalog,execution;
        public AvatarActivityRequest activities;
        public string operation,motionId;
        public ObjectPhysicsSettings physics;
        public AvatarMovementSettings movement;
    }
    [Serializable] public sealed class RoomObjectCondition { public string id; public int revision; }
    [Serializable] public sealed class RoomInspection { public string id,partId; public int objectRevision; public RoomRecipe recipe; }
    [Serializable] public sealed class RoomAgentRequest
    {
        public int version, sequence, sceneRevision;
        public string session;
        public RoomAgentCommand[] commands;
        public RoomObjectCondition[] conditions;
    }
    [Serializable] public sealed class RoomAgentSnapshot { public string clientId,session,captureAck; public RoomAgentRequest request; }
    [Serializable] public sealed class RoomAgentObject
    {
        public string id,name,kind,runtimeState,positionSource;
        public int objectRevision;
        public Vector3 position;
        public float scale;
        public Color color;
        public bool animated,held,simulating;
        public ObjectPhysicsSettings physics;
        public AvatarMovementSettings movement;
    }
    [Serializable] public sealed class RoomAgentState
    {
        public int version=1,revision,sceneRevision,ack;
        public string session,status,selectedId;
        public bool ok,canUndo,canRedo,physicsRunning,visible;
        public RoomInspection inspection;
        public ConstructionSelectionView constructionSelection;
        public ConstructionManipulationView constructionManipulation;
        public string workspaceView;
        public RuleView rules;
        public RoomMotionView motions;
        [NonSerialized] public Newtonsoft.Json.Linq.JObject catalog,execution,capture,chatImage;
        public string[] capabilities;
        public RoomPhysicsObservation physics;
        public TemporaryRoomView temporaryRoom;
        public Interaction.RoomOwnershipView ownership;
        public AvatarMovementObservation avatar;
        public AvatarWalkObservation walk;
        public ActivityProfileView activityProfile;
        public RoomAgentObject[] objects;
        public string[] created=Array.Empty<string>();
    }
    /// <summary>Shared native commands return actual receipts. Only saved edits enter the journal.</summary>
    public sealed class RoomAgentExecutor
    {
        readonly RoomEditor editor;
        public bool WorkspaceVisible { get; private set; }
        public bool RulesFocused { get; private set; }
        public string InspectionId { get; private set; }
        public string InspectedPart { get; private set; }
        public RoomMotionSearch Motions { get; }
        public RoomCapabilityCatalog Catalog { get; }
        public RoomExecutions Executions { get; }
        public RoomAgentExecutor(RoomEditor source,Maestro.Quest.Persistence.WorkspaceHost host=null) { editor=source;Motions=new RoomMotionSearch(source);Catalog=new RoomCapabilityCatalog(source,host);Executions=new RoomExecutions(source,host); }
        bool Preconditions(RoomAgentRequest request,out string error)
        {
            error="The target changed; inspect its latest state before retrying.";
            if(request.version==1) return editor&&request.sceneRevision==editor.Revision;
            if(request.conditions==null || request.conditions.Length>Maestro.Quest.Programs.BehaviourProgram.MaximumResources || request.conditions.Any(x=>x==null || x.id==null) || request.conditions.Select(x=>x.id).Distinct().Count()!=request.conditions.Length) return false;
            var aliases=new HashSet<string>();
            foreach(var command in request.commands)
            {
                if(command==null) return false;
                if(command.action=="create") { if(command.reference!=null) aliases.Add(command.reference); continue; }
                if(command.action=="undo" || command.action=="redo" || command.action=="physicsRun") { if(request.sceneRevision!=editor.Revision) return false; continue; }
                var targets=command.action=="execution"?RoomExecutions.Resources(command.execution):command.target==null?Array.Empty<string>():new[] {command.target};
                foreach(var target in targets) {
                    if(aliases.Contains(target))continue;
                    var condition=request.conditions.FirstOrDefault(x=>x.id==target);
                    if(condition==null || condition.revision<=0 || !editor||condition.revision!=editor.ObjectRevision(target)) return false;
                }
            }
            return true;
        }
        public bool Execute(RoomAgentRequest request,out string status,out string[] created)
        {
            created=Array.Empty<string>(); status="Invalid room request";
            if(request == null || (request.version != 1 && request.version != 2) || request.commands == null || request.commands.Length<1 || request.commands.Length>8) return false;
            var commands=request.commands;
            if(!editor&&!commands.Any(command=>command?.action=="execution")) {
                if(commands.Length==1&&commands[0]?.action=="catalog")return Catalog.Execute(commands[0].catalog,out status);
                if(commands.Length==1&&commands[0]?.action=="workspace") {WorkspaceVisible=commands[0].visible;status=WorkspaceVisible?"Workspace inspection opened":"Returned to chat";return true;}
                status="The selected workspace is unavailable. Its saved files are preserved; room actions cannot run.";return false;
            }
            if(commands.Any(command=>command?.action=="execution")) {
                if(commands.Length!=1||!RoomExecutions.ValidRequest(commands[0].execution)) {status="Action requests must be valid and sent on their own";return false;}
                if((string)commands[0].execution["operation"]=="start") {
                    if(request.version!=2) {status="Inspect the latest targets before running an action";return false;}
                    if(Executions.Replay(commands[0].execution,out var replayed,out status))return replayed;
                    if(!Preconditions(request,out status))return false;
                }
                return Executions.Execute(commands[0].execution,out status);
            }
            if(commands.Any(command=>command?.action=="catalog")) {
                if(commands.Length!=1) {status="Capability queries must be sent on their own";return false;}
                return Catalog.Execute(commands[0].catalog,out status);
            }
            if(commands.Any(command=>command?.action=="motions")) {
                if(commands.Length!=1) {status="Motion searches must be sent on their own";return false;}
                return Motions.Execute(commands[0],out status);
            }
            if(commands.Any(command=>command?.action=="avatarActivities")) {
                if(commands.Length!=1) {status="Tutor-state assignments must be sent on their own";return false;}
                return AvatarActivityActions.Execute(editor,commands[0].activities,out status);
            }
            if(commands.Any(command => command==null || RoomControls.IsControl(command.action) && !RoomControls.ValidCommand(command))) return false;
            if(commands.Any(command => RoomControls.Runtime(command.action)) && commands.Length!=1) {status="Runtime controls must be sent on their own";return false;}
            if(commands.Length==1 && RoomControls.Runtime(commands[0].action)) {
                if(!Preconditions(request,out status))return false;
                var command=commands[0];
                if(command.action=="avatarMotion")return RoomControls.AvatarMotion(editor,command.operation,out status);
                var physics=editor.PhysicsWorld;if(!physics) {status="Room physics is unavailable";return false;}
                return physics.SetRunning(command.operation=="start",out status);
            }
            if(commands.Length==1 && commands[0]?.action=="rules") {
                var workshop=editor.GetComponent<RuleWorkshop>();if(!workshop) {status="Behaviours are unavailable in this room";return false;}
                bool accepted=workshop.Execute(commands[0].rule,out status,out created);
                if(accepted) RulesFocused=true;return accepted;
            }
            if(commands.Length==1 && commands[0]?.action=="workspace") { WorkspaceVisible=commands[0].visible; if(WorkspaceVisible) InspectionId=editor.SelectedId; status=WorkspaceVisible ? "Workspace opened on the book" : "Returned to chat"; return true; }
            if(commands.Length==1 && commands[0]?.action=="inspect")
            {
                RulesFocused=false;
                if(!editor.TryGetLiveObject(commands[0].target,out var target,out status))return false;
                string partId=commands[0].partId;var geometry=target.GetComponent<RecipeObject>();
                if(!string.IsNullOrEmpty(partId) && (!geometry || !geometry.Part(partId))) {status="That recipe part no longer exists";return false;}
                foreach(var recipeObject in editor.GetComponentsInChildren<RecipeObject>())recipeObject.Highlight(null);
                InspectionId=commands[0].target;InspectedPart=partId;editor.Select(target);geometry?.Highlight(partId);status="Object inspected";return true;
            }
            if(!Preconditions(request,out status)) return false;
            if(editor.AnyHeld) {status="Release the held object before editing."; return false;}
            if(commands.Length==1 && (commands[0]?.action=="play" || commands[0]?.action=="stop"))
            {
                editor.PrepareAgentEdit();if(!Preconditions(request,out status))return false;
                var data=editor.Read(commands[0].target); var geometry=editor.Find(commands[0].target)?.GetComponent<RecipeObject>();
                if(data?.recipe==null || data.recipe.tracks.Length==0 || !geometry) {status="This object has no recipe animation";return false;}
                bool play=commands[0].action=="play";
                if(data.recipe.playing!=play) { data.recipe.playing=play; if(!editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out status)) return false; }
                if(play) geometry.Restart(); else geometry.Stop(); status=play ? "Recipe animation started" : "Recipe animation stopped"; return true;
            }
            // Stop active authoring, then recheck the exact targets before copying mutable data.
            editor.PrepareAgentEdit(); if(!Preconditions(request,out status)) return false;
            if(commands.Length==1 && (commands[0]?.action=="undo" || commands[0]?.action=="redo"))
            {
                bool undo=commands[0].action=="undo";
                if(undo ? !editor.CanUndo : !editor.CanRedo) {status="There is no edit to "+commands[0].action; return false;}
                if(undo) editor.Undo(); else editor.Redo(); status=editor.Status; return status=="Undone" || status=="Redone";
            }
            var changes=new Dictionary<string,RoomObjectData>(); var removed=new HashSet<string>(); var refs=new Dictionary<string,string>();
            var added=new List<string>();
            foreach(var command in commands)
            {
                if(command==null) return false;
                if(command.action=="create")
                {
                    if(!RoomRecipe.ValidId(command.reference) || refs.ContainsKey(command.reference) || command.name==null || command.name.Length>80 || command.name.Any(char.IsControl)) return false;
                    RoomObjectKind kind;
                    switch(command.kind) {
                        case "block": kind=RoomObjectKind.Block;break; case "ball": kind=RoomObjectKind.Ball;break;
                        case "cylinder": kind=RoomObjectKind.Cylinder;break; case "recipe": case "boxRobot": kind=RoomObjectKind.Assembly;break;
                        default: status="This creation type is unavailable";return false;
                    }
                    string id=Guid.NewGuid().ToString("N"); refs.Add(command.reference,id); added.Add(id);
                    changes[id]=new RoomObjectData { id=id,name=command.name,kind=kind,position=command.atPosition ? command.position : editor.CreationPosition,
                        scale=command.scale,color=command.color,physics=kind==RoomObjectKind.Ball ? Interaction.ItemPhysics.Bouncy : kind==RoomObjectKind.Assembly ? Interaction.ItemPhysics.Fixed : Interaction.ItemPhysics.Solid,
                        recipe=command.kind=="boxRobot" ? RecipeTemplates.BoxRobot(true) : command.kind=="recipe" ? command.recipe?.Copy() : null };
                    continue;
                }
                string target=command.target; if(target!=null && refs.TryGetValue(target,out var mapped)) target=mapped;
                if(target==null || removed.Contains(target)) {status="The target no longer exists"; return false;}
                var data=changes.TryGetValue(target,out var pending) ? pending : editor.Read(target);
                if(data==null) {status="The target no longer exists";return false;}
                if(ScanDrawingAnchor.Has(data)&&command.action!="delete"){status="Scanned ink uses layer/ink controls; explicitly rebind its anchor instead of loose-object edits";return false;}
                switch(command.action)
                {
                    case "physicsSettings": if(!RoomControls.SetPhysics(data,command.physics,out status))return false;break;
                    case "avatarWalk": if(!AvatarWalkSelection.Apply(editor,data,command.motionId,out status))return false;break;
                    case "avatarSettings": if(!RoomControls.SetMovement(data,command.movement,out status))return false;break;
                    case "move": data.position=command.position; break;
                    case "resize": data.scale=command.scale; break;
                    case "paint": if(data.IsBuiltIn) {status="Painting the book or tutor is unavailable";return false;} if(!RoomEditor.PaintAppearance(data,command.color,out status))return false;break;
                    case "recipe": if(data.kind!=RoomObjectKind.Assembly || command.recipe==null) return false; if(!editor.ApplyRecipeAppearance(data,command.recipe.Copy(),out status))return false;break;
                    case "delete": if(data.IsBuiltIn) {status="The book and Maestro must remain in the room";return false;} removed.Add(target);changes.Remove(target);continue;
                    default: status="This room action is unavailable"; return false;
                }
                changes[target]=data;
            }
            // Validate the entire candidate before touching any scene object or interrupting authoring.
            var candidate=editor.Snapshot(); candidate.objects=candidate.objects.Where(x=>!removed.Contains(x.id) && !changes.ContainsKey(x.id)).Concat(changes.Values).ToArray();
            if(!candidate.Validate(out status)) return false;
            if(!editor.ApplyAgentEdit(editor.Revision,changes.Values.ToArray(),removed.ToArray(),out status)) return false;
            created=added.Where(id=>!removed.Contains(id)).ToArray(); status="Completed "+commands.Length+" room actions. One Undo restores the preceding scene edit."; return true;
        }
    }
    public sealed class RoomAgentInbox
    {
        string clientId;
        public string Session { get; private set; }=Guid.NewGuid().ToString("N");
        public int Ack { get; private set; }
        public void Reset() {Session=Guid.NewGuid().ToString("N");Ack=0;}
        public bool TryAccept(RoomAgentSnapshot snapshot,out RoomAgentRequest request)
        {
            request=null;
            if(snapshot==null || !Guid.TryParseExact(snapshot.clientId,"N",out _))return false;
            // Reopening or cancelling starts a new handshake. A cancelled sequence
            // can never be reused against a late receipt from the preceding client.
            if(clientId!=snapshot.clientId) {clientId=snapshot.clientId;Reset();return false;}
            var candidate=snapshot.request;
            if(snapshot.session!=Session || candidate==null || candidate.session!=Session || candidate.sequence!=Ack+1 || candidate.sequence<=Ack)return false;
            Ack=candidate.sequence;request=candidate;return true;
        }
    }
    public sealed class RoomAgent : MonoBehaviour
    {
        RoomEditor editor;
        NativeBookBrowser browser;
        RoomAgentExecutor executor;
        readonly RoomAgentInbox inbox=new();
        string status="Room actions ready",bindingStatus="Room actions ready";
        int revision;
        float next;
        bool connected,ok=true;
        string[] created=Array.Empty<string>();
        string lastInspected,captureAck;
        internal Newtonsoft.Json.Linq.JObject CapturePayload=>editor?editor.ViewCapturePayload(inbox.Session,captureAck):null;
        public void Initialize(RoomEditor source,NativeBookBrowser book) {browser=book;Bind(source,source?"Room actions ready":"Opening workspace");}
        internal void WorkspaceStatus(string message){status=bindingStatus=message;next=0;}
        internal void Bind(RoomEditor source,string message)
        {
            // An editor replacement always invalidates old requests, even if object IDs/revisions
            // happen to match the incoming document. The browser and chat themselves stay alive.
            if(editor){editor.ClearViewCapture();editor.GetComponent<Maestro.Quest.Imports.ImageImportWorkshop>()?.ClearChat();}if(source)source.ClearViewCapture();captureAck=null;
            editor=source;executor=new RoomAgentExecutor(source,GetComponent<Maestro.Quest.Persistence.WorkspaceHost>());inbox.Reset();revision=0;next=0;
            status=bindingStatus=message;connected=false;ok=source;created=Array.Empty<string>();lastInspected=null;
        }
        public bool OpenRules(string id,out string error) {
            error="The book workspace is unavailable";if(executor==null)return false;
            if(!executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=new RuleRequest {action="inspect",target=id}}}},out error,out _))return false;
            // Local navigation must not replace acknowledgements/results for an in-flight agent request.
            return executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="workspace",visible=true}}},out error,out _);
        }
        void Update()
        {
            if(executor==null || !browser) return;
            if(browser.Snapshot==null)
            {
                if(connected) {if(editor){editor.ClearViewCapture();editor.GetComponent<Maestro.Quest.Imports.ImageImportWorkshop>()?.ClearChat();}captureAck=null;inbox.Reset();revision=0;connected=false;created=Array.Empty<string>();status=editor?"Room session reopened":bindingStatus;}
                return;
            }
            connected=true;
            if(Time.unscaledTime<next) return;next=Time.unscaledTime+.25f;
            Receive(browser.ReadRoomAgentSnapshot());
            browser.PublishRoomAgentState(RoomAgentWire.Serialize(Observe()));
            var capture=CapturePayload;if(capture!=null)browser.PublishRoomCapture(capture.ToString(Newtonsoft.Json.Formatting.None));
        }
        internal void Receive(string json)
        {
            if(executor==null)return;
            if(!string.IsNullOrEmpty(json) && json.Length<=32768)
            {
                try {
                    using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json)) {MaxDepth=64,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
                    var raw=Newtonsoft.Json.Linq.JObject.Load(reader,new Newtonsoft.Json.Linq.JsonLoadSettings {DuplicatePropertyNameHandling=Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error});
                    if(reader.Read())throw new ArgumentException("Extra room data");
                    var requestJson=raw["request"];
                    var snapshot=JsonUtility.FromJson<RoomAgentSnapshot>(json);
                    string previousSession=inbox.Session;bool accepted=inbox.TryAccept(snapshot,out var request);
                    if(previousSession!=inbox.Session){if(editor){editor.ClearViewCapture();editor.GetComponent<Maestro.Quest.Imports.ImageImportWorkshop>()?.ClearChat();}captureAck=null;}
                    if(editor&&snapshot!=null&&snapshot.session==inbox.Session)editor.GetComponent<Maestro.Quest.Imports.ImageImportWorkshop>()?.SyncChat(inbox.Session,raw["images"] as Newtonsoft.Json.Linq.JObject);
                    if(snapshot!=null&&snapshot.session==inbox.Session&&Guid.TryParseExact(snapshot.captureAck,"N",out _))captureAck=snapshot.captureAck;
                    if(accepted) {
                        if(RoomControls.ValidWire(requestJson?.ToString() ?? "") && RoomAgentWire.PopulateStructured(request,requestJson as Newtonsoft.Json.Linq.JObject)) ok=executor.Execute(request,out status,out created);
                        else {ok=false;status="Invalid room control arguments";created=Array.Empty<string>();}
                    }
                } catch(Exception ex) when(ex is ArgumentException || ex is Newtonsoft.Json.JsonException) { /* Invalid or partial messages never execute. */ }
            }
        }
        public RoomAgentState Observe()
        {
            if(!editor)return new RoomAgentState {session=inbox.Session,revision=++revision,sceneRevision=1,ack=inbox.Ack,ok=ok,status=status,created=created,
                objects=Array.Empty<RoomAgentObject>(),capabilities=RoomControls.WorkspaceCapabilities(GetComponent<Maestro.Quest.Persistence.WorkspaceHost>()).Concat(new[]{"catalog.v1","catalogVocabulary.v1"}).Concat(RoomGuideCatalog.Available?new[]{"catalogGuides.v1"}:Array.Empty<string>()).Distinct().ToArray(),execution=executor?.Executions.Observe(),visible=executor?.WorkspaceVisible==true,
                workspaceView="objects",catalog=executor?.Catalog.Observe()};
            if(executor.WorkspaceVisible && editor.SelectedId!=null) lastInspected=editor.SelectedId;
            else if(executor.InspectionId!=null) lastInspected=executor.InspectionId;
            var inspected=editor.Read(lastInspected);
            return new RoomAgentState { session=inbox.Session,revision=++revision,sceneRevision=editor.Revision,ack=inbox.Ack,ok=ok,status=status,created=created,
                chatImage=editor.GetComponent<Maestro.Quest.Imports.ImageImportWorkshop>()?.Chat.Request(),capture=editor.ViewCaptureMetadata,ownership=editor.Ownership.Observe(),temporaryRoom=editor.ObserveTemporaryRoom(),motions=executor.Motions.Observe(),catalog=executor.Catalog.Observe(),execution=executor.Executions.Observe(),capabilities=RoomControls.Capabilities(editor),physics=RoomControls.ObservePhysics(editor),avatar=RoomControls.ObserveAvatar(editor),walk=AvatarWalkSelection.Observe(editor),activityProfile=AvatarActivityActions.Observe(editor),
                visible=executor.WorkspaceVisible,workspaceView=executor.RulesFocused ? "rules" : "objects",rules=editor.GetComponent<RuleWorkshop>()?.Observe(executor.RulesFocused),inspection=inspected==null || executor.RulesFocused ? null : new RoomInspection {id=inspected.id,partId=executor.InspectionId==inspected.id ? executor.InspectedPart : null,objectRevision=editor.ObjectRevision(inspected.id),recipe=editor.RecipeForEditing(inspected)},
                constructionSelection=editor.ObserveConstructionSelection(),constructionManipulation=editor.ObserveConstructionManipulation(),selectedId=editor.SelectedId,canUndo=editor.CanUndo,canRedo=editor.CanRedo,physicsRunning=editor.PhysicsWorld && editor.PhysicsWorld.Running,
                objects=editor.ObserveObjects() };
        }
    }
}
