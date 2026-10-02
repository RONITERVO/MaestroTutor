// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Programs
{
    /// <summary>Passive typed memory, never a saved interpreter or permission to run.
    /// Declaration IDs survive display-name edits; program IDs isolate copied behaviours.</summary>
    internal sealed class ProgramMemoryDocument
    {
        internal const int MaximumBytes=1024*1024,MaximumPrograms=64,MaximumCells=512,MaximumCellsPerProgram=32,MaximumWriteCells=16;
        internal const string InitialRevision="initial";
        static readonly UTF8Encoding Utf8=new(false,true);
        internal sealed class Cell
        {
            internal string Name {get;}
            internal ProgramValue Value {get;}
            internal Cell(string name,ProgramValue value)
            {
                Need(BehaviourProgram.Name(name),"Invalid memory variable name.");
                // Values constructed directly by native code still need the literal bounds.
                var checkedValue=ProgramValue.Literal(JToken.FromObject(value.Value??throw Invalid("Memory cannot contain void.")),value.Type);
                var shape=TypeJson(checkedValue.Type);
                Need(shape.ToString(Formatting.None).Length<=2048&&Tokens(shape).Count()<=256,"Memory type exceeds its limit.");
                Name=name;Value=checkedValue;
            }
            internal bool Same(Cell other)=>other!=null&&Name==other.Name&&Value.Same(other.Value);
        }
        readonly IReadOnlyDictionary<string,IReadOnlyDictionary<string,Cell>> programs;
        readonly byte[] bytes;
        internal string Revision {get;}
        internal string Identity {get;}
        internal IReadOnlyDictionary<string,IReadOnlyDictionary<string,Cell>> Programs=>programs;
        internal static bool Id(string value)=>value!=null&&value.Length==32&&value.All(c=>c>='a'&&c<='f'||c>='0'&&c<='9');
        internal static bool ProgramId(string value)=>value!=null&&value.Length==32&&value.All(c=>c>='a'&&c<='f'||c>='A'&&c<='F'||c>='0'&&c<='9');
        static InvalidDataException Invalid(string message)=>new(message);
        static void Need(bool condition,string message){if(!condition)throw Invalid(message);}
        static bool Exact(JObject value,params string[] keys)=>value!=null&&value.Count==keys.Length&&keys.All(value.ContainsKey);
        static string Text(JToken value)=>value?.Type==JTokenType.String?(string)value:throw Invalid("Expected memory text.");
        static string Identifier(JToken value,bool program=false){string result=Text(value);Need(program?ProgramId(result):Id(result),"Invalid memory identity.");return result;}
        static IEnumerable<JToken> Tokens(JToken value)=>value is JContainer container?container.DescendantsAndSelf():new[]{value};
        static JToken TypeJson(ProgramDataType type)=>type.Kind switch {
            ProgramType.List=>new JObject {["list"]=TypeJson(type.Item)},
            ProgramType.Record=>new JObject {["record"]=new JObject(type.Fields.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new JProperty(x.Key,TypeJson(x.Value))))},
            _=>new JValue(type.Kind.ToString().ToLowerInvariant())
        };
        ProgramMemoryDocument(string revision,IDictionary<string,IReadOnlyDictionary<string,Cell>> source)
        {
            Need(Id(revision)||revision==InitialRevision&&source.Count==0,"Invalid memory revision.");
            Need(source.Count<=MaximumPrograms&&source.Values.Sum(x=>x.Count)<=MaximumCells,"Memory collection is full.");
            foreach(var group in source){Need(ProgramId(group.Key)&&group.Value.Count>0&&group.Value.Count<=MaximumCellsPerProgram,"Invalid memory program.");foreach(var cell in group.Value)Need(Id(cell.Key)&&cell.Value!=null,"Invalid memory variable.");}
            Revision=revision;
            programs=new ReadOnlyDictionary<string,IReadOnlyDictionary<string,Cell>>(source.ToDictionary(x=>x.Key,x=>(IReadOnlyDictionary<string,Cell>)new ReadOnlyDictionary<string,Cell>(new Dictionary<string,Cell>(x.Value)),StringComparer.Ordinal));
            var json=new JObject {["version"]=1,["revision"]=revision,["programs"]=new JArray(programs.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(p=>new JObject {
                ["id"]=p.Key,["cells"]=new JArray(p.Value.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(c=>new JObject {["id"]=c.Key,["name"]=c.Value.Name,["type"]=TypeJson(c.Value.Value.Type),["value"]=JToken.FromObject(c.Value.Value.Value)}))
            }))};
            bytes=Utf8.GetBytes(json.ToString(Formatting.None));Need(bytes.Length<=MaximumBytes,"Memory document exceeds its byte limit.");
            using var sha=SHA256.Create();Identity=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        }
        internal static ProgramMemoryDocument Empty()=>new(InitialRevision,new Dictionary<string,IReadOnlyDictionary<string,Cell>>());
        internal byte[] Encode()=>(byte[])bytes.Clone();
        internal static ProgramMemoryDocument Decode(byte[] bytes)
        {
            Need(bytes!=null&&bytes.Length>0&&bytes.Length<=MaximumBytes,"Invalid memory document size.");
            using var reader=new JsonTextReader(new StringReader(Utf8.GetString(bytes))) {MaxDepth=16,DateParseHandling=DateParseHandling.None};
            var root=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            Need(!reader.Read()&&Exact(root,"version","revision","programs")&&root["version"]?.Type==JTokenType.Integer&&(int)root["version"]==1,"Unsupported memory document.");
            Need(root["programs"] is JArray a&&a.Count<=MaximumPrograms,"Invalid memory program list.");
            var result=new Dictionary<string,IReadOnlyDictionary<string,Cell>>();int count=0;
            foreach(var token in (JArray)root["programs"]){
                Need(token is JObject p&&Exact(p,"id","cells")&&p["cells"] is JArray list&&list.Count>0&&list.Count<=MaximumCellsPerProgram,"Invalid memory program.");
                string program=Identifier(token["id"],true);var cells=new Dictionary<string,Cell>();
                foreach(var item in (JArray)token["cells"]){
                    Need(++count<=MaximumCells&&item is JObject c&&Exact(c,"id","name","type","value"),"Invalid memory variable.");
                    var type=item["type"];Need(type.ToString(Formatting.None).Length<=2048&&Tokens(type).Count()<=256,"Memory type exceeds its limit.");
                    var value=ProgramValue.Literal(item["value"],ProgramDataType.Read(type));
                    Need(cells.TryAdd(Identifier(item["id"]),new Cell(Text(item["name"]),value)),"Duplicate memory variable.");
                }
                Need(result.TryAdd(program,cells),"Duplicate memory program.");
            }
            return new ProgramMemoryDocument(Text(root["revision"]),result);
        }
        internal bool TryRead(string program,string cell,ProgramDataType expected,out ProgramValue value)
        {
            Need(ProgramId(program)&&Id(cell)&&expected!=null,"Invalid memory read.");value=default;
            if(!programs.TryGetValue(program,out var group)||!group.TryGetValue(cell,out var entry))return false;
            Need(entry.Value.Type==expected,"Saved memory type differs; use a new variable identity or explicitly reset it.");value=entry.Value;return true;
        }
        internal ProgramMemoryDocument WithValues(string program,IReadOnlyDictionary<string,Cell> values)
        {
            Need(ProgramId(program)&&values!=null&&values.Count>0&&values.Count<=MaximumWriteCells,"Invalid memory checkpoint.");
            var group=programs.TryGetValue(program,out var earlier)?new Dictionary<string,Cell>(earlier):new Dictionary<string,Cell>();bool changed=false;
            foreach(var pair in values){
                Need(Id(pair.Key)&&pair.Value!=null,"Invalid memory variable.");
                if(group.TryGetValue(pair.Key,out var old))Need(old.Value.Type==pair.Value.Value.Type,"Saved memory type differs; use a new variable identity or explicitly reset it.");
                changed|=!pair.Value.Same(old);group[pair.Key]=pair.Value;
            }
            if(!changed)return this;
            var next=new Dictionary<string,IReadOnlyDictionary<string,Cell>>(programs) {[program]=group};
            return new ProgramMemoryDocument(Guid.NewGuid().ToString("N"),next);
        }
        // Only an explicit reset removes cells. Checkpoints retain undeclared cells
        // so a temporary definition edit/Undo cannot silently discard saved values.
        internal ProgramMemoryDocument Forget(string program,string cell=null)
        {
            Need(ProgramId(program)&&(cell==null||Id(cell)),"Invalid memory reset.");
            if(!programs.TryGetValue(program,out var earlier)||cell!=null&&!earlier.ContainsKey(cell))return this;
            var next=new Dictionary<string,IReadOnlyDictionary<string,Cell>>(programs);
            if(cell==null||earlier.Count==1)next.Remove(program);
            else {var group=new Dictionary<string,Cell>(earlier);group.Remove(cell);next[program]=group;}
            return new ProgramMemoryDocument(Guid.NewGuid().ToString("N"),next);
        }
        internal bool Retains(string identity)=>programs.Values.SelectMany(x=>x.Values).Any(c=>Tokens(JToken.FromObject(c.Value.Value)).OfType<JValue>().Any(v=>v.Type==JTokenType.String&&(string)v==identity));
    }
}
