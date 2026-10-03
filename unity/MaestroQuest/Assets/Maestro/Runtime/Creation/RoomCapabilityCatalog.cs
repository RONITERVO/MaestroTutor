// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Read-only discovery and live preflight. Never edits, reserves or starts an action.</summary>
    public sealed class RoomCapabilityCatalog
    {
        public const int PageSize=6;
        readonly RoomEditor editor;readonly WorkspaceHost workspace;
        JObject request,cached;
        Entry inspected;
        int moduleRevision;bool moduleReady,modulePending;string moduleNotice;
        public RoomCapabilityCatalog(RoomEditor editor,WorkspaceHost workspace=null) {this.editor=editor;this.workspace=workspace??(editor?editor.GetComponentInParent<WorkspaceHost>():null);}
        static bool Exact(JObject value,params string[] keys)=>value!=null&&value.Count==keys.Length&&keys.All(value.ContainsKey);
        static bool Text(JToken value,int max)=>value?.Type==JTokenType.String&&((string)value).Length<=max&&!((string)value).Any(char.IsControl);
        static bool Version(JToken value)=>value?.Type==JTokenType.Integer&&(double)value>=1&&(double)value<=1000000;
        static bool Id(JToken value)=>Text(value,96)&&Regex.IsMatch((string)value,@"^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$");
        static bool QueryKeys(JObject value,params string[] keys) {
            if(!value.ContainsKey("category"))return Exact(value,keys);
            return value["category"]?.Type==JTokenType.String&&new[]{"actions","events","facts","modules"}.Contains((string)value["category"])&&Exact(value,keys.Concat(new[]{"category"}).ToArray());
        }
        public static bool ValidRequest(JObject value)
        {
            if(value==null||value["operation"]?.Type!=JTokenType.String)return false;
            switch((string)value["operation"]) {
                case "search":return QueryKeys(value,"operation","query","offset")&&Text(value["query"],80)&&value["offset"]?.Type==JTokenType.Integer&&(double)value["offset"]>=0&&(double)value["offset"]<=1000000;
                case "inspect":return (value.ContainsKey("arguments")?value["category"]?.Type==JTokenType.String&&(string)value["category"]=="facts"&&QueryKeys(value,"operation","capability","version","arguments")&&ValidCall(new JObject {["id"]=value["capability"]?.DeepClone(),["version"]=value["version"]?.DeepClone(),["arguments"]=value["arguments"].DeepClone()}):QueryKeys(value,"operation","capability","version"))&&((string)value["category"]=="modules"?value["capability"]?.Type==JTokenType.String&&ProgramModuleLibrary.ValidHash((string)value["capability"]):Id(value["capability"]))&&Version(value["version"]);
                case "check":return Exact(value,"operation","call")&&ValidCall(value["call"] as JObject);
                default:return false;
            }
        }
        public static bool ValidCall(JObject call)
        {
            if(!Exact(call,"id","version","arguments")||!Id(call["id"])||!Version(call["version"])||call["arguments"] is not JObject arguments||arguments.ToString(Newtonsoft.Json.Formatting.None).Length>24000)return false;
            int count=0;
            bool Bounded(JToken token,int depth,JObject schema=null) {
                schema=schema==null?null:CapabilitySchema.Resolve(schema,token);
                if((string)schema?["format"]=="programMemoryValue")return Text(token,8192);
                if((string)schema?["format"]=="programModule")return token is JObject module&&ProgramModuleLibrary.ValidRecord(module);
                if(++count>4096||depth>12)return false;
                if(token is JObject obj)return obj.Properties().All(p=>p.Name.Length<=80&&!p.Name.Any(char.IsControl)&&Bounded(p.Value,depth+1,schema?["properties"]?[p.Name] as JObject));
                if(token is JArray array)return array.Count<=64&&array.All(x=>Bounded(x,depth+1,schema?["items"] as JObject));
                return token.Type switch {JTokenType.String=>Text(token,128),JTokenType.Integer or JTokenType.Float=>ProgramValue.ValidNumber((double)token),JTokenType.Boolean or JTokenType.Null=>true,_=>false};
            }
            return Bounded(arguments,0,BehaviourCatalog.Action((string)call["id"])?.InputSchema);
        }
        public static bool ValidWire(JObject command)=>Exact(command,"action","catalog")&&(string)command["action"]=="catalog"&&command["catalog"] is JObject query&&ValidRequest(query);
        public bool Execute(JObject value,out string status)
        {
            status="Invalid capability query";if(!ValidRequest(value))return false;
            request=(JObject)value.DeepClone();cached=null;inspected=null;status=(string)Observe()["status"];return true;
        }
        sealed class Entry {
            public string Id,Label,Search;public int Version;public Func<JObject> Definition;
        }
        // Descriptions are native vocabulary data. Search pages never expand every
        // schema into the observation or agent prompt.
        static Entry[] Entries(string category)=>category switch {
            "events"=>BehaviourCatalog.Events.Select(x=>new Entry {Id=x.Id,Version=x.Version,Label=x.Label,Search=x.Id+" "+x.Label+" "+x.Description,Definition=x.ToJson}).ToArray(),
            "facts"=>BehaviourCatalog.Facts.Select(x=>new Entry {Id=x.Id,Version=x.Version,Label=x.Label,Search=x.Id+" "+x.Label+" "+x.Description+" "+x.Type,Definition=x.ToJson}).ToArray(),
            _=>BehaviourCatalog.Actions.Select(x=>new Entry {Id=x.Id,Version=x.Version,Label=x.Label,Search=x.SearchText,Definition=x.ToJson}).ToArray()
        };
        static readonly System.Collections.Generic.Dictionary<string,Entry[]> vocabulary=new() {
            ["actions"]=Entries("actions"),["events"]=Entries("events"),["facts"]=Entries("facts")
        };
        JObject Scoped(JObject result) {if(request["category"]!=null)result["category"]=request["category"].DeepClone();return result;}
        JObject Cache(JObject value) {cached=(JObject)value.DeepClone();return value;}
        JObject ReadFact(JObject result,Entry entry) {
            var runtime=editor?editor.GetComponent<RoomRules>():null;ProgramValue value=default;
            var definition=entry==null?null:BehaviourCatalog.Fact(entry.Id);var arguments=request["arguments"] as JObject;
            if(request.ContainsKey("arguments"))result["arguments"]=request["arguments"].DeepClone();
            bool available=definition!=null&&(definition.Domain=="workspace"&&workspace?workspace.Runtime.TryRead(entry.Id,entry.Version,arguments,out value):runtime&&runtime.TryReadFact(entry.Id,entry.Version,arguments,out value))&&definition.ValidValue(value);
            result["available"]=available;result["value"]=available?JToken.FromObject(value.Value):JValue.CreateNull();
            if(entry!=null)result["status"]=available?"Current fact value. Reading does not change the room.":definition.Parameterized&&arguments==null?"Choose fact arguments to read a value.":!definition.ValidArguments(entry.Version,arguments,out _)?"Fact arguments do not match this definition.":"Fact value is currently unavailable; do not treat it as false or zero.";
            return result;
        }
        JObject ObserveModules(string operation) {
            var library=editor?editor.GetComponent<RuleWorkshop>()?.Modules:null;library?.Poll();
            bool ready=library?.Ready==true,pending=library?.Pending==true;int revision=library?.Revision??1;
            string notice=library==null?"Module library is unavailable":!ready?"Module library is loading":library.Error;
            if(cached!=null&&moduleRevision==revision&&moduleReady==ready&&modulePending==pending&&moduleNotice==notice)return (JObject)cached.DeepClone();
            moduleRevision=revision;moduleReady=ready;modulePending=pending;moduleNotice=notice;
            var common=new JObject {["operation"]=operation,["category"]="modules",["revision"]=revision,["ready"]=ready,["pending"]=pending};
            if(operation=="search") {
                string query=((string)request["query"]).Trim();var entries=library?.Search(query)??Array.Empty<ProgramModuleLibrary.Entry>();int offset=Math.Min((int)request["offset"],Math.Max(0,(entries.Length-1)/PageSize*PageSize));
                common["query"]=query;common["offset"]=offset;common["pageSize"]=PageSize;common["total"]=entries.Length;
                common["entries"]=new JArray(entries.Skip(offset).Take(PageSize).Select(e=>new JObject {["id"]=e.Hash,["version"]=1,["label"]=e.Name}));common["status"]=notice??"Found "+entries.Length+" reusable modules. Search does not publish, import or run anything.";
            }else {
                string hash=(string)request["capability"];var entry=(int)request["version"]==1?library?.Inspect(hash):null;
                common["capability"]=hash;common["version"]=request["version"].DeepClone();common["definition"]=entry?.ReadDefinition()??(JToken)JValue.CreateNull();common["included"]=entry?.Included==true;
                common["status"]=notice??entry?.Error??(entry==null?"Unknown module content ID or unsupported version":entry.Included?"Included module. Copy/edit its source to remix; pinned imports keep this exact definition. Nothing starts automatically.":"Pinned module definition. Importing changes a draft; applying never starts it.");
            }
            return Cache(common);
        }
        public JObject Observe()
        {
            if(request==null)return null;string operation=(string)request["operation"],category=(string)request["category"]??"actions";
            if(category=="modules")return ObserveModules(operation);
            // Search/definition expansion happens once per query. Only fact values
            // and action readiness are live, through their existing native readers.
            if(cached!=null) {var copy=(JObject)cached.DeepClone();return category=="facts"&&operation=="inspect"?ReadFact(copy,inspected):copy;}
            if(operation=="search") {
                string query=((string)request["query"]).Trim();
                var terms=query.Split(' ',StringSplitOptions.RemoveEmptyEntries);
                var matches=vocabulary[category].Where(x=>terms.All(term=>x.Search.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
                int offset=Math.Min((int)request["offset"],Math.Max(0,(matches.Length-1)/PageSize*PageSize));
                return Cache(Scoped(new JObject {["operation"]=operation,["query"]=query,["offset"]=offset,["pageSize"]=PageSize,["total"]=matches.Length,
                    ["entries"]=new JArray(matches.Skip(offset).Take(PageSize).Select(x=>new JObject {["id"]=x.Id,["version"]=x.Version,["label"]=x.Label})),
                    ["status"]=matches.Length==0?"No matching "+category:"Found "+matches.Length+" "+category+". Search does not run or enable anything."}));
            }
            if(operation=="inspect") {
                var entry=vocabulary[category].FirstOrDefault(x=>x.Id==(string)request["capability"]&&x.Version==(int)request["version"]);
                var result=Scoped(new JObject {["operation"]=operation,["capability"]=request["capability"].DeepClone(),["version"]=request["version"].DeepClone(),
                    ["definition"]=entry!=null?entry.Definition():JValue.CreateNull(),["status"]=entry==null?"Unknown "+category+" entry or unsupported version":category=="actions"?"Action definition. Check concrete arguments before running it.":"Event definition. Inspecting does not subscribe or start a behaviour."});
                inspected=entry;Cache(result);
                return category=="facts"?ReadFact(result,entry):result;
            }
            var call=(JObject)request["call"];
            bool valid=BehaviourCatalog.TryCall((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out var error);
            bool available=false,occupied=false;string[] resources=Array.Empty<string>();
            if(valid) {
                resources=step.Resources.Distinct().ToArray();
                var runtime=editor?editor.GetComponent<RoomRules>():null;
                bool maintenance=step.Definition.Module.Domain=="workspace"&&workspace;
                var scheduler=maintenance?workspace.Runtime?.Scheduler:runtime?runtime.Scheduler:null;
                if(scheduler==null)error="Action runtime is not ready";
                else {
                    occupied=scheduler.ActionBusy(step);
                    available=maintenance?workspace.Runtime.CanRun(step,out error):runtime.CanRun(step,out error);
                    if(available&&occupied) {available=false;error=step.RequiresQuietRoom?"Stop other room actions before this room-wide action":"A running action owns a required animation channel or object";}
                    if(available&&!scheduler.HasCapacity) {available=false;error="All action slots are currently in use";}
                }
            }
            return new JObject {["operation"]="check",["call"]=call.DeepClone(),["valid"]=valid,["available"]=available,["occupied"]=occupied,
                ["resources"]=new JArray(resources),["status"]=available?"Ready now. This check does not reserve or start the action.":error??"Action unavailable"};
        }
    }
}
