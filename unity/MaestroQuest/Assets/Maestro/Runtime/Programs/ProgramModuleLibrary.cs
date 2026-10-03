// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
 /// <summary>Immutable content-addressed definitions. File IO/validation run off-thread; Poll commits observations on the owner thread.</summary>
 public sealed class ProgramModuleLibrary
 {
  public const int MaximumEntries=256,MaximumIncluded=16,MaximumBytes=96000;
  public sealed class Entry {
   public string Hash {get;internal set;} public string Name {get;internal set;} public string Error {get;internal set;} public bool Included {get;internal set;} internal JObject Definition;internal HashSet<string> References=new();
   public JObject ReadDefinition()=>Definition==null?null:(JObject)Definition.DeepClone();
  }
  public sealed class Write {
   internal Task<Result> Task;internal IDisposable Lease;public string Hash,Error;public bool Pending=true,Changed;public int Revision;
  }
  internal sealed class Result {public Entry Entry;public bool Removed,Changed;public string Error;}
  sealed class Loaded {public Dictionary<string,Entry> Entries=new(),Included=new();public string Error;public bool Overflow;}
  readonly object gate=new();
  readonly string directory;readonly string[] includedSources;readonly Task<Loaded> loading;
  Dictionary<string,Entry> entries=new(),included=new();Write write;bool overflow;
  public bool Ready {get;private set;}
  public string Error {get;private set;}
  public int Revision {get;private set;}=1;
  public bool Pending=>write?.Pending==true;
  public int Count=>entries.Keys.Union(included.Keys).Count();
  IEnumerable<Entry> VisibleEntries()=>entries.Values.Concat(included.Values.Where(e=>!entries.ContainsKey(e.Hash)));
  public static string[] IncludedFromApplication()=>UnityEngine.Resources.LoadAll<UnityEngine.TextAsset>("Programs/Modules").OrderBy(a=>a.name,StringComparer.Ordinal).Select(a=>a.text).ToArray();
  public static bool ValidHash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='a'&&c<='f'||c>='0'&&c<='9');
  readonly Maestro.Quest.Persistence.WorkspaceWriteGate workspaceWrites;
  public ProgramModuleLibrary(string parent,Maestro.Quest.Persistence.WorkspaceWriteGate writeGate=null,IEnumerable<string> includedDefinitions=null){
   workspaceWrites=writeGate??new();includedSources=(includedDefinitions??Array.Empty<string>()).Take(MaximumIncluded+1).ToArray();
   directory=Path.Combine(Path.GetFullPath(parent),"program-modules.v1");
   // Build the static vocabulary on the Unity owner thread before pure validation on the worker.
   _=BehaviourCatalog.Actions.Count;loading=Task.Run(Load);
  }
  static string Compact(JToken value)=>value.ToString(Formatting.None);
  static JObject Wrapper(JObject module){
   var program=(JObject)module["program"];var result=new JObject {["version"]=3,["moduleVersion"]=1,["entry"]="main",["resources"]=program["resources"]?.DeepClone(),["state"]=new JArray(),["events"]=program["events"]?.DeepClone(),
    ["functions"]=new JArray(new JObject {["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=new JArray()}),
    ["imports"]=new JArray(new JObject {["alias"]="module",["hash"]=ProgramModules.Hash(module),["module"]=module.DeepClone(),["signals"]=new JObject(((JArray)program["events"]).Select(e=>new JProperty((string)e["name"],(string)e["name"])))})};
   if(program.ContainsKey("parallelVersion"))result["parallelVersion"]=program["parallelVersion"].DeepClone();
   if(program.ContainsKey("dataVersion"))result["dataVersion"]=program["dataVersion"].DeepClone();return result;
  }
  public static JObject Definition(string source,string name,string[] exports){
   if(!BehaviourProgram.TryParse(source,out var compiled,out var error))throw new ProgramFault(error);
   var program=ReadObject(compiled.Source);if((int)program["version"]==2){program["version"]=3;program["state"]=new JArray();program["events"]=new JArray();}
   var module=new JObject {["version"]=1,["name"]=name,["exports"]=new JArray(exports??Array.Empty<string>()),["program"]=program};Validate(module);return module;
  }
  public static void Validate(JObject module){
   if(module==null||Compact(module).Length>BehaviourProgram.MaximumCharacters||module["program"] is not JObject p||p["events"] is not JArray)throw new ProgramFault("Invalid or oversized module definition");
   if(!BehaviourProgram.TryParse(Compact(Wrapper(module)),out _,out var error))throw new ProgramFault("Module cannot be imported: "+error);
  }
  // Bounded wire shape only, matching shared/programModuleIdentity.ts. The
  // import capability additionally compiles the complete definition before IO.
  public static bool ValidRecord(JObject m){
   try {
    bool Version(JToken value,int version)=>(value?.Type==JTokenType.Integer||value?.Type==JTokenType.Float)&&(double)value==version;
    if(m==null||m.Count!=4||!new[]{"version","name","exports","program"}.All(m.ContainsKey)||!Version(m["version"],1)||m["name"]?.Type!=JTokenType.String)return false;
    string name=(string)m["name"];if(string.IsNullOrWhiteSpace(name.Replace("\uFEFF",""))||name.Length>64||name.Any(char.IsControl)||m["exports"] is not JArray exports||exports.Count<1||exports.Count>16)return false;
    bool Plain(JToken t)=>t?.Type==JTokenType.String&&System.Text.RegularExpressions.Regex.IsMatch((string)t,"^[a-zA-Z0-9_]{1,32}$");
    if(!exports.All(Plain)||exports.Values<string>().Distinct().Count()!=exports.Count||m["program"] is not JObject p||!Version(p["version"],3))return false;
    if(p["resources"] is not JArray resources||resources.Count>16||resources.Any(r=>r.Type!=JTokenType.String||!System.Text.RegularExpressions.Regex.IsMatch((string)r,"^(maestro|book|[a-fA-F0-9]{32})$")))return false;
    if(p["events"] is not JArray events||events.Count>16||events.Any(e=>e is not JObject o||o.Count!=2||o["name"]?.Type!=JTokenType.String||!System.Text.RegularExpressions.Regex.IsMatch((string)o["name"],"^user\\.[a-zA-Z0-9_]{1,32}$")||o["type"]?.Type!=JTokenType.String||!new[]{"number","text","boolean"}.Contains((string)o["type"])))return false;
    if(Compact(m).Length>BehaviourProgram.MaximumCharacters)return false;_=ProgramModules.Hash(m);return true;
   }catch{return false;}
  }
  public static JObject ImportDefinition(string hash,JObject definition){
   if(!ValidHash(hash)||!ValidRecord(definition)||ProgramModules.Hash(definition)!=hash)throw new ProgramFault("Module content does not match its file identity");
   Validate(definition);return (JObject)definition.DeepClone();
  }
  static JObject ReadObject(string source){
   using var reader=new JsonTextReader(new StringReader(source)) {MaxDepth=48,DateParseHandling=DateParseHandling.None};
   var module=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())throw new ProgramFault("Extra module data");return module;
  }
  static Entry Decode(string hash,string source){
   var module=ReadObject(source);Validate(module);if(ProgramModules.Hash(module)!=hash)throw new ProgramFault("Module content does not match its file identity");
   return new Entry {Hash=hash,Name=(string)module["name"],Definition=module,References=module.Descendants().OfType<JValue>().Where(x=>x.Type==JTokenType.String).Select(x=>(string)x).Where(x=>x.Length==32||x.Length==64).ToHashSet(StringComparer.Ordinal)};
  }
  static string Read(string path){var file=new FileInfo(path);if(!file.Exists||file.Length>MaximumBytes)throw new IOException("Missing or oversized module file");return File.ReadAllText(path,new UTF8Encoding(false,true));}
  Loaded Load(){
   var result=new Loaded();
   if(includedSources.Length>MaximumIncluded)result.Error="Included module limit exceeded.";
   foreach(string source in includedSources.Take(MaximumIncluded))try {
    if(source==null||Encoding.UTF8.GetByteCount(source)>MaximumBytes)throw new InvalidDataException("Included module exceeds its byte limit");
    var definition=ReadObject(source);string hash=ProgramModules.Hash(definition);var entry=Decode(hash,source);entry.Included=true;result.Included[hash]=entry;
   }catch(Exception){result.Error="An included module is unavailable; existing saved imports remain unchanged.";}
   try {
    if(!Directory.Exists(directory))return result;
    var paths=Directory.EnumerateFiles(directory,"*.json").Take(MaximumEntries+1).OrderBy(x=>x,StringComparer.Ordinal).ToArray();result.Overflow=paths.Length>MaximumEntries;
    foreach(string path in paths.Take(MaximumEntries)){
     string hash=Path.GetFileNameWithoutExtension(path);if(!ValidHash(hash)){result.Error="Unrecognized library files are preserved.";continue;}
     try {result.Entries.Add(hash,Decode(hash,Read(path)));}
     catch(Exception ex){result.Entries.Add(hash,new Entry {Hash=hash,Name="Unavailable "+hash.Substring(0,8),Error=ex.Message});}
    }
    if(result.Overflow)result.Error="Library file limit exceeded. Existing files are preserved; remove entries and reopen the room before publishing.";
   }catch(Exception ex){result.Error="Cannot read module library: "+ex.Message;}
   return result;
  }
  public void Poll(){lock(gate){
   if(!Ready&&loading.IsCompleted){var loaded=loading.GetAwaiter().GetResult();entries=loaded.Entries;included=loaded.Included;Error=loaded.Error;overflow=loaded.Overflow;Ready=true;}
   if(write?.Pending!=true||!write.Task.IsCompleted)return;
   var result=write.Task.GetAwaiter().GetResult();write.Error=result.Error;write.Changed=result.Changed;
   if(result.Error==null){if(result.Removed)entries.Remove(write.Hash);else entries[write.Hash]=result.Entry;if(result.Changed)Revision++;}
   write.Revision=Revision;write.Pending=false;write.Lease?.Dispose();write.Lease=null;
  }}
  public void Flush(){loading.GetAwaiter().GetResult();if(write?.Pending==true)write.Task.GetAwaiter().GetResult();Poll();}
  public Entry[] Search(string query){Poll();lock(gate){var terms=query.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries);return VisibleEntries().Where(e=>terms.All(t=>(e.Name+" "+e.Hash+" "+string.Join(" ",e.Definition?["exports"]?.Values<string>()??Array.Empty<string>())).IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(e=>e.Name,StringComparer.Ordinal).ThenBy(e=>e.Hash,StringComparer.Ordinal).ToArray();}}
  public Entry Inspect(string hash){Poll();lock(gate)return entries.TryGetValue(hash,out var entry)?entry:included.TryGetValue(hash,out entry)?entry:null;}
  // Called by the existing off-thread retained-save audit. It never commits owner-thread state.
  public bool Retains(string id,out bool uncertain){var loaded=loading.GetAwaiter().GetResult();lock(gate){var current=(Ready?entries:loaded.Entries).Values.Concat((Ready?included:loaded.Included).Values);uncertain=Pending||(Ready?Error:loaded.Error)!=null||current.Any(e=>e.Error!=null);return current.Any(e=>e.References.Contains(id));}}
  public bool CanWrite(out string error){Poll();error=workspaceWrites.Frozen?Maestro.Quest.Persistence.WorkspaceWriteGate.FrozenReason:!Ready?"Module library is loading":Pending?"Wait for the dispatched library write":Revision>=1000000?"Reopen the room before changing the library":null;return error==null;}
  public bool CanPublish(JObject module,out string error){lock(gate){
   if(!CanWrite(out error))return false;string hash=ProgramModules.Hash(module);
   if(entries.TryGetValue(hash,out var entry)&&entry.Error!=null){error="The existing library copy is damaged; remove it explicitly before publishing again";return false;}
   if(!entries.ContainsKey(hash)&&(overflow||entries.Count>=MaximumEntries)){error="Module library is full; remove an unused library entry first";return false;}
   return true;
  }}
  public Write Publish(JObject module){lock(gate){
   Validate(module);if(!CanPublish(module,out var error))throw new ProgramFault(error);string hash=ProgramModules.Hash(module),source=Compact(module);
   write=new Write {Hash=hash,Lease=workspaceWrites.Write()};write.Task=Task.Run(()=>{
    string temporary=null;try {
     Directory.CreateDirectory(directory);string path=Path.Combine(directory,hash+".json");
     if(File.Exists(path))return new Result {Entry=Decode(hash,Read(path)),Changed=false};
     // A stage must not be longer than its 64-character content-addressed destination.
     // Unity Windows file APIs can reject an otherwise usable workspace at MAX_PATH.
     temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
     using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=new UTF8Encoding(false,true).GetBytes(source);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
     File.Move(temporary,path);temporary=null;return new Result {Entry=Decode(hash,source),Changed=true};
    }catch(Exception ex){return new Result {Error=ex.Message};}finally{if(temporary!=null)try{File.Delete(temporary);}catch(Exception){}}
   });return write;
  }}
  public bool CanRemove(string hash,out string error){lock(gate){
   if(!CanWrite(out error))return false;
   if(Inspect(hash)?.Included==true){error="Included modules cannot be removed. Copy and edit their source to make your own version.";return false;}return true;
  }}
  public Write Remove(string hash){lock(gate){
   if(!ValidHash(hash))throw new ProgramFault("Invalid module identity");if(!CanRemove(hash,out var error))throw new ProgramFault(error);
   write=new Write {Hash=hash,Lease=workspaceWrites.Write()};write.Task=Task.Run(()=>{try{string path=Path.Combine(directory,hash+".json");bool present=File.Exists(path);File.Delete(path);return new Result {Removed=true,Changed=present};}catch(Exception ex){return new Result {Error=ex.Message};}});return write;
  }}
 }
}
