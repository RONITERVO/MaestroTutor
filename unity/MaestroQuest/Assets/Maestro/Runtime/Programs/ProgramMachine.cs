// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Programs
{
    public enum ProgramYield { Action, Yield, Waiting, Signal, Completed, Failed }
    public sealed class ProgramWait {public string Event,Source;public float Seconds;}
    public sealed class ProgramSignal {public string Event;public ProgramValue Value;}
    public interface IProgramFacts {bool TryRead(string name,out ProgramValue value);}
    /// <summary>Cooperatively evaluated statements; native action completion remains the host's responsibility.</summary>
    public sealed class ProgramMachine
    {
        sealed class Scope {public ProgramFunction Function;public Dictionary<string,ProgramValue> Values;}
        sealed class Frame {public JArray Body;public int Index,Remaining=1;public bool Forever;public Scope Scope;public bool Function;public Scope Caller;public string Result;}
        public const int MaximumInstructions=65536;
        readonly BehaviourProgram program;
        readonly IProgramFacts facts;
        readonly Stack<Frame> frames=new();
        Scope observed;
        bool terminal;
        readonly Dictionary<string,ProgramValue> state;
        Scope waitingScope;string receivedVariable,valueVariable;
        public ProgramWait Wait {get;private set;}
        public ProgramSignal Signal {get;private set;}
        public IReadOnlyDictionary<string,ProgramValue> State=>new Dictionary<string,ProgramValue>(state);
        public string NodeId {get;private set;}
        public string Error {get;private set;}
        public int Instructions {get;private set;}
        public ProgramValue Result {get;private set;}
        public IReadOnlyDictionary<string,ProgramValue> Locals=>observed==null ? new Dictionary<string,ProgramValue>() : new Dictionary<string,ProgramValue>(observed.Values);
        public string Function=>observed?.Function.Name;
        public ProgramMachine(BehaviourProgram program,IProgramFacts facts)
        {
            this.program=program??throw new ArgumentNullException(nameof(program));this.facts=facts;state=new(program.InitialState);
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
            action=null;Signal=null;if(Wait!=null)return ProgramYield.Waiting;if(terminal)return Error==null?ProgramYield.Completed:ProgramYield.Failed;
            if(budget<1||budget>256)throw new ArgumentOutOfRangeException(nameof(budget));
            try {
                int began=Instructions;
                while(Instructions-began<budget) {
                    if(frames.Count==0) {terminal=true;return ProgramYield.Completed;}
                    Charge();
                    var frame=frames.Peek();
                    if(frame.Index>=frame.Body.Count) {
                        if(frame.Forever||--frame.Remaining>0)frame.Index=0;
                        else if(frame.Function) {if(frame.Scope.Function.Returns!=ProgramType.Void)throw new ProgramFault("Function ended without returning a value");Return(default);}
                        else frames.Pop();continue;
                    }
                    observed=frame.Scope;var node=(JObject)frame.Body[frame.Index++];NodeId=(string)node["id"];string op=(string)node["op"];
                    ProgramValue Eval(string key)=>Evaluate(node[key],frame.Scope);
                    switch(op) {
                        case "setState":state[(string)node["variable"]]=Eval("value");break;
                        case "forever":frames.Push(new Frame {Body=(JArray)node["body"],Scope=frame.Scope,Forever=true});break;
                        case "sleep":
                            double seconds=Eval("seconds").Number;if(!double.IsFinite(seconds)||seconds<.1||seconds>3600)throw new ProgramFault("Delay must be 0.1 to 3600 seconds");
                            Wait=new ProgramWait {Seconds=(float)seconds};return ProgramYield.Waiting;
                        case "awaitEvent":
                            double timeout=Eval("timeout").Number;if(!double.IsFinite(timeout)||timeout!=0&&(timeout<.1||timeout>3600))throw new ProgramFault("Event timeout must be zero or 0.1 to 3600 seconds");
                            waitingScope=frame.Scope;receivedVariable=(string)node["received"];valueVariable=(string)node["value"];
                            waitingScope.Values[receivedVariable]=new ProgramValue(false);
                            Wait=new ProgramWait {Event=(string)node["event"],Source=(string)node["source"],Seconds=(float)timeout};return ProgramYield.Waiting;
                        case "emitEvent":Signal=new ProgramSignal {Event=(string)node["event"],Value=Eval("value")};return ProgramYield.Signal;
                        case "set":frame.Scope.Values[(string)node["variable"]]=Eval("value");break;
                        case "if":Block((JArray)node[Eval("test").Boolean?"then":"else"],frame.Scope);break;
                        case "switch":var value=Eval("value");var arm=((JArray)node["cases"]).FirstOrDefault(x=>ProgramValue.Literal(x["value"]).Same(value));Block((JArray)(arm==null?node["default"]:arm["body"]),frame.Scope);break;
                        case "repeat":
                            double repeats=Eval("count").Number;if(repeats<0||repeats>10000||Math.Truncate(repeats)!=repeats)throw new ProgramFault("Repeat count must be an integer from 0 to 10000");
                            Block((JArray)node["body"],frame.Scope,(int)repeats);break;
                        case "call":Call((string)node["function"],((JArray)node["args"]).Select(x=>Evaluate(x,frame.Scope)).ToArray(),frame.Scope,(string)node["result"]);break;
                        case "return":Return(node.ContainsKey("value")?Eval("value"):default);break;
                        case "invoke":
                            var arguments=(JObject)node["arguments"].DeepClone();
                            foreach(var binding in ((JObject)node["bindings"]).Properties())arguments[binding.Name]=JToken.FromObject(Evaluate(binding.Value,frame.Scope).Value);
                            if(!BehaviourCatalog.TryInvocation((string)node["capability"],(int)node["version"],arguments,out action,out var error))throw new ProgramFault(error??"Invalid computed capability arguments");
                            if(!RuleDocument.Targets(action).All(program.Allows))throw new ProgramFault("Computed target is not a declared resource");
                            action.id=NodeId;return ProgramYield.Action;
                    }
                }return ProgramYield.Yield;
            } catch(ProgramFault error) {Error=error.Message;frames.Clear();terminal=true;action=null;return ProgramYield.Failed;}
        }
        public void Resume(bool received,ProgramValue value=default)
        {
            if(Wait==null)throw new InvalidOperationException("This program is not waiting");
            if(received&&Wait.Event!=null) {
                if(value.Type!=program.EventType(Wait.Event))throw new ArgumentException("Event payload type differs");
                waitingScope.Values[receivedVariable]=new ProgramValue(true);waitingScope.Values[valueVariable]=value;
            }
            Wait=null;waitingScope=null;receivedVariable=valueVariable=null;
            BeginActivation(); // Scope/state/stack remain intact.
        }
        internal void BeginActivation()=>Instructions=0;
        void Charge() {if(++Instructions>MaximumInstructions)throw new ProgramFault("Program instruction budget exhausted");}
        ProgramValue Evaluate(JToken token,Scope scope)
        {
            Charge();
            var e=(JObject)token;
            if(e.ContainsKey("value"))return ProgramValue.Literal(e["value"]);
            if(e.ContainsKey("var"))return scope.Values[(string)e["var"]];
            if(e.ContainsKey("state"))return state[(string)e["state"]];
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
