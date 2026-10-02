// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Rules
{
    /// <summary>Detached, revision-checked quick edits of canonical invocation nodes.
    /// The book remains the full editor for text, expressions and structure.</summary>
    public sealed class CapabilityQuickEdit
    {
        sealed class Field {
            public string Path,Root,Key;public JObject Schema,Parent;public JArray Array;public int Index;
            public bool Optional,Container,Variant;
            public JToken Value=>Array!=null?Array[Index]:Parent[Key];
            public void Set(JToken value) {if(Array!=null)Array[Index]=value;else Parent[Key]=value;}
        }
        readonly RuleWorkshop workshop;
        readonly RoomEditor editor;
        RuleSequence sequence;
        JObject program;
        List<JObject> nodes=new();
        List<Field> fields=new();
        int revision,nodeIndex,fieldIndex;
        bool saving;
        int precision;
        static readonly double[] increments={0,.01,.05,.1,.5,1,5,15};
        public string PrecisionLabel=>precision==0?"Auto":increments[precision].ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
        public void CyclePrecision() {precision=(precision+1)%increments.Length;Status="Numeric step: "+PrecisionLabel;}
        public bool Dirty {get;private set;}
        public string Status {get;private set;}="Choose a block, then a field";
        public string NodeId=>Node==null?null:(string)Node["id"];
        public string CapabilityId=>Node==null?null:(string)Node["capability"];
        public string ProgramSource=>program?.ToString(Formatting.None);
        public int FieldCount=>fields.Count;
        public string FieldPath=>Current?.Path;
        public bool Stale=>sequence!=null&&(workshop.Revision!=revision||workshop.Selected?.id!=sequence.id);
        JObject Node=>nodeIndex>=0&&nodeIndex<nodes.Count?nodes[nodeIndex]:null;
        Field Current=>fieldIndex>=0&&fieldIndex<fields.Count?fields[fieldIndex]:null;
        BehaviourCatalog.ActionDefinition Definition=>BehaviourCatalog.Action(CapabilityId);
        bool Bound(Field field)=>field!=null&&((JObject)Node["bindings"]).Properties().Any(x=>x.Name==field.Path||field.Container&&x.Name.StartsWith(field.Root+".",StringComparison.Ordinal));
        public CapabilityQuickEdit(RuleWorkshop workshop,RoomEditor editor) {this.workshop=workshop;this.editor=editor;Reload();}
        static string Short(string value,int limit)=>value?.Length>limit?value.Substring(0,limit-3)+"...":value;
        public string Summary {
            get {
                if(sequence==null)return "Create or select a behaviour";
                if(Node==null)return sequence.name+"\nEdit this program in the book";
                var field=Current;string value=field==null?"No editable fields":Bound(field)?"From expression · edit in book":
                    (string)field.Schema["format"]=="programModule"?"Module document · edit in book":field.Variant?(string)CapabilitySchema.Resolve(field.Schema,field.Value)?["title"]??"Unsupported source":field.Value==null?"Not included":field.Container?"Included · Set field removes it":field.Value.ToString(Formatting.None);
                if(field!=null&&!Bound(field)&&(string)field.Schema["x-resource"]=="object"&&field.Value?.Type==JTokenType.String) {
                    string id=(string)field.Value;var item=editor.Read(id);
                    value=item==null?"Missing object":(string.IsNullOrEmpty(item.name)?item.kind.ToString():item.name)+(item.IsBuiltIn?"":" · "+id.Substring(0,4));
                }
                if(field!=null&&!Bound(field)&&field.Value?.Type==JTokenType.String&&field.Schema["x-enum-labels"]?[(string)field.Value]?.Type==JTokenType.String)value=(string)field.Schema["x-enum-labels"][(string)field.Value];
                value=Short(value,28);
                return Short(sequence.name,24)+" · Block "+(nodeIndex+1)+"/"+nodes.Count+" · "+Short(Definition?.Label,30)+
                    "\n"+(field==null?"":(fieldIndex+1)+"/"+fields.Count+" "+Short(field.Path,32)+": "+value)+
                    "\n"+(Stale?"Changed elsewhere · discard to reload":Dirty?"Draft · Apply saves; it does not run":"Saved · values adjust; text and structure use the book");
            }
        }
        public void Say(string value) {Status=value;}
        public void Refresh() {if(!saving&&!Dirty&&(sequence?.id!=workshop.Selected?.id||revision!=workshop.Revision))Reload();}
        public void Reload() {
            string selected=sequence?.id==workshop.Selected?.id?NodeId:workshop.SelectedStep?.id;sequence=workshop.Selected;revision=workshop.Revision;program=null;nodes.Clear();fields.Clear();Dirty=false;
            if(sequence==null) {Status="Create or select a behaviour";return;}
            if(sequence.Compile(out var error)==null) {Status=error+" · Repair in the book";return;}
            program=JObject.Parse(sequence.program);
            nodes=ActionNodes(program);
            nodeIndex=Math.Max(0,nodes.FindIndex(x=>(string)x["id"]==selected));fieldIndex=0;Fields();workshop.SelectLiteralNode(NodeId);Status="Choose a block, then a field";
        }
        public bool Clean() {if(!Dirty)return true;Status="Apply or discard the quick-edit draft first";return false;}
        public void Step(int direction) {
            if(nodes.Count==0)return;nodeIndex=(nodeIndex+direction+nodes.Count)%nodes.Count;fieldIndex=0;Fields();if(!Dirty)workshop.SelectLiteralNode(NodeId);
        }
        public void FieldStep(int direction) {if(fields.Count>0)fieldIndex=(fieldIndex+direction+fields.Count)%fields.Count;}
        void Changed(string status) {Dirty=true;Status=status;Fields();}
        static List<JObject> ActionNodes(JObject source) {
            var result=new List<JObject>();
            foreach(var function in ((JArray)source["functions"]).OfType<JObject>())ProgramModules.Nodes((JArray)function["body"],node=>{if((string)node["op"]=="invoke")result.Add(node);});
            return result;
        }
        void Fields() {
            string selected=Current?.Path;fields=new List<Field>();if(Node==null||Definition==null)return;
            void Walk(JObject schema,JToken value,string path,string root,JObject parent,string key,JArray array,int index,bool optional,int depth) {
                if(depth>12)return;
                if((string)schema["format"]=="programModule") {fields.Add(new Field {Path=path,Root=root,Schema=schema,Parent=parent,Key=key});return;}
                if(schema["oneOf"] is JArray) {
                    fields.Add(new Field {Path=(string)schema["title"]??"Variant",Root=root,Schema=schema,Parent=parent,Key=key,Variant=true});
                    schema=CapabilitySchema.Resolve(schema,value);if(schema==null)return;
                }
                string type=(string)schema["type"];
                if(optional)fields.Add(new Field {Path=path+" (include)",Root=root,Schema=schema,Parent=parent,Key=key,Array=array,Index=index,Optional=true,Container=true});
                if(value==null)return;
                if(type=="object"&&value is JObject obj) {
                    var required=(JArray)schema["required"];
                    foreach(var child in ((JObject)schema["properties"]).Properties())
                        Walk((JObject)child.Value,obj[child.Name],path.Length==0?child.Name:path+"."+child.Name,root.Length==0?child.Name:root,obj,child.Name,null,0,!required.Any(x=>(string)x==child.Name),depth+1);
                } else if(type=="array"&&value is JArray entries) {
                    for(int i=0;i<entries.Count;i++)Walk((JObject)schema["items"],entries[i],path+"["+i+"]",root,null,null,entries,i,false,depth+1);
                } else fields.Add(new Field {Path=path,Root=root,Schema=schema,Parent=parent,Key=key,Array=array,Index=index});
            }
            var args=(JObject)Node["arguments"];var shape=Definition.InputSchema;
            Walk(shape,args,"","",Node,"arguments",null,0,false,0);
            int found=fields.FindIndex(x=>x.Path==selected);fieldIndex=found>=0?found:Mathf.Clamp(fieldIndex,0,Math.Max(0,fields.Count-1));
        }
        JToken Initial(JObject schema) {
            if(schema["examples"] is JArray examples&&examples.Count>0)return examples[0].DeepClone();
            if(schema["oneOf"] is JArray variants)return Initial((JObject)variants[0]);
            if(schema["enum"] is JArray choices)return choices[0].DeepClone();
            switch((string)schema["type"]) {
                case "object":
                    var obj=new JObject();foreach(string key in ((JArray)schema["required"]).Values<string>())obj[key]=Initial((JObject)schema["properties"][key]);
                    if((string)schema["format"]=="unitQuaternion")obj["w"]=1;return obj;
                case "array":return new JArray(Enumerable.Range(0,(int)schema["minItems"]).Select(_=>Initial((JObject)schema["items"])));
                case "boolean":return new JValue(false);
                case "number":case "integer":return new JValue(Math.Max((double)schema["minimum"],Math.Min(0,(double)schema["maximum"])));
                default:
                    if((string)schema["x-resource"]=="object") {
                        var options=Objects(schema);return new JValue(options.Contains(editor.SelectedId)?editor.SelectedId:options.FirstOrDefault()??"");
                    }
                    return new JValue("");
            }
        }
        string[] Objects(JObject schema)=>editor.Snapshot().objects.Select(x=>x.id).Where(id=>CapabilityArguments.Validate(new JValue(id),schema,out _)).ToArray();
        public void CycleCapability(int direction=1) {
            if(Node==null) {Status="Choose an action block; add program structure in the book";return;}
            if(((JObject)Node["bindings"]).Count>0||Node["results"]!=null) {Status="Change wired block types in the book; expressions and results are preserved";return;}
            var choices=BehaviourCatalog.Actions;int index=choices.ToList().FindIndex(x=>x.Id==CapabilityId);
            var definition=choices[(index+direction+choices.Count)%choices.Count];
            Node["capability"]=definition.Id;Node["version"]=definition.Version;Node["arguments"]=definition.Example??Initial(definition.InputSchema);
            fieldIndex=0;fields.Clear();Changed("Block type changed in draft");
        }
        public void Adjust(int direction) {
            var field=Current;if(field==null)return;if(Bound(field)) {Status="This field comes from an expression; edit it in the book";return;}
            if(field.Variant) {ChangeVariant(field,direction);return;}
            if((bool?)field.Schema["x-static"]==true) {Status=(string)field.Schema["format"]=="programModule"?"Edit the module document in the book":"Use the variant field to change this choice";return;}
            if(field.Container) {ToggleOptional(field);return;}
            var schema=field.Schema;
            if((string)schema["x-resource"]=="object") {
                Cycle(field,Objects(schema),direction);return;
            }
            if(schema["enum"] is JArray values) {Cycle(field,values.Values<string>().ToArray(),direction);return;}
            string type=(string)schema["type"];
            if(type=="boolean") {field.Set(new JValue(field.Value?.Type==JTokenType.Boolean&&!(bool)field.Value));Changed("Value changed in draft");return;}
            if(type=="number"||type=="integer") {
                double min=(double)schema["minimum"],max=(double)schema["maximum"],span=max-min;
                double step=precision>0?increments[precision]:span>=360?15:span>=10?1:.05;
                if(type=="integer")step=Math.Max(1,Math.Round(step));
                double value=field.Value?.Type==JTokenType.Integer||field.Value?.Type==JTokenType.Float?(double)field.Value:0;
                field.Set(new JValue(Math.Round(Math.Clamp(value+direction*step,min,max),6)));Changed("Value changed in draft");return;
            }
            Status="Edit text, motion selections and detailed values in the book";
        }
        void ChangeVariant(Field field,int direction) {
            if(((JObject)Node["bindings"]).Count>0) {Status="Change wired variants in the book; expressions are preserved";return;}
            var variants=(JArray)field.Schema["oneOf"];int index=variants.IndexOf(CapabilitySchema.Resolve(field.Schema,field.Value));
            var selected=(JObject)variants[(index+direction+variants.Count)%variants.Count];var next=(JObject)Initial(selected);
            var previous=(JObject)field.Value;
            foreach(var property in ((JObject)selected["properties"]).Properties())
                if((bool?)property.Value["x-static"]!=true&&previous[property.Name]!=null&&CapabilityArguments.Validate(previous[property.Name],(JObject)property.Value,out _))next[property.Name]=previous[property.Name].DeepClone();
            field.Set(next);Changed("Variant changed in draft; compatible values retained");
        }
        void Cycle(Field field,string[] options,int direction) {
            if(options.Length==0) {Status="No compatible object is available";return;}
            int index=Array.IndexOf(options,(string)field.Value);field.Set(new JValue(options[(index+direction+options.Length)%options.Length]));Changed("Choice changed in draft");
        }
        void ToggleOptional(Field field) {
            if(!field.Optional)return;
            if(field.Value!=null)field.Parent.Remove(field.Key);else field.Set(Initial(field.Schema));Changed("Optional field changed in draft");
        }
        public void SetField() {
            var field=Current;if(field==null)return;
            if(field.Variant||(bool?)field.Schema["x-static"]==true) {Adjust(1);return;}if(Bound(field)) {Status="This field comes from an expression; edit it in the book";return;}
            if(field.Container) {ToggleOptional(field);return;}
            if((string)field.Schema["x-resource"]=="object") {
                if(editor.SelectedId==null||!CapabilityArguments.Validate(new JValue(editor.SelectedId),field.Schema,out _)) {Status="Select an object allowed by this field";return;}
                field.Set(new JValue(editor.SelectedId));Changed("Room selection assigned in draft");return;
            }
            if((string)field.Schema["type"]=="boolean") {Adjust(1);return;}
            Status="Use Value - / +, or edit this value in the book";
        }
        public void AddBlock() {
            if(Node?.Parent is not JArray body) {Status="Add program structure in the book";return;}
            var wait=BehaviourCatalog.Action("time.wait");
            var added=new JObject {["id"]="quick_"+Guid.NewGuid().ToString("N").Substring(0,24),["op"]="invoke",["capability"]=wait.Id,["version"]=wait.Version,
                ["arguments"]=new JObject {["seconds"]=1},["bindings"]=new JObject()};
            body.Insert(body.IndexOf(Node)+1,added);nodes=ActionNodes(program);
            nodeIndex=nodes.IndexOf(added);fieldIndex=0;fields.Clear();Changed("Wait block added in draft");
        }
        public void DeleteBlock() {
            if(Node==null)return;
            if(Node["results"]!=null) {Status="Delete result-producing blocks in the book so their users remain visible";return;}
            if(nodes.Count<=1) {Status="Keep one action block, or edit the structure in the book";return;}
            Node.Remove();nodes=ActionNodes(program);
            nodeIndex=Mathf.Clamp(nodeIndex,0,nodes.Count-1);fieldIndex=0;fields.Clear();Changed("Block removed in draft");
        }
        public bool Apply() {
            if(!Dirty) {Status="There are no draft changes";return false;}
            if(Stale) {Status="Behaviours changed. Draft retained; discard to load the latest version";return false;}
            var candidate=(JObject)program.DeepClone();var resources=((JArray)candidate["resources"]).Values<string>().ToList();
            foreach(var node in ActionNodes(candidate)) {
                var definition=BehaviourCatalog.Action((string)node["capability"]);
                foreach(var id in CapabilityArguments.LiteralResources((JObject)node["arguments"],definition.InputSchema,(JObject)node["bindings"],(int)candidate["version"]))
                    if(!resources.Contains(id))resources.Add(id);
            }
            candidate["resources"]=new JArray(resources);var updated=sequence.Copy();updated.program=candidate.ToString(Formatting.None);
            saving=true;bool accepted;
            try {accepted=workshop.Execute(new RuleRequest {action="edit",revision=revision,edits=new[] {new RuleEdit {kind="save",sequence=updated}}},out var error,out _);Status=error;}
            finally {saving=false;}
            if(!accepted)return false;Reload();Status="Draft applied as one Undo edit; playback has not started";return true;
        }
        public bool SelectLegacyStep() {
            if(!Clean())return false;
            if(workshop.SelectLiteralNode(NodeId))return true;
            Status="Fit props on this structured block in the book";return false;
        }
    }
}
