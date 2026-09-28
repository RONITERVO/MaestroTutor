// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RoomSessionCapability : CapabilityModule
    {
        public override string Id=>"room.session";
        public override string Label=>"Temporary room";
        public override string Description=>"Explicit room-wide temporary play. Begin captures the current base and makes later object edits temporary immediately; completion waits for its baseline write. Failed baseline saves keep the fork available for Keep or Discard. Keep saves one captured snapshot as one saved Undo and continues temporary play; later edits remain temporary. Discard returns to the latest kept base and pauses physics. Programs, imported files and chat are separate. Pass the exact observed room sessionId (or bind room.sessionId); Begin/Discard return the new identity. Begin/Discard require other runs and authoring to finish. A dispatched Keep can finish after Stop; inspect save status instead of replaying it. Ordinary creations remain saved outside this mode.";
        public override string Duration=>"completion";
        public override string Ownership=>"roomSession";
        public override bool RequiresQuietRoom(JObject args)=>(string)args["operation"]!="keep";
        public override IReadOnlyList<string> Requirements=>new[]{"room.session.current","manual.released","storage.writable"};
        public override JObject InputSchema {get {var schema=Object(new JObject {["operation"]=Choice("begin","keep","discard"),["sessionId"]=Text("^[a-f0-9]{32}$",32)});schema["x-features"]=new JArray("temporaryRoom.v1");return schema;}}
        public override JObject OutputSchema=>Object(new JObject {["sessionId"]=Text("^[a-f0-9]{32}$",32),["saveId"]=Text("^([a-f0-9]{32})?$",32),["savedRevision"]=Number(0,1000000,true)});
        public override JObject Example=>new() {["operation"]="begin",["sessionId"]=new string('0',32)};
        static bool Quiet(CapabilityContext context,out string error) {
            error=null;var editor=context.Editor;
            if(!editor.CanChangeTemporaryBoundary(out error))return false;
            foreach(var data in editor.Snapshot().objects) {
                var item=editor.Find(data.id);if(!item)continue;
                var avatar=item.GetComponent<MaestroAvatar>();
                if(context.Workshop&&context.Workshop.ControlsTarget(data.id)||item.GetComponent<RigidRoomItem>()?.AnimationOwned==true||
                    item.GetComponent<AvatarSpatialMotion>()?.Active==true||avatar&&avatar.UpperBodyActive||item.GetComponent<RecipeObject>()?.IsPlaying==true) {
                    error="Stop movement and animation authoring before changing the room session";return false;
                }
            }
            return true;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error) {
            error=null;var editor=context.Editor;if(!editor) {error="Room editor is unavailable";return false;}
            if((string)args["sessionId"]!=editor.TemporarySessionId) {error="This room session changed; inspect it before trying again";return false;}
            bool begin=(string)args["operation"]=="begin";
            if(begin==editor.TemporaryRoom) {error=begin?"A temporary room is already active":"Start a temporary room first";return false;}
            if((string)args["operation"]!="discard"&&!editor.CanSaveRoom) {error="This room is unavailable for saving";return false;}
            if(editor.TemporarySavePending) {error="Wait for the dispatched snapshot to finish";return false;}
            return Quiet(context,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(context,args,out error))return false;
            var editor=context.Editor;string kind=(string)args["operation"];
            bool accepted=kind switch {
                "begin"=>editor.BeginTemporaryRoom(out error,stopAuthoring:false),
                "keep"=>editor.KeepTemporaryRoom(out error),
                _=>editor.DiscardTemporaryRoom(out error,stopAuthoring:false)
            };
            if(!accepted)return false;
            operation=kind!="discard"?new Saving(editor,editor.LastTemporarySave):new CompletedCapability(Result(editor.TemporarySessionId,editor.TemporarySaveId,editor.TemporarySaveRevision));return true;
        }
        static JObject Result(string sessionId,string saveId,int revision)=>new() {["sessionId"]=sessionId,["saveId"]=saveId,["savedRevision"]=revision};
        sealed class Saving : CapabilityOperation {
            readonly RoomEditor editor;readonly TemporarySaveReceipt receipt;
            public Saving(RoomEditor editor,TemporarySaveReceipt receipt) {this.editor=editor;this.receipt=receipt;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error) {
                if(editor)editor.PollTemporarySave();error=receipt.Error;
                return receipt.Pending?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;
            }
            public override string InterruptionStatus=>receipt.IsBaseline?(receipt.Pending?"Stopped waiting. The starting room snapshot may still save; temporary mode and later edits remain active. Inspect room save "+receipt.Id+" before continuing.":receipt.Error!=null?"Stopped; the starting room snapshot failed. Edits remain temporary; Save snapshot retries or Discard returns to the starting room.":"Stopped further actions; the starting room snapshot was saved and temporary mode remains active."):receipt.Pending?"Stopped waiting. The dispatched snapshot may still be saved; inspect room save "+receipt.Id+" before retrying.":receipt.Error!=null?"Stopped; the snapshot write failed. Temporary edits remain in the room.":"Stopped further actions; the captured room snapshot was already saved.";
            public override JObject Result=>RoomSessionCapability.Result(receipt.SessionId,receipt.Id,receipt.SavedRevision);
        }
        public static bool RunManual(RoomEditor editor,string kind,out string status) {
            var executions=new RoomExecutions(editor);
            return executions.Execute(new JObject {["operation"]="start",["call"]=new JObject {["id"]="room.session",["version"]=1,["arguments"]=new JObject {["operation"]=kind,["sessionId"]=editor.TemporarySessionId}}},out status);
        }
    }
}
