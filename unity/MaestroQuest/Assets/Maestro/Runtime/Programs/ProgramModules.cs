// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
 /// <summary>Content-pinned, self-contained composition into the existing program machine. No code loading.</summary>
 public static class ProgramModules
 {
  static void Need(bool ok,string message){if(!ok)throw new ProgramFault(message);}
  static JObject Obj(JToken v)=>v as JObject??throw new ProgramFault("Expected a module object");
  static JArray List(JToken v,int max)=>v is JArray a&&a.Count<=max?a:throw new ProgramFault("Missing or oversized module list");
  static string Text(JToken v)=>v?.Type==JTokenType.String?(string)v:throw new ProgramFault("Expected module text");
  static bool NumberIs(JToken v,double expected)=>(v?.Type==JTokenType.Integer||v?.Type==JTokenType.Float)&&(double)v==expected;
  static bool Plain(JToken v)=>v?.Type==JTokenType.String&&BehaviourProgram.Name((string)v);
  static void Fields(JObject v,params string[] keys)=>Need(v.Count==keys.Length&&keys.All(v.ContainsKey),"Missing or unknown module field");
  internal static bool CompiledName(string v)=>v!=null&&v.Split('.').Length<=4&&v.Split('.').All(BehaviourProgram.Name);
  /// <summary>SHA256 over versioned canonical ASCII. Strings are UTF16 code units; numbers are binary64. Not authentication.</summary>
  public static string Hash(JToken module){
   var b=new StringBuilder("Maestro.Module.v1\n");int nodes=0;
   void Encode(JToken v,int depth){
    Need(depth<=48&&++nodes<=32768,"Module hash input limit exceeded");
    switch(v.Type){
     case JTokenType.Null:b.Append('N');return;
     case JTokenType.Boolean:b.Append((bool)v?'T':'F');return;
     case JTokenType.Integer:case JTokenType.Float:
      double number=(double)v;Need(!double.IsNaN(number)&&!double.IsInfinity(number),"Module numbers must be finite");b.Append('D').Append(BitConverter.DoubleToInt64Bits(number==0?0:number).ToString("x16",CultureInfo.InvariantCulture));return;
     case JTokenType.String:
      string text=(string)v;b.Append('S').Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':');foreach(char c in text)b.Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));return;
     case JTokenType.Array:
      var array=(JArray)v;b.Append('A').Append(array.Count.ToString(CultureInfo.InvariantCulture)).Append('[');foreach(var item in array)Encode(item,depth+1);b.Append(']');return;
     case JTokenType.Object:
      var obj=(JObject)v;b.Append('O').Append(obj.Count.ToString(CultureInfo.InvariantCulture)).Append('{');foreach(var p in obj.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal)){Encode(new JValue(p.Name),depth+1);Encode(p.Value,depth+1);}b.Append('}');return;
     default:throw new ProgramFault("Unsupported module value");
    }
   }
   Encode(module,0);using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.ASCII.GetBytes(b.ToString())).Select(v=>v.ToString("x2",CultureInfo.InvariantCulture)));
  }
  static void Expression(JToken token,Action<JObject> visit){var e=Obj(token);visit(e);if(e.ContainsKey("op"))foreach(var arg in List(e["args"],3))Expression(arg,visit);if(e.ContainsKey("fact")&&e["bindings"] is JObject bindings)foreach(var binding in bindings.Properties())Expression(binding.Value,visit);}
  static void Expressions(JObject n,Action<JObject> visit){
   switch((string)n["op"]){
    case "set":case "setState":case "emitEvent":case "switch":Expression(n["value"],visit);break;
    case "if":Expression(n["test"],visit);break;case "repeat":Expression(n["count"],visit);break;case "sleep":Expression(n["seconds"],visit);break;
    case "return":if(n.ContainsKey("value"))Expression(n["value"],visit);break;
    case "call":foreach(var arg in List(n["args"],8))Expression(arg,visit);break;
    case "invoke":foreach(var p in Obj(n["bindings"]).Properties())Expression(p.Value,visit);break;
    case "awaitCondition":Expression(n["test"],visit);Expression(n["stableSeconds"],visit);Expression(n["timeout"],visit);break;
    case "awaitEvent":Expression(n["timeout"],visit);if(n.ContainsKey("bindings"))foreach(var p in Obj(n["bindings"]).Properties())Expression(p.Value,visit);break;
   }
  }
  internal static void Nodes(JArray body,Action<JObject> visit){foreach(var token in body){var n=Obj(token);visit(n);switch((string)n["op"]){
   case "if":Nodes(List(n["then"],128),visit);Nodes(List(n["else"],128),visit);break;
   case "repeat":case "forever":Nodes(List(n["body"],128),visit);break;
   case "switch":foreach(var arm in List(n["cases"],16))Nodes(List(Obj(arm)["body"],128),visit);Nodes(List(n["default"],128),visit);break;
  }}}
  internal static JObject Link(JObject source,Action<JObject> validate){
   int instances=0;
   JObject Scope(JObject raw,int depth){
    Need(depth<=3,"Module nesting exceeds three levels");var p=(JObject)raw.DeepClone();var imports=new Dictionary<string,(JObject item,JObject linked)>();
    if(raw.ContainsKey("moduleVersion")||raw.ContainsKey("imports")){
     Need(NumberIs(raw["version"],3)&&NumberIs(raw["moduleVersion"],1)&&raw.ContainsKey("imports"),"Modules need version 3 and moduleVersion 1");
     List(p["state"],16);
     foreach(var token in List(raw["imports"],4)){
      Need(++instances<=4,"A program may include at most four module instances");var imp=Obj(token);Fields(imp,"alias","hash","module","signals");Need(Plain(imp["alias"])&&!imports.ContainsKey((string)imp["alias"]),"Invalid or duplicate module alias");
      var m=Obj(imp["module"]);Fields(m,"version","name","exports","program");string title=Text(m["name"]);Need(NumberIs(m["version"],1)&&!string.IsNullOrWhiteSpace(title.Replace("\uFEFF",""))&&title.Length<=64&&!title.Any(char.IsControl),"Invalid module definition");
      string hash=Text(imp["hash"]);Need(hash.Length==64&&hash.All(c=>c>='a'&&c<='f'||c>='0'&&c<='9')&&Hash(m)==hash,"Module content does not match its pinned hash");
      var child=Obj(m["program"]);Need(NumberIs(child["version"],3),"A module needs program version 3");
      var exports=List(m["exports"],16);Need(exports.Count>0&&exports.All(Plain)&&exports.Select(Text).Distinct().Count()==exports.Count,"Invalid module exports");
      Need(exports.All(n=>List(child["functions"],16).Any(f=>Text(Obj(f)["name"])==Text(n))),"Export must name a local function");
      Need(List(child["resources"],16).All(r=>List(raw["resources"],16).Any(v=>JToken.DeepEquals(r,v))),"Declare every imported module resource in its caller");
      Need(!child.ContainsKey("dataVersion")||NumberIs(raw["dataVersion"],1),"Caller must enable imported structured values");
      var linked=Scope(child,depth+1);var signals=Obj(imp["signals"]);var events=List(raw["events"],16).Select(Obj).ToArray();var declared=List(linked["events"],16);
      Need(signals.Count==declared.Count,"Connect every module signal explicitly");foreach(var e in declared){var ev=Obj(e);string name=Text(ev["name"]);Need(signals.ContainsKey(name)&&signals[name].Type==JTokenType.String&&events.Any(c=>JToken.DeepEquals(c["name"],signals[name])&&JToken.DeepEquals(c["type"],ev["type"])),"Module signal needs a matching caller declaration");}
      imports.Add((string)imp["alias"],(imp,linked));
     }
    }
    Need(Plain(p["entry"]),"Entry must use a local function name");foreach(var state in List(p["state"]??new JArray(),16))Need(Plain(Obj(state)["name"]),"State names must be local");
    foreach(var token in List(p["functions"],16)){var f=Obj(token);Need(Plain(f["name"]),"Function names must be local");Nodes(List(f["body"],128),n=>{
     Need(Plain(n["id"]),"Block identities must be local");Expressions(n,e=>{if(e.ContainsKey("state"))Need(Plain(e["state"]),"State references must be local");});
     if((string)n["op"]=="setState")Need(Plain(n["variable"]),"State destinations must be local");
     if((string)n["op"]=="call"){
      Need(Plain(n["function"]),"Call functions must be local names");if(n.ContainsKey("module")){
       Need(Plain(n["module"]),"Invalid module alias");string alias=(string)n["module"];Need(imports.TryGetValue(alias,out var imported)&&List(imported.item["module"]["exports"],16).Any(x=>Text(x)==Text(n["function"])),"Unknown module or unexported function");n["function"]=alias+"."+Text(n["function"]);n.Remove("module");
      }
     }
    });}
    foreach(var pair in imports){string prefix=pair.Key+".";var linked=pair.Value.linked;var signals=(JObject)pair.Value.item["signals"];
     foreach(var state in (JArray)linked["state"])state["name"]=prefix+Text(state["name"]);
     foreach(var f in (JArray)linked["functions"]){f["name"]=prefix+Text(f["name"]);Nodes((JArray)f["body"],n=>{
      n["id"]=prefix+Text(n["id"]);Expressions(n,e=>{if(e.ContainsKey("state"))e["state"]=prefix+Text(e["state"]);});
      if((string)n["op"]=="setState")n["variable"]=prefix+Text(n["variable"]);if((string)n["op"]=="call")n["function"]=prefix+Text(n["function"]);
      if(((string)n["op"]=="awaitEvent"||(string)n["op"]=="emitEvent")&&signals.TryGetValue(Text(n["event"]),out var target))n["event"]=target.DeepClone();
     });}
     foreach(var f in (JArray)linked["functions"])((JArray)p["functions"]).Add(f.DeepClone());foreach(var state in (JArray)linked["state"])((JArray)p["state"]).Add(state.DeepClone());
    }
    p.Remove("moduleVersion");p.Remove("imports");if(depth>0)validate(p);return p;
   }
   return Scope(source,0);
  }
 }
}
