// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Programs
{
    public sealed class ProgramFault : Exception {public ProgramFault(string message):base(message) {}}
    internal sealed class ProgramFunction
    {
        public string Name;public ProgramDataType Returns;
        public string[] Parameters;public Dictionary<string,ProgramDataType> Types=new();
        public Dictionary<string,ProgramValue> Initial=new();public JArray Body;
    }
    /// <summary>Immutable validated program data. No code loading, reflection or evaluation of source strings.</summary>
    public sealed class BehaviourProgram
    {
        public const int MaximumCharacters=24000,MaximumNodes=128,MaximumFunctions=16;
        public string Source {get;private set;}
        public int Version {get;private set;}
        bool structured,parallel,memory;
        internal readonly Dictionary<string,string> Remembered=new();
        internal readonly Dictionary<string,ProgramValue> InitialState=new();
        internal readonly Dictionary<string,ProgramType> CustomEvents=new();
        internal ProgramType EventType(string name) => CustomEvents.TryGetValue(name,out var type)?type:BehaviourCatalog.Event(name)!=null?ProgramType.Text:throw new ProgramFault("Unknown event");
        public string Entry {get;private set;}
        public string[] Resources => resources.ToArray();
        string[] referencedIds=System.Array.Empty<string>();
        public string[] ReferencedIds=>referencedIds.ToArray();
        readonly HashSet<string> resources=new();
        readonly Dictionary<string,ProgramFunction> functions=new();
        readonly Dictionary<string,CapabilityCall> actions=new();
        readonly HashSet<string> ids=new();
        readonly Dictionary<string,HashSet<string>> calls=new();
        sealed class TokenIdentity:IEqualityComparer<JToken> {
            public bool Equals(JToken a,JToken b)=>ReferenceEquals(a,b);
            public int GetHashCode(JToken value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        readonly Dictionary<JToken,ProgramValue> constants=new(new TokenIdentity());
        internal ProgramValue Constant(JToken expression)=>constants[expression];
        int expressions;
        public static IReadOnlyDictionary<string,ProgramDataType> Facts=>BehaviourCatalog.FactTypes;
        public static bool TryParse(string source,out BehaviourProgram program,out string error)
        {
            program=null;error=null;
            try {var value=new BehaviourProgram();value.Read(source);program=value;return true;}
            catch(Exception ex) when(ex is ProgramFault || ex is JsonException || ex is ArgumentException || ex is OverflowException) {error=ex.Message;return false;}
        }
        public int NodeCount=>ids.Count;
        public CapabilityCall[] NativeActions=>actions.Values.Select(x=>x.Copy()).ToArray();
        public RuleStep[] SimpleSteps()
        {
            var f=functions[Entry];
            if(Version!=2 || functions.Count!=1 || f.Returns!=ProgramType.Void || f.Parameters.Length!=0 || f.Initial.Count!=0 ||
                f.Body.Any(n=>(string)n["op"]!="invoke" || ((JObject)n["bindings"]).Count!=0 || n["results"]!=null))return null;
            var steps=new List<RuleStep>();foreach(var node in f.Body) {if(!Action((string)node["id"]).TryStep(out var step,out _))return null;steps.Add(step);}
            return steps.ToArray();
        }
        public string WithSimpleSteps(RuleStep[] steps)
        {
            if(SimpleSteps()==null)throw new ArgumentException("Edit this program's functions in the book");
            var root=JObject.Parse(Source);
            root["functions"][0]["body"]=ActionNodes(steps);
            // Preserve extra declarations; replace resources used by the edited actions.
            root["resources"]=new JArray(Resources.Except(SimpleSteps().SelectMany(RuleDocument.Targets)).Concat(steps.SelectMany(RuleDocument.Targets)).Distinct());
            return root.ToString(Formatting.None);
        }
        static JArray ActionNodes(RuleStep[] steps) => new(steps.Select(step=> {
            var call=LegacyCapabilityAdapters.Invocation(step);
            return new JObject { ["id"]=step.id,["op"]="invoke",["capability"]=call["id"],["version"]=call["version"],["arguments"]=call["arguments"],["bindings"]=new JObject() };
        }));
        public static string FromSteps(params RuleStep[] steps) => new JObject {
            ["version"]=2,["entry"]="main",["resources"]=new JArray(steps.SelectMany(RuleDocument.Targets).Distinct()),
            ["functions"]=new JArray(new JObject { ["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=ActionNodes(steps) })
        }.ToString(Formatting.None);
        public static string FromInvocation(JObject call) {
            var definition=BehaviourCatalog.Action((string)call["id"]);var source=new JObject {
            ["version"]=definition.Module.MinimumProgramVersion,["entry"]="main",["resources"]=new JArray(CapabilityArguments.Resources((JObject)call["arguments"],BehaviourCatalog.Action((string)call["id"]).InputSchema)),
            ["functions"]=new JArray(new JObject {["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),
                ["body"]=new JArray(new JObject {["id"]="action",["op"]="invoke",["capability"]=call["id"].DeepClone(),["version"]=call["version"].DeepClone(),["arguments"]=call["arguments"].DeepClone(),["bindings"]=new JObject()})})
            };
            if(definition.Module.MinimumProgramVersion>=3){source["state"]=new JArray();source["events"]=new JArray();}
            return source.ToString(Formatting.None);
        }
        public bool ReferencesMotion(string id)=>Source.Contains("\""+id+"\"");
        internal ProgramFunction Function(string name)=>functions[name];
        internal CapabilityCall Action(string id)=>actions[id].Copy();
        internal bool Allows(string target)=>resources.Contains(target);
        static void Need(bool condition,string message) {if(!condition)throw new ProgramFault(message);}
        internal static bool Name(string value)=>!string.IsNullOrEmpty(value)&&value.Length<=32&&value.All(c=>c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_');
        internal static ProgramType Type(string name)=>name switch {"number"=>ProgramType.Number,"boolean"=>ProgramType.Boolean,"text"=>ProgramType.Text,"void"=>ProgramType.Void,_=>throw new ProgramFault("Unknown value type")};
        static JObject Object(JToken token)=>token as JObject ?? throw new ProgramFault("Expected an object");
        static JArray Array(JToken token,int maximum)=>token is JArray array&&array.Count<=maximum ? array : throw new ProgramFault("Missing or oversized list");
        static string Text(JToken token)=>token?.Type==JTokenType.String ? (string)token : throw new ProgramFault("Expected text");
        static void Keys(JObject value,string required,string optional="")
        {
            var needed=required.Split(' ',StringSplitOptions.RemoveEmptyEntries);var allowed=needed.Concat(optional.Split(' ',StringSplitOptions.RemoveEmptyEntries)).ToHashSet();
            Need(needed.All(key=>value.ContainsKey(key))&&value.Properties().All(p=>allowed.Contains(p.Name)),"Missing or unknown program field");
        }
        void Read(string source)
        {
            Need(source!=null&&source.Length<=MaximumCharacters,"Program exceeds its size limit");
            using var reader=new JsonTextReader(new System.IO.StringReader(source)) {MaxDepth=48,DateParseHandling=DateParseHandling.None};
            var root=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            Need(!reader.Read(),"Extra data follows the program");
            var raw=root;
            root=ProgramModules.Link(raw,value=>new BehaviourProgram().ReadRoot(value));
            ReadRoot(root);
            referencedIds=raw.Descendants().OfType<JValue>().Where(x=>x.Type==JTokenType.String&&Guid.TryParseExact((string)x,"N",out _)).Select(x=>(string)x).Distinct().ToArray();
            Source=raw.ToString(Formatting.None);
        }
        void ReadRoot(JObject root)
        {
            Need((root["version"]?.Type==JTokenType.Integer||root["version"]?.Type==JTokenType.Float)&&((double)root["version"]==2||(double)root["version"]==3),"Unsupported program version");
            Version=(int)root["version"];Keys(root,Version==3?"version entry resources functions state events":"version entry resources functions","dataVersion parallelVersion memoryVersion");
            Need(!root.ContainsKey("memoryVersion")||Version==3&&(root["memoryVersion"]?.Type==JTokenType.Integer||root["memoryVersion"]?.Type==JTokenType.Float)&&(double)root["memoryVersion"]==1,"Unsupported remembered-value version");memory=root.ContainsKey("memoryVersion");
            Need(!root.ContainsKey("dataVersion")||Version==3&&(root["dataVersion"]?.Type==JTokenType.Integer||root["dataVersion"]?.Type==JTokenType.Float)&&(double)root["dataVersion"]==1,"Unsupported structured-value version");structured=root.ContainsKey("dataVersion");
            Need(!root.ContainsKey("parallelVersion")||Version==3&&(root["parallelVersion"]?.Type==JTokenType.Integer||root["parallelVersion"]?.Type==JTokenType.Float)&&(double)root["parallelVersion"]==1,"Unsupported parallel-program version");parallel=root.ContainsKey("parallelVersion");
            if(Version==3) {
                foreach(var token in Array(root["state"],16)) {var item=Object(token);Keys(item,"name initial","type memory");string name=Text(item["name"]);Need(ProgramModules.CompiledName(name)&&InitialState.TryAdd(name,Literal(item["initial"],item["type"])),"Invalid or duplicate state name");
                    if(item.ContainsKey("memory")){string id=Text(item["memory"]);Need(memory&&Name(name)&&ProgramMemoryDocument.Id(id)&&!Remembered.ContainsValue(id),"Remembered variables need memoryVersion 1 and distinct stable identities");Remembered.Add(name,id);}}
                foreach(var token in Array(root["events"],16)) {var item=Object(token);Keys(item,"name type");string name=Text(item["name"]);var type=Type(Text(item["type"]));
                    Need(System.Text.RegularExpressions.Regex.IsMatch(name,@"^user\.[a-zA-Z0-9_]{1,32}$")&&type!=ProgramType.Void&&CustomEvents.TryAdd(name,type),"Invalid or duplicate custom event");}
            }
            Entry=Text(root["entry"]);
            foreach(var item in Array(root["resources"],16)) {string id=Text(item);Need(RuleDocument.IsTarget(id)&&resources.Add(id),"Invalid or duplicate resource");}
            var definitions=Array(root["functions"],MaximumFunctions);Need(definitions.Count>0,"A program needs a function");
            foreach(var token in definitions)
            {
                var f=Object(token);Keys(f,"name returns parameters locals body");var function=new ProgramFunction {Name=Text(f["name"]),Returns=DataType(f["returns"],true),Body=Array(f["body"],MaximumNodes)};
                Need(ProgramModules.CompiledName(function.Name)&&!functions.ContainsKey(function.Name),"Invalid or duplicate function name");
                var parameters=new List<string>();
                foreach(var parameter in Array(f["parameters"],8)) {
                    var p=Object(parameter);Keys(p,"name type");string name=Text(p["name"]);var type=DataType(p["type"]);
                    Need(Name(name)&&type!=ProgramType.Void&&function.Types.TryAdd(name,type),"Invalid or duplicate parameter");parameters.Add(name);
                }
                foreach(var local in Array(f["locals"],16)) {
                    var p=Object(local);Keys(p,"name initial","type");string name=Text(p["name"]);var value=Literal(p["initial"],p["type"]);
                    Need(Name(name)&&function.Types.TryAdd(name,value.Type),"Invalid or duplicate local");function.Initial.Add(name,value);
                }
                function.Parameters=parameters.ToArray();functions.Add(function.Name,function);calls.Add(function.Name,new());
            }
            Need(functions.ContainsKey(Entry)&&functions[Entry].Parameters.Length==0,"Entry must name a function with no parameters");
            foreach(var function in functions.Values) {
                Body(function.Body,function,0);
                Need(function.Returns==ProgramType.Void||Returns(function.Body),"A value-returning function must return on every path");
            }
            var visiting=new HashSet<string>();var depths=new Dictionary<string,int>();
            int Depth(string name) {
                Need(!visiting.Contains(name),"Recursive function calls are unsupported");if(depths.TryGetValue(name,out var cached))return cached;
                visiting.Add(name);int depth=1;foreach(string callee in calls[name])depth=Math.Max(depth,1+Depth(callee));visiting.Remove(name);
                Need(depth<=8,"Function call depth exceeds its limit");depths.Add(name,depth);return depth;
            }
            foreach(string name in functions.Keys)Depth(name);
        }
        ProgramDataType DataType(JToken token,bool allowVoid=false) {
            var type=ProgramDataType.Read(token);Need(allowVoid||type!=ProgramType.Void,"A value cannot be void");
            Need(structured||type.Kind<=ProgramType.Text,"Structured values need program version 3");return type;
        }
        ProgramValue Literal(JToken value,JToken declared=null) {
            Need(declared==null||structured,"Explicit value types need dataVersion 1");var type=declared==null?null:DataType(declared);var result=ProgramValue.Literal(value,type);
            Need(structured||result.Type.Kind<=ProgramType.Text,"Structured values need program version 3");return result;
        }
        ProgramDataType Expression(JToken token,ProgramFunction function,int depth=0)
        {
            Need(depth<=8&&++expressions<=512,"Expression limit exceeded");var expression=Object(token);
            if(expression.ContainsKey("value")) {Keys(expression,"value","type");var constant=Literal(expression["value"],expression["type"]);constants.Add(expression,constant);return constant.Type;}
            if(expression.ContainsKey("var")) {Keys(expression,"var");Need(function.Types.TryGetValue(Text(expression["var"]),out var type),"Unknown variable");return type;}
            if(expression.ContainsKey("state")) {Keys(expression,"state");Need(Version==3&&InitialState.TryGetValue(Text(expression["state"]),out var state),"Unknown program state");return InitialState[Text(expression["state"])].Type;}
            if(expression.ContainsKey("fact")) {
                var fact=BehaviourCatalog.Fact(Text(expression["fact"]));Need(fact!=null,"Unknown room fact");
                if(fact.Parameterized) {
                    Need(Version==3,"Fact queries need program version 3");Keys(expression,"fact version arguments bindings");
                    Need(expression["version"]?.Type==JTokenType.Integer&&fact.ValidArguments((int)expression["version"],Object(expression["arguments"]),out _),"Invalid fact query arguments or version");
                    foreach(var binding in Object(expression["bindings"]).Properties()){var t=fact.ArgumentType(binding.Name,Object(expression["arguments"]));Need(t!=ProgramType.Void&&Expression(binding.Value,function,depth+1)==t,"Invalid fact argument binding");}
                }else Keys(expression,"fact");
                Need(structured||fact.Type.Kind<=ProgramType.Text,"Structured facts need dataVersion 1");return fact.Type;
            }
            Keys(expression,"op args");string op=Text(expression["op"]);var args=Array(expression["args"],3);Need(args.Count>0,"Invalid expression argument count");
            var types=args.Select(x=>Expression(x,function,depth+1)).ToArray();
            var dataType=ProgramDataType.Operation(op,types,args.Count>1?args[1]["value"]:null);
            if(dataType!=null){Need(structured,"Structured values need version 3 and dataVersion 1");return dataType;}
            Need(args.Count==(op=="not"?1:2),"Invalid expression argument count");
            if(op=="not"||op=="and"||op=="or") {Need(types.All(x=>x==ProgramType.Boolean),"Logic needs booleans");return ProgramType.Boolean;}
            if(op=="eq"||op=="ne") {Need(types[0]==types[1],"Comparison types differ");return ProgramType.Boolean;}
            Need(new[] {"add","sub","mul","div","mod","lt","le","gt","ge"}.Contains(op)&&types.All(x=>x==ProgramType.Number),"Unknown operation or nonnumeric argument");
            return new[] {"lt","le","gt","ge"}.Contains(op)?ProgramType.Boolean:ProgramType.Number;
        }
        void Body(JArray body,ProgramFunction function,int depth)
        {
            Need(depth<=8,"Block nesting limit exceeded");
            foreach(var token in body)
            {
                var node=Object(token);string id=Text(node["id"]),op=Text(node["op"]);Need(ProgramModules.CompiledName(id)&&ids.Add(id)&&ids.Count<=MaximumNodes,"Invalid, duplicate or excessive block identities");
                void Child(string key)=>Body(Array(node[key],MaximumNodes),function,depth+1);
                void Expr(string key,ProgramDataType type)=>Need(Expression(node[key],function)==type,"Expression type differs from its use");
                switch(op) {
                    case "checkpoint":Keys(node,"id op");Need(memory&&Remembered.Count>0,"Saving remembered values needs memoryVersion 1 and a remembered variable");break;
                    case "setState":
                        Need(Version==3,"State needs program version 3");Keys(node,"id op variable value");Need(InitialState.TryGetValue(Text(node["variable"]),out var state),"Unknown program state");Expr("value",state.Type);break;
                    case "forever":Need(Version==3,"Events need program version 3");Keys(node,"id op body");Child("body");break;
                    case "sleep":Need(Version==3,"Timers need program version 3");Keys(node,"id op seconds");Expr("seconds",ProgramType.Number);break;
                    case "awaitCondition":
                        Need(Version==3,"Conditions need program version 3");Keys(node,"id op test transition initial stableSeconds timeout received value");
                        Need(new[]{"true","false","either"}.Contains(Text(node["transition"]))&&new[]{"baseline","report"}.Contains(Text(node["initial"])),"Invalid condition transition or initial policy");
                        Need(function.Types.TryGetValue(Text(node["received"]),out var conditionReceived)&&conditionReceived==ProgramType.Boolean&&function.Types.TryGetValue(Text(node["value"]),out var conditionValue)&&conditionValue==ProgramType.Boolean&&Text(node["received"])!=Text(node["value"]),"Condition destinations need distinct boolean locals");
                        Expr("test",ProgramType.Boolean);Expr("stableSeconds",ProgramType.Number);Expr("timeout",ProgramType.Number);break;
                    case "awaitEvent":
                        Need(Version==3,"Events need program version 3");Keys(node,"id op event source timeout received value","fields version arguments bindings");
                        string eventName=Text(node["event"]);var eventType=EventType(eventName);string sourceId=Text(node["source"]);
                        var definition=BehaviourCatalog.Event(eventName);
                        Need(sourceId==""||definition?.ObjectEvent==true&&RuleDocument.IsTarget(sourceId),"Only object events accept a source");
                        if(definition?.HasSubscription==true) {
                            Need(node["version"]?.Type==JTokenType.Integer&&(double)node["version"]==definition.Version,"Unsupported event subscription version");
                            Need(definition.ValidArguments((int)node["version"],Object(node["arguments"]),out var eventError),eventError??"Invalid event arguments");
                            foreach(var binding in Object(node["bindings"]).Properties()) {
                                var argumentType=definition.ArgumentType(binding.Name,Object(node["arguments"]));Need(argumentType!=ProgramType.Void,"Unsupported event argument binding");
                                Need(Expression(binding.Value,function)==argumentType,"Event argument type differs");
                            }
                        } else {
                            Need(!node.ContainsKey("arguments")&&!node.ContainsKey("bindings"),"This event has no subscription arguments");
                            Need(definition==null?!node.ContainsKey("version"):
                                node.ContainsKey("version")?node["version"]?.Type==JTokenType.Integer&&(double)node["version"]==definition.Version:definition.Version==1,
                                "Unsupported native event version; inspect its current definition");
                        }
                        Need(function.Types.TryGetValue(Text(node["received"]),out var received)&&received==ProgramType.Boolean,"Event received needs a boolean local");
                        Need(function.Types.TryGetValue(Text(node["value"]),out var payload)&&payload==eventType,"Event value needs a matching local");
                        Need(Text(node["received"])!=Text(node["value"]),"Event destinations must differ");
                        if(node.ContainsKey("fields")) {
                            var assigned=new HashSet<string> {Text(node["received"]),Text(node["value"])};
                            foreach(var field in Object(node["fields"]).Properties()) {
                                var fieldType=definition?.FieldType(field.Name)??ProgramType.Void;
                                Need(fieldType!=ProgramType.Void&&function.Types.TryGetValue(Text(field.Value),out var localType)&&localType==fieldType&&assigned.Add(Text(field.Value)),"Invalid or duplicate event field destination");
                            }
                        }
                        Expr("timeout",ProgramType.Number);break;
                    case "emitEvent":
                        Need(Version==3,"Events need program version 3");Keys(node,"id op event value");Need(CustomEvents.TryGetValue(Text(node["event"]),out var customType),"Only declared custom events may be emitted");Expr("value",customType);break;
                    case "set": Keys(node,"id op variable value");Need(function.Types.TryGetValue(Text(node["variable"]),out var type),"Unknown assigned variable");Expr("value",type);break;
                    case "if": Keys(node,"id op test then else");Expr("test",ProgramType.Boolean);Child("then");Child("else");break;
                    case "repeat": Keys(node,"id op count body");Expr("count",ProgramType.Number);Child("body");break;
                    case "switch":
                        Keys(node,"id op value cases default");var choice=Expression(node["value"],function);Need(choice.Kind<=ProgramType.Text,"Cases require a scalar value");var values=new List<ProgramValue>();
                        foreach(var item in Array(node["cases"],16)) {var arm=Object(item);Keys(arm,"value body");var value=Literal(arm["value"]);Need(value.Type==choice&&!values.Any(x=>x.Same(value)),"Duplicate or differently typed case");values.Add(value);Body(Array(arm["body"],MaximumNodes),function,depth+1);}Child("default");break;
                    case "parallel":
                        Need(parallel,"Parallel calls need parallelVersion 1");Keys(node,"id op branches");
                        var branches=Array(node["branches"],4);Need(branches.Count>=2,"Parallel needs two to four branches");var destinations=new HashSet<string>();
                        foreach(var tokenBranch in branches) {
                            var branch=Object(tokenBranch);Keys(branch,"function args","result");string branchName=Text(branch["function"]);
                            Need(functions.TryGetValue(branchName,out var branchFunction),"Unknown parallel function");calls[function.Name].Add(branchName);
                            var branchArgs=Array(branch["args"],8);Need(branchArgs.Count==branchFunction.Parameters.Length,"Wrong parallel argument count");
                            for(int i=0;i<branchArgs.Count;i++)Need(Expression(branchArgs[i],function)==branchFunction.Types[branchFunction.Parameters[i]],"Parallel argument type differs");
                            if(branch.ContainsKey("result")) {string destination=Text(branch["result"]);Need(destinations.Add(destination)&&function.Types.TryGetValue(destination,out var resultType)&&resultType==branchFunction.Returns&&resultType!=ProgramType.Void,"Invalid or duplicate parallel result destination");}
                        }
                        break;
                    case "call":
                        Keys(node,"id op function args","result");string name=Text(node["function"]);Need(functions.TryGetValue(name,out var callee),"Unknown function");calls[function.Name].Add(name);
                        var args=Array(node["args"],8);Need(args.Count==callee.Parameters.Length,"Wrong function argument count");
                        for(int i=0;i<args.Count;i++)Need(Expression(args[i],function)==callee.Types[callee.Parameters[i]],"Function argument type differs");
                        if(node.ContainsKey("result"))Need(function.Types.TryGetValue(Text(node["result"]),out var result)&&result==callee.Returns&&result!=ProgramType.Void,"Invalid return destination");break;
                    case "return":
                        Keys(node,function.Returns==ProgramType.Void?"id op":"id op value");if(function.Returns!=ProgramType.Void)Expr("value",function.Returns);break;
                    case "invoke":
                        Keys(node,"id op capability version arguments bindings","results waitForChannels");
                        if(node.ContainsKey("waitForChannels")){Need(Version==3,"Channel waiting needs program version 3");Expr("waitForChannels",ProgramType.Number);}
                        string capability=Text(node["capability"]);Need((node["version"]?.Type==JTokenType.Integer||node["version"]?.Type==JTokenType.Float)&&(double)node["version"]==Math.Truncate((double)node["version"]),"Capability version must be an integer");
                        Need(BehaviourCatalog.TryCall(capability,(int)node["version"],Object(node["arguments"]),out var step,out var invocationError),invocationError??"Invalid capability arguments");
                        var contract=BehaviourCatalog.Action(capability);
                        Need(Version>=contract.Module.MinimumProgramVersion,"This capability needs program version "+contract.Module.MinimumProgramVersion);
                        Need(CapabilityArguments.LiteralResources(Object(node["arguments"]),contract.InputSchema,Object(node["bindings"]),Version).All(resources.Contains),"Declare every action resource");step.NodeId=id;actions.Add(id,step);
                        if(node.ContainsKey("results")) {
                            Need(Version==3,"Action results need program version 3");
                            var outputs=(JObject)contract.OutputSchema["properties"];var assigned=new HashSet<string>();
                            foreach(var output in Object(node["results"]).Properties()) {
                                Need(outputs[output.Name] is JObject,"Unknown action result");
                                string destination=Text(output.Value);
                                var outputType=CapabilitySchema.OutputType(outputs[output.Name] as JObject);
                                Need(outputType!=null&&function.Types.TryGetValue(destination,out var localType)&&localType==outputType&&assigned.Add(destination),"Invalid or duplicate action result destination");
                            }
                        }
                        Need(CapabilitySchema.SeparateBindings(Object(node["bindings"])),"A value and its child fields cannot both be bound");
                        foreach(var binding in Object(node["bindings"]).Properties()) {
                            Need(CapabilitySchema.Value(node["arguments"],binding.Name)!=null,"A bound argument needs a literal placeholder");
                            var expected=BindingType(capability,binding.Name,Object(node["arguments"]));Need(Expression(binding.Value,function)==expected,"Native argument type differs");
                        }break;
                    default:throw new ProgramFault("Unknown program block");
                }
            }
        }
        static bool Returns(JArray body)
        {
            foreach(var item in body) {
                string op=(string)item["op"];if(op=="return")return true;
                if(op=="if"&&Returns((JArray)item["then"])&&Returns((JArray)item["else"]))return true;
                if(op=="switch"&&Returns((JArray)item["default"])&&((JArray)item["cases"]).All(x=>Returns((JArray)x["body"])))return true;
            }return false;
        }
        internal static ProgramDataType BindingType(string capability,string name,JObject arguments)
        {
            var type=CapabilitySchema.BindingType(BehaviourCatalog.Action(capability)?.InputSchema,name,arguments);
            Need(type!=null,"Unsupported capability argument binding; selectors must stay literal");return type;
        }
    }
}
