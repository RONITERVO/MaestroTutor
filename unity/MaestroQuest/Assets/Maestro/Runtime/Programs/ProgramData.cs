// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public enum ProgramType { Void, Number, Boolean, Text, List, Record }
    /// <summary>Structural, immutable type identity; no CLR/user code types are loaded.</summary>
    public sealed class ProgramDataType : IEquatable<ProgramDataType>
    {
        public readonly ProgramType Kind;public readonly ProgramDataType Item;
        public readonly IReadOnlyDictionary<string,ProgramDataType> Fields;
        readonly string key;
        ProgramDataType(ProgramType kind,ProgramDataType item=null,IDictionary<string,ProgramDataType> fields=null) {
            Kind=kind;Item=item;Fields=fields==null?null:new ReadOnlyDictionary<string,ProgramDataType>(new Dictionary<string,ProgramDataType>(fields));
            key=kind==ProgramType.List?"["+item.key+"]":kind==ProgramType.Record?"{"+string.Join(",",Fields.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+":"+x.Value.key))+"}":kind.ToString();
        }
        static readonly ProgramDataType[] scalars={new(ProgramType.Void),new(ProgramType.Number),new(ProgramType.Boolean),new(ProgramType.Text)};
        public static implicit operator ProgramDataType(ProgramType type)=>type<=ProgramType.Text?scalars[(int)type]:throw new ArgumentException("Structured type needs a shape");
        public bool Equals(ProgramDataType other)=>other!=null&&key==other.key;
        public override bool Equals(object other)=>other is ProgramDataType type&&Equals(type);
        public override int GetHashCode()=>key.GetHashCode();
        public static bool operator ==(ProgramDataType a,ProgramDataType b)=>ReferenceEquals(a,b)||a is not null&&b is not null&&a.key==b.key;
        public static bool operator !=(ProgramDataType a,ProgramDataType b)=>!(a==b);
        public override string ToString()=>Kind.ToString();
        internal static void Need(bool condition,string message){if(!condition)throw new ProgramFault(message);}
        static bool Field(string name)=>BehaviourProgram.Name(name)&&name!="__proto__"&&name!="constructor"&&name!="prototype";
        public static ProgramDataType Read(JToken token,int depth=0) {
            Need(depth<=4,"Value type nesting exceeds 4");
            if(token?.Type==JTokenType.String)return (string)token switch {"void"=>ProgramType.Void,"number"=>ProgramType.Number,"boolean"=>ProgramType.Boolean,"text"=>ProgramType.Text,_=>throw new ProgramFault("Unknown value type")};
            Need(token is JObject o&&o.Count==1,"Expected a value type");var value=(JObject)token;
            if(value.ContainsKey("list")){var item=Read(value["list"],depth+1);Need(item.Kind!=ProgramType.Void,"List item cannot be void");return new(ProgramType.List,item);}
            Need(value["record"] is JObject fields&&fields.Count<=8,"A record type needs up to 8 fields");var result=new Dictionary<string,ProgramDataType>();
            foreach(var p in ((JObject)value["record"]).Properties()){Need(Field(p.Name),"Invalid record field");var type=Read(p.Value,depth+1);Need(type.Kind!=ProgramType.Void,"Record field cannot be void");result.Add(p.Name,type);}
            return new(ProgramType.Record,fields:result);
        }
        public static ProgramDataType Infer(JToken value,int depth=0) {
            Need(depth<=4,"Value nesting exceeds 4");
            if(value?.Type==JTokenType.String)return ProgramType.Text;if(value?.Type==JTokenType.Boolean)return ProgramType.Boolean;if(value?.Type==JTokenType.Float||value?.Type==JTokenType.Integer)return ProgramType.Number;
            if(value is JArray array){Need(array.Count>0&&array.Count<=32,"An empty list needs an explicit type; lists have at most 32 items");var item=Infer(array[0],depth+1);Need(array.All(v=>Infer(v,depth+1)==item),"List items have different types");return new(ProgramType.List,item);}
            Need(value is JObject fields&&fields.Count<=8,"Expected a bounded value");var result=new Dictionary<string,ProgramDataType>();
            foreach(var p in ((JObject)value).Properties()){Need(Field(p.Name),"Invalid record field");result.Add(p.Name,Infer(p.Value,depth+1));}return new(ProgramType.Record,fields:result);
        }
        internal static ProgramDataType Operation(string op,ProgramDataType[] types,JToken field) {
            if(!new[]{"length","at","append","replace","remove","field","withField"}.Contains(op))return null;
            var a=types[0];
            if(op=="field"||op=="withField"){
                Need(a.Kind==ProgramType.Record&&field?.Type==JTokenType.String&&a.Fields.ContainsKey((string)field),"Choose a literal record field");
                var t=a.Fields[(string)field];Need(types.Length==(op=="field"?2:3)&&types[1]==ProgramType.Text&&(op=="field"||types[2]==t),"Record operation types differ");return op=="field"?t:a;
            }
            Need(a.Kind==ProgramType.List,"List operation needs a list");Need(types.Length==(op=="length"?1:op=="replace"?3:2),"Invalid list operation arguments");
            if(op=="append")Need(types[1]==a.Item,"List item type differs");else if(op!="length")Need(types[1]==ProgramType.Number,"List index needs a number");
            if(op=="replace")Need(types[2]==a.Item,"List item type differs");return op=="length"?ProgramType.Number:op=="at"?a.Item:a;
        }
    }
    /// <summary>Detached immutable values. Only explicitly copied JSON crosses an API boundary.</summary>
    public readonly struct ProgramValue
    {
        readonly ProgramDataType shape;readonly JToken data;
        public ProgramDataType Type=>shape??(ProgramDataType)ProgramType.Void;
        public readonly double Number;public readonly bool Boolean;public readonly string Text;
        public readonly int Nodes,Characters;
        public ProgramValue(double value){shape=ProgramType.Number;Number=value;Boolean=false;Text=null;data=null;Nodes=1;Characters=30;}
        public ProgramValue(bool value){shape=ProgramType.Boolean;Boolean=value;Number=0;Text=null;data=null;Nodes=1;Characters=5;}
        public ProgramValue(string value){shape=ProgramType.Text;Text=value;Number=0;Boolean=false;data=null;Nodes=1;Characters=JsonConvert.ToString(value).Length;}
        ProgramValue(JToken value,ProgramDataType type,int nodes,int characters){shape=type;data=value.DeepClone();Number=0;Boolean=false;Text=null;Nodes=nodes;Characters=characters;}
        // Box scalar arms explicitly: JToken implicit conversions otherwise make the whole switch return JSON wrappers.
        public object Value=>Type.Kind switch {ProgramType.Number=>(object)Number,ProgramType.Boolean=>(object)Boolean,ProgramType.Text=>(object)Text,ProgramType.List or ProgramType.Record=>data.DeepClone(),_=>null};
        public string Display=>data!=null?data.ToString(Formatting.None):Convert.ToString(Value,System.Globalization.CultureInfo.InvariantCulture);
        public static ProgramValue Literal(JToken value,ProgramDataType declared=null) {
            var type=declared??ProgramDataType.Infer(value);int nodes=0;
            void Check(JToken v,ProgramDataType t,int depth){
                ProgramDataType.Need(depth<=4&&++nodes<=128,"Value nesting or node limit exceeded");
                if(t.Kind==ProgramType.Number){ProgramDataType.Need((v?.Type==JTokenType.Integer||v?.Type==JTokenType.Float)&&double.IsFinite((double)v)&&Math.Abs((double)v)<=1000000,"Number exceeds its limit");return;}
                if(t.Kind==ProgramType.Boolean){ProgramDataType.Need(v?.Type==JTokenType.Boolean,"Expected a boolean");return;}
                if(t.Kind==ProgramType.Text){ProgramDataType.Need(v?.Type==JTokenType.String&&((string)v).Length<=128&&!((string)v).Any(char.IsControl),"Text exceeds its limit");return;}
                if(t.Kind==ProgramType.List){ProgramDataType.Need(v is JArray list&&list.Count<=32,"Expected a list of at most 32 items");foreach(var item in (JArray)v)Check(item,t.Item,depth+1);return;}
                ProgramDataType.Need(t.Kind==ProgramType.Record&&v is JObject obj&&obj.Count==t.Fields.Count&&t.Fields.Keys.All(obj.ContainsKey),"Record fields differ from its type");
                foreach(var p in t.Fields)Check(v[p.Key],p.Value,depth+1);
            }
            Check(value,type,0);int characters=Cost(value);ProgramDataType.Need(characters<=1024,"Value exceeds its 1024 character budget");
            return type.Kind switch {ProgramType.Number=>new ProgramValue((double)value),ProgramType.Boolean=>new ProgramValue((bool)value),ProgramType.Text=>new ProgramValue((string)value),_=>new ProgramValue(value,type,nodes,characters)};
        }
        static int Cost(JToken value) {
            if(value.Type==JTokenType.Integer||value.Type==JTokenType.Float)return 30;if(value.Type==JTokenType.Boolean)return 5;if(value.Type==JTokenType.String)return JsonConvert.ToString((string)value).Length;
            if(value is JArray list)return 2+Math.Max(0,list.Count-1)+list.Sum(Cost);
            var fields=(JObject)value;return 2+Math.Max(0,fields.Count-1)+fields.Properties().Sum(p=>JsonConvert.ToString(p.Name).Length+1+Cost(p.Value));
        }
        public bool Same(ProgramValue value)=>Type==value.Type&&(Type.Kind switch {ProgramType.Number=>Number==value.Number,ProgramType.Boolean=>Boolean==value.Boolean,ProgramType.Text=>Text==value.Text,ProgramType.List or ProgramType.Record=>EqualData(data,value.data),_=>true});
        static bool EqualData(JToken a,JToken b) {
            if(a.Type==JTokenType.Integer||a.Type==JTokenType.Float)return (double)a==(double)b;
            if(a is JArray list){var other=(JArray)b;if(list.Count!=other.Count)return false;for(int i=0;i<list.Count;i++)if(!EqualData(list[i],other[i]))return false;return true;}
            if(a is JObject obj){foreach(var p in obj.Properties())if(!EqualData(p.Value,b[p.Name]))return false;return true;}
            return JToken.DeepEquals(a,b);
        }
        public ProgramValue Operation(string op,ProgramValue b,ProgramValue c) {
            if(op=="length")return new ProgramValue(((JArray)data).Count);
            if(op=="field")return Literal(data[b.Text],Type.Fields[b.Text]);
            if(op=="at"||op=="replace"||op=="remove")ProgramDataType.Need(Math.Truncate(b.Number)==b.Number&&b.Number>=0&&b.Number<((JArray)data).Count,"List index is out of range");
            if(op=="at")return Literal(data[(int)b.Number],Type.Item);
            var next=data.DeepClone();
            if(op=="withField")next[b.Text]=JToken.FromObject(c.Value);
            else if(op=="append")((JArray)next).Add(JToken.FromObject(b.Value));
            else if(op=="replace")next[(int)b.Number]=JToken.FromObject(c.Value);
            else if(op=="remove")((JArray)next).RemoveAt((int)b.Number);
            else throw new ProgramFault("Unknown data operation");
            return Literal(next,Type);
        }
    }
}
