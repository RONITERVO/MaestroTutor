// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Programs
{
    public enum ProgramYield { Action, Yield, Completed, Failed }
    public interface IProgramFacts {bool TryRead(string name,out ProgramValue value);}
    /// <summary>Cooperatively evaluated statements; native action completion remains the host's responsibility.</summary>
    public sealed class ProgramMachine
    {
        sealed class Scope {public ProgramFunction Function;public Dictionary<string,ProgramValue> Values;}
        sealed class Frame {public JArray Body;public int Index,Remaining=1;public Scope Scope;public bool Function;public Scope Caller;public string Result;}
        public const int MaximumInstructions=65536;
        readonly BehaviourProgram program;
        readonly IProgramFacts facts;
        readonly Stack<Frame> frames=new();
        Scope observed;
        bool terminal;
        public string NodeId {get;private set;}
        public string Error {get;private set;}
        public int Instructions {get;private set;}
        public ProgramValue Result {get;private set;}
        public IReadOnlyDictionary<string,ProgramValue> Locals=>observed==null ? new Dictionary<string,ProgramValue>() : new Dictionary<string,ProgramValue>(observed.Values);
        public string Function=>observed?.Function.Name;
        public ProgramMachine(BehaviourProgram program,IProgramFacts facts)
        {
            this.program=program??throw new ArgumentNullException(nameof(program));this.facts=facts;
            Call(program.Entry,Array.Empty<ProgramValue>(),null,null);
        }
        void Call(string name,ProgramValue[] args,Scope caller,string result)
        {
            var function=program.Function(name);var scope=new Scope {Function=function,Values=new Dictionary<string,ProgramValue>(function.Initial)};
            for(int i=0;i<args.Length;i++)scope.Values.Add(function.Parameters[i],args[i]);
            frames.Push(new Frame {Body=function.Body,Scope=scope,Function=true,Caller=caller,Result=result});
        }
        void Block(JArray body,Scope scope,int repeat=1) {if(repeat>0&&body.Count>0)frames.Push(new Frame {Body=body,Scope=scope,Remaining=repeat});}
        void Return(ProgramValue value)
        {
            while(frames.Count>0) {
                var frame=frames.Pop();if(!frame.Function)continue;
                if(frame.Result!=null)frame.Caller.Values[frame.Result]=value;
                if(frames.Count==0)Result=value;return;
            }
        }
        public ProgramYield Advance(out RuleStep action,int budget=32)
        {
            action=null;if(terminal)return Error==null?ProgramYield.Completed:ProgramYield.Failed;
            if(budget<1||budget>256)throw new ArgumentOutOfRangeException(nameof(budget));
            try {
                int began=Instructions;
                while(Instructions-began<budget) {
                    if(frames.Count==0) {terminal=true;return ProgramYield.Completed;}
                    Charge();
                    var frame=frames.Peek();
                    if(frame.Index>=frame.Body.Count) {
                        if(--frame.Remaining>0)frame.Index=0;
                        else if(frame.Function) {if(frame.Scope.Function.Returns!=ProgramType.Void)throw new ProgramFault("Function ended without returning a value");Return(default);}
                        else frames.Pop();continue;
                    }
                    observed=frame.Scope;var node=(JObject)frame.Body[frame.Index++];NodeId=(string)node["id"];string op=(string)node["op"];
                    ProgramValue Eval(string key)=>Evaluate(node[key],frame.Scope);
                    switch(op) {
                        case "set":frame.Scope.Values[(string)node["variable"]]=Eval("value");break;
                        case "if":Block((JArray)node[Eval("test").Boolean?"then":"else"],frame.Scope);break;
                        case "switch":var value=Eval("value");var arm=((JArray)node["cases"]).FirstOrDefault(x=>ProgramValue.Literal(x["value"]).Same(value));Block((JArray)(arm==null?node["default"]:arm["body"]),frame.Scope);break;
                        case "repeat":
                            double repeats=Eval("count").Number;if(repeats<0||repeats>10000||Math.Truncate(repeats)!=repeats)throw new ProgramFault("Repeat count must be an integer from 0 to 10000");
                            Block((JArray)node["body"],frame.Scope,(int)repeats);break;
                        case "call":Call((string)node["function"],((JArray)node["args"]).Select(x=>Evaluate(x,frame.Scope)).ToArray(),frame.Scope,(string)node["result"]);break;
                        case "return":Return(node.ContainsKey("value")?Eval("value"):default);break;
                        case "action":
                            action=program.Action(NodeId);
                            foreach(var binding in ((JObject)node["bindings"]).Properties())Bind(action,binding.Name,Evaluate(binding.Value,frame.Scope));
                            if(!BehaviourProgram.ValidStep(action,out var error))throw new ProgramFault(error??"Invalid computed native arguments");
                            if(!RuleDocument.Targets(action).All(program.Allows))throw new ProgramFault("Computed target is not a declared resource");
                            return ProgramYield.Action;
                    }
                }return ProgramYield.Yield;
            } catch(ProgramFault error) {Error=error.Message;frames.Clear();terminal=true;action=null;return ProgramYield.Failed;}
        }
        static void Bind(RuleStep step,string name,ProgramValue value)
        {
            switch(name) {
                case "targetId":step.targetId=value.Text;break;case "motionId":step.motionId=value.Text;break;
                case "seconds":step.seconds=(float)value.Number;break;case "loop":step.loop=value.Boolean;break;
                case "gesture":case "clipIndex":
                    if(Math.Truncate(value.Number)!=value.Number||value.Number<0||value.Number>31)throw new ProgramFault("Native enum/index must be a bounded integer");
                    if(name=="gesture")step.gesture=(RuleGesture)(int)value.Number;else step.clipIndex=(int)value.Number;break;
            }
        }
        void Charge() {if(++Instructions>MaximumInstructions)throw new ProgramFault("Program instruction budget exhausted");}
        ProgramValue Evaluate(JToken token,Scope scope)
        {
            Charge();
            var e=(JObject)token;
            if(e.ContainsKey("value"))return ProgramValue.Literal(e["value"]);
            if(e.ContainsKey("var"))return scope.Values[(string)e["var"]];
            if(e.ContainsKey("fact")) {
                string name=(string)e["fact"];
                if(facts==null||!facts.TryRead(name,out var value)||value.Type!=BehaviourProgram.Facts[name])throw new ProgramFault("Room fact unavailable: "+name);
                if(value.Type==ProgramType.Number&&(!double.IsFinite(value.Number)||Math.Abs(value.Number)>1000000)||value.Type==ProgramType.Text&&(value.Text==null||value.Text.Length>128))throw new ProgramFault("Room fact is invalid: "+name);
                return value;
            }
            string op=(string)e["op"];var args=(JArray)e["args"];var a=Evaluate(args[0],scope);
            if(op=="not")return new ProgramValue(!a.Boolean);
            if(op=="and"&&!a.Boolean||op=="or"&&a.Boolean)return a;
            var b=Evaluate(args[1],scope);
            if(op=="and"||op=="or")return b;
            if(op=="eq"||op=="ne")return new ProgramValue(op=="eq"?a.Same(b):!a.Same(b));
            if(op=="lt")return new ProgramValue(a.Number<b.Number);if(op=="le")return new ProgramValue(a.Number<=b.Number);
            if(op=="gt")return new ProgramValue(a.Number>b.Number);if(op=="ge")return new ProgramValue(a.Number>=b.Number);
            if((op=="div"||op=="mod")&&b.Number==0)throw new ProgramFault("Division by zero");
            double result=op switch {"add"=>a.Number+b.Number,"sub"=>a.Number-b.Number,"mul"=>a.Number*b.Number,"div"=>a.Number/b.Number,_=>a.Number%b.Number};
            if(!double.IsFinite(result)||Math.Abs(result)>1000000)throw new ProgramFault("Arithmetic result exceeds its limit");return new ProgramValue(result);
        }
    }
}
