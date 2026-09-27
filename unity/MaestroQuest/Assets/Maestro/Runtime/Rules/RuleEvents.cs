// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleScheduler
    {
        // Receivers are captured at emission. An event cannot reach a later wait,
        // including a later wait in the same run, and cannot recurse into a handler.
        sealed class Delivery {public Run Run;public int Serial;}
        sealed class EventMessage {public Delivery[] Receivers;public ProgramValue Value;public float At;public int Depth;}
        readonly Dictionary<string,HashSet<Run>> subscriptions=new(StringComparer.Ordinal);
        readonly Queue<EventMessage> eventQueue=new();
        float lastNow;
        public const int MaximumEvents=64,MaximumEventDepth=16;
        public int EventQueueCount=>eventQueue.Count;
        public int EventsDropped {get;private set;}
        static bool ValidValue(ProgramValue value)=>value.Type==ProgramType.Boolean ||
            value.Type==ProgramType.Number&&double.IsFinite(value.Number)&&Math.Abs(value.Number)<=1000000 ||
            value.Type==ProgramType.Text&&value.Text!=null&&value.Text.Length<=128&&!value.Text.Any(char.IsControl);
        public static bool ValidEventValue(JToken token) {
            if(token==null || token.Type!=JTokenType.Boolean&&token.Type!=JTokenType.String&&token.Type!=JTokenType.Integer&&token.Type!=JTokenType.Float)return false;
            return ValidValue(ProgramValue.Literal(token));
        }
        public static bool ValidSignalWire(JObject command) {
            if(command==null||command.Count!=2||(string)command["action"]!="rules"||command["rule"] is not JObject rule)return false;
            var keys=new[] {"action","revision","eventName","value"};
            return rule.Count==keys.Length&&keys.All(rule.ContainsKey)&&(string)rule["action"]=="signal"&&
                rule["revision"]?.Type==JTokenType.Integer&&(double)rule["revision"]>=1&&(double)rule["revision"]<=int.MaxValue&&
                rule["eventName"]?.Type==JTokenType.String&&System.Text.RegularExpressions.Regex.IsMatch((string)rule["eventName"],@"^user\.[a-zA-Z0-9_]{1,32}$")&&ValidEventValue(rule["value"]);
        }
        public bool Signal(string name,ProgramValue value,float now,out string status)
        {
            status="Choose a declared custom event and matching payload";
            if(!ValidValue(value))return false;
            var declarations=document.sequences.Where(x=>!unavailable.ContainsKey(x.id)).Select(x=>x.Compile(out _)).Where(x=>name!=null&&x.CustomEvents.ContainsKey(name)).ToArray();
            if(declarations.Length==0||declarations.Any(x=>x.CustomEvents[name]!=value.Type))return false;
            return EnqueueEvent(name,"",value,now,0,out status);
        }
        bool EnqueueEvent(string name,string source,ProgramValue value,float now,int depth,out string status)
        {
            status="Events are paused";if(suspended||!float.IsFinite(now))return false;
            if(depth>MaximumEventDepth) {status="Event chain limit reached; add a timer or wait for an external event";return false;}
            if(!ValidValue(value)) {status="Invalid event payload";return false;}
            var recipients=subscriptions.TryGetValue(name,out var listeners)?listeners.Where(run=>run.Machine.Wait!=null&&
                (run.Machine.Wait.Source==""||run.Machine.Wait.Source==source)).Select(run=>new Delivery {Run=run,Serial=run.WaitSerial}).ToArray():Array.Empty<Delivery>();
            if(recipients.Length==0) {status="Event had no waiting receivers";return true;}
            if(eventQueue.Count>=MaximumEvents) {if(EventsDropped<int.MaxValue)EventsDropped++;status=LastError="Event queue is full; the event was not delivered";return false;}
            eventQueue.Enqueue(new EventMessage {Receivers=recipients,Value=value,At=now,Depth=depth});status="Event queued for "+recipients.Length+" waiting program(s)";return true;
        }
        void WaitForEvent(Run run,float now)
        {
            lastNow=now;run.Targets.Clear();run.Claims=Array.Empty<BehaviourCatalog.Claim>();run.Active=null;run.WaitSerial++;run.Ends=now+run.Machine.Wait.Seconds;
            string name=run.Machine.Wait.Event;if(name==null)return;
            if(!subscriptions.TryGetValue(name,out var listeners))subscriptions[name]=listeners=new();
            listeners.Add(run);
        }
        void Unsubscribe(Run run)
        {
            var name=run.Machine?.Wait?.Event;if(name==null||!subscriptions.TryGetValue(name,out var listeners))return;
            listeners.Remove(run);if(listeners.Count==0)subscriptions.Remove(name);
        }
        void DispatchEvents(float now)
        {
            int count=Math.Min(16,eventQueue.Count);
            for(int i=0;i<count;i++) {
                var message=eventQueue.Dequeue();
                foreach(var receiver in message.Receivers) {
                    var run=receiver.Run;
                    if(!running.Contains(run)||run.WaitSerial!=receiver.Serial||run.Machine.Wait?.Event==null||
                        run.Machine.Wait.Seconds>0&&message.At>run.Ends)continue;
                    Unsubscribe(run);run.Machine.Resume(true,message.Value);run.EventDepth=message.Depth;run.Computing=true;
                }
            }
        }
    }
}
