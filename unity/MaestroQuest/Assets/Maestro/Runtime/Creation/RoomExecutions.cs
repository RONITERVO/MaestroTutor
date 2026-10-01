// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed class RoomExecutions
    {
        readonly RoomEditor editor;readonly WorkspaceHost workspace;
        string selectedId;
        public RoomExecutions(RoomEditor editor,WorkspaceHost workspace=null) {this.editor=editor;this.workspace=workspace??(editor?editor.GetComponentInParent<WorkspaceHost>():null);}
        RuleScheduler Room=>editor?editor.GetComponent<RoomRules>()?.Scheduler:null;
        RuleScheduler Maintenance=>workspace?workspace.Runtime?.Scheduler:null;
        RuleScheduler Resolve(JObject request)
        {
            if((string)request["operation"]=="start")return BehaviourCatalog.Action((string)request["call"]?["id"])?.Module.Domain=="workspace"&&Maintenance!=null?Maintenance:Room;
            string id=(string)request["runId"];
            if((string)request["operation"]=="recover")return Maintenance?.Receipts?.CanRecover((string)request["recoveryId"],out _)==true?Maintenance:Room;
            return Maintenance?.Invocation(id)!=null?Maintenance:Room;
        }
        void Select(RuleScheduler scheduler,string id){if(scheduler!=null&&ReferenceEquals(scheduler,Maintenance))workspace.Runtime.SelectedInvocation=id;else selectedId=id;}
        static bool Exact(JObject value,params string[] keys)=>value!=null&&value.Count==keys.Length&&keys.All(value.ContainsKey);
        static bool RunId(JToken value)=>value?.Type==JTokenType.String&&System.Text.RegularExpressions.Regex.IsMatch((string)value,"^[a-f0-9]{32}$");
        public static bool ValidRequest(JObject request)
        {
            if(request==null||request["operation"]?.Type!=JTokenType.String)return false;
            return (string)request["operation"] switch {
                "start"=>(Exact(request,"operation","call")||Exact(request,"operation","call","runId")&&RunId(request["runId"]))&&RoomCapabilityCatalog.ValidCall(request["call"] as JObject),
                "recover"=>Exact(request,"operation","recoveryId")&&RunId(request["recoveryId"]),
                "inspect" or "cancel"=>Exact(request,"operation","runId")&&RunId(request["runId"]),
                _=>false
            };
        }
        public static bool ValidWire(JObject command)=>Exact(command,"action","execution")&&(string)command["action"]=="execution"&&ValidRequest(command["execution"] as JObject);
        public static string[] Resources(JObject request)
        {
            if(!ValidRequest(request)||(string)request["operation"]!="start")return Array.Empty<string>();
            var call=(JObject)request["call"];var definition=BehaviourCatalog.Action((string)call["id"]);
            return definition==null?Array.Empty<string>():CapabilityArguments.Resources((JObject)call["arguments"],definition.InputSchema);
        }
        // Read an already consumed ID before target revision/readiness checks:
        // duplicate delivery reports its existing result even if the room changed.
        public bool Replay(JObject request,out bool accepted,out string error)
        {
            accepted=false;error=null;
            if(!ValidRequest(request)||(string)request["operation"]!="start"||request["runId"]==null)return false;
            var scheduler=Resolve(request);if(scheduler?.Receipts==null)return false;
            string id=(string)request["runId"];var prior=scheduler.Invocation(id);
            if(prior==null) {
                if(id==scheduler.Receipts.NextId)return false;
                error=scheduler.Receipts.Error??"This action ID is expired or unknown. Its effects cannot be established; it was not replayed.";Select(scheduler,id);return true;
            }
            Select(scheduler,id);
            if(!JToken.DeepEquals(prior["call"],request["call"])) {error="This action ID already belongs to a different call";return true;}
            error=(string)prior["status"];accepted=(string)prior["phase"] is "preparing" or "running" or "completed" or "cancelled";return true;
        }
        public bool Execute(JObject request,out string error)
        {
            error="Invalid action request";if(!ValidRequest(request))return false;
            var scheduler=Resolve(request);if(scheduler==null){error="Action runtime is not ready";return false;}
            var runtime=editor?editor.GetComponent<RoomRules>():null;bool maintenance=ReferenceEquals(scheduler,Maintenance);
            string operation=(string)request["operation"];
            if(Replay(request,out var replayed,out error))return replayed;
            if(operation=="recover"){bool recovered=scheduler.RecoverInvocations((string)request["recoveryId"],out error);if(recovered)Select(scheduler,null);return recovered;}
            if(operation=="start") {
                var call=(JObject)request["call"];
                if(!BehaviourCatalog.TryCall((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out error)||!(maintenance?workspace.Runtime.CanRun(step,out error):runtime.CanRun(step,out error)))return false;
                bool accepted=scheduler.Invoke(call,Time.unscaledTime,out var id,out error,(string)request["runId"]);
                if(id!=null) {Select(scheduler,id);error=(string)scheduler.Invocation(id)["status"];}
                return accepted;
            }
            string selected=(string)request["runId"];Select(scheduler,selected);
            if(operation=="cancel"&&!scheduler.CancelInvocation(selected,out error))return false;
            var result=scheduler.Invocation(selected);
            if(result==null) {error="This action outcome is unknown or no longer retained";return false;}
            error=(string)result["status"];return true;
        }
        public JObject Observe()
        {
            var room=Room?.ObserveInvocations(selectedId);if(Maintenance==null)return room;
            room??=new JObject {["selected"]=JValue.CreateNull(),["running"]=new JArray(),["outcomes"]=new JArray(),["nextRunId"]=JValue.CreateNull(),["storageError"]="The selected room is unavailable. Workspace maintenance remains available."};
            room["workspace"]=Maintenance.ObserveInvocations(workspace.Runtime.SelectedInvocation);return room;
        }
    }
}
