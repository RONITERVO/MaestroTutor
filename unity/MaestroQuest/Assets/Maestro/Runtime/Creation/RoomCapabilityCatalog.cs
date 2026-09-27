// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Read-only discovery and live preflight. Never edits, reserves or starts an action.</summary>
    public sealed class RoomCapabilityCatalog
    {
        public const int PageSize=6;
        readonly RoomEditor editor;
        JObject request;
        public RoomCapabilityCatalog(RoomEditor editor) {this.editor=editor;}
        static bool Exact(JObject value,params string[] keys)=>value!=null&&value.Count==keys.Length&&keys.All(value.ContainsKey);
        static bool Text(JToken value,int max)=>value?.Type==JTokenType.String&&((string)value).Length<=max&&!((string)value).Any(char.IsControl);
        static bool Version(JToken value)=>value?.Type==JTokenType.Integer&&(double)value>=1&&(double)value<=1000000;
        static bool Id(JToken value)=>Text(value,96)&&Regex.IsMatch((string)value,@"^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$");
        public static bool ValidRequest(JObject value)
        {
            if(value==null||value["operation"]?.Type!=JTokenType.String)return false;
            switch((string)value["operation"]) {
                case "search":return Exact(value,"operation","query","offset")&&Text(value["query"],80)&&value["offset"]?.Type==JTokenType.Integer&&(double)value["offset"]>=0&&(double)value["offset"]<=1000000;
                case "inspect":return Exact(value,"operation","capability","version")&&Id(value["capability"])&&Version(value["version"]);
                case "check":return Exact(value,"operation","call")&&ValidCall(value["call"] as JObject);
                default:return false;
            }
        }
        public static bool ValidCall(JObject call)
        {
            if(!Exact(call,"id","version","arguments")||!Id(call["id"])||!Version(call["version"])||call["arguments"] is not JObject arguments||arguments.ToString(Newtonsoft.Json.Formatting.None).Length>24000)return false;
            int count=0;
            bool Bounded(JToken token,int depth) {
                if(++count>4096||depth>12)return false;
                if(token is JObject obj)return obj.Properties().All(p=>p.Name.Length<=80&&!p.Name.Any(char.IsControl)&&Bounded(p.Value,depth+1));
                if(token is JArray array)return array.Count<=64&&array.All(x=>Bounded(x,depth+1));
                return token.Type switch {JTokenType.String=>Text(token,128),JTokenType.Integer or JTokenType.Float=>double.IsFinite((double)token)&&Math.Abs((double)token)<=1000000,JTokenType.Boolean or JTokenType.Null=>true,_=>false};
            }
            return Bounded(arguments,0);
        }
        public static bool ValidWire(JObject command)=>Exact(command,"action","catalog")&&(string)command["action"]=="catalog"&&command["catalog"] is JObject query&&ValidRequest(query);
        public bool Execute(JObject value,out string status)
        {
            status="Invalid capability query";if(!ValidRequest(value))return false;
            request=(JObject)value.DeepClone();status=(string)Observe()["status"];return true;
        }
        public JObject Observe()
        {
            if(request==null)return null;string operation=(string)request["operation"];
            if(operation=="search") {
                string query=((string)request["query"]).Trim();
                var terms=query.Split(' ',StringSplitOptions.RemoveEmptyEntries);
                var matches=BehaviourCatalog.Actions.Where(x=>terms.All(term=>(x.Id+" "+x.Label+" "+string.Join(" ",x.Requirements)).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
                int offset=Math.Min((int)request["offset"],Math.Max(0,(matches.Length-1)/PageSize*PageSize));
                return new JObject {["operation"]=operation,["query"]=query,["offset"]=offset,["pageSize"]=PageSize,["total"]=matches.Length,
                    ["entries"]=new JArray(matches.Skip(offset).Take(PageSize).Select(x=>new JObject {["id"]=x.Id,["version"]=x.Version,["label"]=x.Label})),
                    ["status"]=matches.Length==0?"No matching actions":"Found "+matches.Length+" actions. Search does not run them."};
            }
            if(operation=="inspect") {
                var definition=BehaviourCatalog.Action((string)request["capability"]);
                bool known=definition!=null&&definition.Version==(int)request["version"];
                return new JObject {["operation"]=operation,["capability"]=request["capability"].DeepClone(),["version"]=request["version"].DeepClone(),
                    ["definition"]=known?definition.ToJson():JValue.CreateNull(),["status"]=known?"Action definition. Check concrete arguments before running it.":"Unknown capability or unsupported version"};
            }
            var call=(JObject)request["call"];
            bool valid=BehaviourCatalog.TryInvocation((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out var error);
            bool available=false,occupied=false;string[] resources=Array.Empty<string>();
            if(valid) {
                resources=RuleDocument.Targets(step).Distinct().ToArray();
                var runtime=editor.GetComponent<RoomRules>();
                if(!runtime)error="Action runtime is not ready";
                else {
                    occupied=runtime.Scheduler?.ActionBusy(step)==true;
                    available=runtime.CanRun(step,out error);
                    if(available&&occupied) {available=false;error="A running action owns a required animation channel or object";}
                    if(available&&!runtime.Scheduler.HasCapacity) {available=false;error="All action slots are currently in use";}
                }
            }
            return new JObject {["operation"]="check",["call"]=call.DeepClone(),["valid"]=valid,["available"]=available,["occupied"]=occupied,
                ["resources"]=new JArray(resources),["status"]=available?"Ready now. This check does not reserve or start the action.":error??"Action unavailable"};
        }
    }
}
