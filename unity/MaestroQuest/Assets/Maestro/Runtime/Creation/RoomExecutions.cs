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
                "start"=>Exact(request,"operation","call")&&RoomCapabilityCatalog.ValidCall(request["call"] as JObject),
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
        public bool Execute(JObject request,out string error)
        {
            error="Invalid action request";if(!ValidRequest(request))return false;
            var runtime=editor.GetComponent<RoomRules>();if(!runtime||runtime.Scheduler==null) {error="Action runtime is not ready";return false;}
            string operation=(string)request["operation"];
            if(operation=="start") {
                var call=(JObject)request["call"];
                if(!BehaviourCatalog.TryInvocation((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out error)||!runtime.CanRun(step,out error))return false;
                bool accepted=runtime.Scheduler.Invoke(call,Time.unscaledTime,out var id,out error);
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
