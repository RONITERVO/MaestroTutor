// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleScheduler
    {
        ProgramMemoryStore memory;
        Func<string> memoryBlocked;
        readonly List<Run> checkpoints=new();
        Task<ProgramMemoryStore.Result> dispatchedCheckpoint;
        float nextCheckpoint;
        internal void ConfigureMemory(ProgramMemoryStore store,Func<string> blocked=null){StopAll();memory=store;memoryBlocked=blocked;}
        internal bool MemoryTargetBusy(string id)=>running.Any(x=>x.Sequence.id==id)||queued.Any(x=>x.SequenceId==id);
        ProgramMachine CreateMachine(RuleSequence sequence,out string identity)
        {
            var program=sequence.Compile(out _);identity=null;
            if(program.Remembered.Count==0)return new ProgramMachine(program,this);
            string blocked=memoryBlocked?.Invoke();if(blocked!=null)throw new ProgramFault(blocked);
            if(memory==null)throw new ProgramFault("Program memory is unavailable");
            if(memory.Pending)throw new ProgramFault("Wait for the accepted memory save before starting this behaviour");
            var saved=memory.Snapshot();identity=saved.GroupIdentity(sequence.id);
            var values=new Dictionary<string,ProgramValue>();
            foreach(var pair in program.Remembered)if(saved.TryRead(sequence.id,pair.Value,program.InitialState[pair.Key].Type,out var value))values.Add(pair.Key,value);
            return new ProgramMachine(program,this,values);
        }
        bool QueueCheckpoint(Run run)
        {
            if(run.Parent!=null||memory==null){LastError="Only a workspace behaviour can save remembered values";Stop(run,false,"failed",LastError);return false;}
            if(!checkpoints.Contains(run))checkpoints.Add(run);
            return true;
        }
        // FIFO admission bounds writes globally, including no-op checkpoints. An IO
        // completion is never a timer/event wake and does not renew instruction work.
        void PumpCheckpoints(float now)
        {
            if(dispatchedCheckpoint!=null){if(!dispatchedCheckpoint.IsCompleted)return;dispatchedCheckpoint=null;}
            checkpoints.RemoveAll(x=>!running.Contains(x));
            if(checkpoints.Count==0||now<nextCheckpoint||memory?.Pending==true)return;
            var run=checkpoints[0];checkpoints.RemoveAt(0);nextCheckpoint=now+1;
            try {
                string blocked=memoryBlocked?.Invoke();if(blocked!=null)throw new ProgramFault(blocked);
                var saved=memory.Snapshot();
                if(saved.GroupIdentity(run.Sequence.id)!=run.MemoryIdentity)throw new ProgramFault("Remembered values changed during this run; inspect and start again");
                dispatchedCheckpoint=run.MemoryWrite=memory.Write(saved.Revision,run.Sequence.id,run.Machine.RememberedValues());
            }catch(Exception e){LastError=e.Message;Stop(run,false,"failed",LastError);}
        }
        void PollCheckpoint(Run run)
        {
            if(run.MemoryWrite==null||!run.MemoryWrite.IsCompleted)return;
            try {
                var result=run.MemoryWrite.GetAwaiter().GetResult();run.MemoryWrite=null;
                if(result.Error!=null)throw new ProgramFault(result.Error);
                run.MemoryIdentity=memory.Snapshot().GroupIdentity(run.Sequence.id);
                run.Machine.CompleteCheckpoint();run.Computing=true;
            }catch(Exception e){LastError=e.Message;Stop(run,false,"failed",LastError);}
        }
    }
}
