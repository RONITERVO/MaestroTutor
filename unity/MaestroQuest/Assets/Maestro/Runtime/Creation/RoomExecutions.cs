// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed class RoomExecutions
    {
        readonly RoomEditor editor;
        string selectedId;
        public RoomExecutions(RoomEditor editor) {this.editor=editor;}
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
            var scheduler=editor.GetComponent<RoomRules>()?.Scheduler;if(scheduler?.Receipts==null)return false;
            string id=(string)request["runId"];var prior=scheduler.Invocation(id);
            if(prior==null) {
                if(id==scheduler.Receipts.NextId)return false;
                error=scheduler.Receipts.Error??"This action ID is expired or unknown. Its effects cannot be established; it was not replayed.";selectedId=id;return true;
            }
            selectedId=id;
            if(!JToken.DeepEquals(prior["call"],request["call"])) {error="This action ID already belongs to a different call";return true;}
            error=(string)prior["status"];accepted=(string)prior["phase"] is "preparing" or "running" or "completed" or "cancelled";return true;
        }
        public bool Execute(JObject request,out string error)
        {
            error="Invalid action request";if(!ValidRequest(request))return false;
            var runtime=editor.GetComponent<RoomRules>();if(!runtime||runtime.Scheduler==null) {error="Action runtime is not ready";return false;}
            string operation=(string)request["operation"];
            if(Replay(request,out var replayed,out error))return replayed;
            if(operation=="recover"){bool recovered=runtime.Scheduler.RecoverInvocations((string)request["recoveryId"],out error);if(recovered)selectedId=null;return recovered;}
            if(operation=="start") {
                var call=(JObject)request["call"];
                if(!BehaviourCatalog.TryInvocation((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out error)||!runtime.CanRun(step,out error))return false;
                bool accepted=runtime.Scheduler.Invoke(call,Time.unscaledTime,out var id,out error,(string)request["runId"]);
                if(id!=null) {selectedId=id;error=(string)runtime.Scheduler.Invocation(id)["status"];}
                return accepted;
            }
            selectedId=(string)request["runId"];
            if(operation=="cancel"&&!runtime.Scheduler.CancelInvocation(selectedId,out error))return false;
            var result=runtime.Scheduler.Invocation(selectedId);
            if(result==null) {error="This action outcome is unknown or no longer retained";return false;}
            error=(string)result["status"];return true;
        }
        public JObject Observe()=>editor.GetComponent<RoomRules>()?.Scheduler?.ObserveInvocations(selectedId);
    }
}
