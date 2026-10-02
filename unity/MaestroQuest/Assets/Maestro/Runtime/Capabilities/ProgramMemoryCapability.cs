// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ProgramMemoryCapability:CapabilityModule
    {
        public override string Id=>"program.memory.edit";
        public override string Label=>"Edit remembered values";
        public override string Description=>"Set one typed remembered variable or reset one/all cells of an exact saved behaviour. First inspect rules with action memory and target program ID; stop that behaviour and wait for accepted writes to drain. Pass fresh memory and rules revisions and the observed memory sessionId. variableId is empty only for reset-all. valueJson is one strict JSON value matching its saved/declaration type. Reset keeps definitions and never starts a run. Removed declarations/behaviours retain bounded values until explicitly reset. In temporary rooms edits and checkpoints affect only temporary memory. Keep captures room and memory together; Discard restores their last confirmed saved pair. Room Undo never rewinds memory. No Undo. A dispatched write can finish after Stop; inspect memory and its receipt before continuing after uncertainty.";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Requirements=>new[]{"behaviour.stopped","programMemory.ready","storage.writable","room.session.current"};
        public override JObject InputSchema {get{
            JObject Branch(string kind){var fields=new JObject { ["kind"]=Choice(kind),["sessionId"]=Text("^[a-f0-9]{32}$",32),["programId"]=Text("^[a-fA-F0-9]{32}$",32),["variableId"]=Text(kind=="set"?"^[a-f0-9]{32}$":"^(|[a-f0-9]{32})$",32),["revision"]=Text("^(initial|[a-f0-9]{32})$",32),["rulesRevision"]=Number(1,1000000,true)};
                if(kind=="set")fields["valueJson"]=new JObject { ["type"]="string",["maxLength"]=8192,["format"]="programMemoryValue",["x-static"]=true };return Object(fields);}
            return new JObject {["type"]="object",["title"]="Memory edit",["x-discriminators"]=new JArray("kind"),["oneOf"]=new JArray(Branch("set"),Branch("reset")),["x-features"]=new JArray("rememberedVariables.v1","temporaryMemory.v1")};
        }}
        public override JObject OutputSchema=>Object(new JObject {["revision"]=Text("^(initial|[a-f0-9]{32})$",32),["changed"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["kind"]="reset",["sessionId"]=new string('0',32),["programId"]=new string('0',32),["variableId"]="",["revision"]="initial",["rulesRevision"]=1};
        static bool Prepare(CapabilityContext context,JObject args,out ProgramMemoryStore store,out Dictionary<string,ProgramMemoryDocument.Cell> values,out string error)
        {
            store=null;values=null;error=null;var workshop=context.Editor?context.Editor.GetComponent<RuleWorkshop>():null;
            if(!workshop||workshop.Memory==null){error="Program memory is unavailable";return false;}
            store=workshop.Memory;
            try {
                if((string)args["sessionId"]!=context.Editor.TemporarySessionId)throw new ProgramFault("The room memory session changed; inspect remembered values before editing");
                if(workshop.MemoryBlocked!=null)throw new ProgramFault(workshop.MemoryBlocked);
                if(context.Editor.WriteGate.Frozen)throw new ProgramFault("Workspace storage is held; wait before editing memory");
                if(store.Pending)throw new ProgramFault("Wait for the accepted memory write to finish");
                if(workshop.Revision!=(int)args["rulesRevision"])throw new ProgramFault("Behaviours changed; inspect their remembered declarations again");
                string program=(string)args["programId"],id=(string)args["variableId"];
                if(workshop.Runtime?.Scheduler?.MemoryTargetBusy(program)==true)throw new ProgramFault("Stop this behaviour and its queued starts before editing remembered values");
                var saved=store.Snapshot();if(saved.Revision!=(string)args["revision"])throw new ProgramFault("Remembered values changed; inspect them before editing");
                if((string)args["kind"]=="reset")return true;
                var compiled=workshop.Snapshot().sequences.FirstOrDefault(x=>x.id==program)?.Compile(out _);
                string name=compiled?.Remembered.FirstOrDefault(x=>x.Value==id).Key;ProgramValue initial=default;
                if(name!=null)initial=compiled.InitialState[name];
                else if(saved.Programs.TryGetValue(program,out var group)&&group.TryGetValue(id,out var cell)){name=cell.Name;initial=cell.Value;}
                else throw new ProgramFault("Inspect an existing remembered declaration or retained value");
                string json=(string)args["valueJson"];if(string.IsNullOrEmpty(json)||json.Length>8192)throw new ProgramFault("Invalid remembered value JSON");
                using var reader=new JsonTextReader(new StringReader(json)){MaxDepth=8,DateParseHandling=DateParseHandling.None};
                var token=JToken.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())throw new ProgramFault("Extra remembered value JSON");
                values=new(){[id]=new ProgramMemoryDocument.Cell(name,ProgramValue.Literal(token,initial.Type))};
                if(saved.Programs.TryGetValue(program,out var stored)&&stored.TryGetValue(id,out var old)&&old.Value.Type!=initial.Type)throw new ProgramFault("Saved type differs; reset this variable or use a new identity");
                return true;
            }catch(Exception ex){error=ex.Message;return false;}
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Prepare(context,args,out _,out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!Prepare(context,args,out var store,out var values,out error))return false;
            try {string id=(string)args["variableId"];var task=(string)args["kind"]=="set"?store.Write((string)args["revision"],(string)args["programId"],values):store.Reset((string)args["revision"],(string)args["programId"],id==""?null:id);operation=new MemoryWrite(task);return true;}
            catch(Exception ex){error=ex.Message;return false;}
        }
        sealed class MemoryWrite:CapabilityOperation
        {
            readonly Task<ProgramMemoryStore.Result> task;internal MemoryWrite(Task<ProgramMemoryStore.Result> task){this.task=task;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){error=null;if(!task.IsCompleted)return RuleActionState.Preparing;try{error=task.GetAwaiter().GetResult().Error;}catch(Exception ex){error=ex.Message;}return error==null?RuleActionState.Ready:RuleActionState.Failed;}
            public override JObject Result {get{var result=task.GetAwaiter().GetResult();return new(){["revision"]=result.Revision,["changed"]=result.Changed};}}
            public override string InterruptionStatus=>"Stopped waiting. An accepted memory edit may still finish; inspect remembered values and the action receipt before continuing.";
        }
    }
}
