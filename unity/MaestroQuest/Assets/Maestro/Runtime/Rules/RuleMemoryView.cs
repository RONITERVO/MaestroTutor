// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Rules
{
    [Serializable] public sealed class ProgramMemoryCellView {public string id,name,typeJson,valueJson;public bool saved,declared;}
    [Serializable] public sealed class ProgramMemoryGroupView {public string id,name;public int cells;}
    [Serializable] public sealed class ProgramMemoryView {
        public bool ready,pending,busy,temporary;public string error,revision,programId,sessionId;public int page,count;
        public ProgramMemoryGroupView[] programs=Array.Empty<ProgramMemoryGroupView>();
        public ProgramMemoryCellView[] cells=Array.Empty<ProgramMemoryCellView>();
    }
    public sealed partial class RuleWorkshop
    {
        string memoryTarget,memoryCacheKey;int memoryPage;
        ProgramMemoryCellView[] memoryCells=Array.Empty<ProgramMemoryCellView>();
        ProgramMemoryView ObserveMemory()
        {
            string target=memoryTarget??Selected?.id??"";
            var view=new ProgramMemoryView {sessionId=editor.TemporarySessionId,temporary=Memory?.Temporary==true,ready=Memory?.Ready==true,pending=Memory?.Pending==true,error=Memory?.Error??MemoryBlocked??"",revision="",programId=target,busy=Runtime?.Scheduler?.MemoryTargetBusy(target)==true};
            if(!view.ready||Memory.Error!=null)return view;
            var saved=Memory.Snapshot();view.revision=saved.Revision;
            view.programs=saved.Programs.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new ProgramMemoryGroupView {id=x.Key,name=document.sequences.FirstOrDefault(s=>s.id==x.Key)?.name??"Removed behaviour",cells=x.Value.Count}).ToArray();
            string key=saved.Revision+":"+Revision+":"+target;
            if(key!=memoryCacheKey){
                var cells=new Dictionary<string,ProgramMemoryCellView>();
                ProgramMemoryCellView Cell(string id,string name,ProgramValue value,bool stored,bool declared)=>new() {id=id,name=name,typeJson=ProgramMemoryDocument.TypeJson(value.Type).ToString(Formatting.None),valueJson=JToken.FromObject(value.Value).ToString(Formatting.None),saved=stored,declared=declared};
                if(saved.Programs.TryGetValue(target,out var group))foreach(var pair in group)cells.Add(pair.Key,Cell(pair.Key,pair.Value.Name,pair.Value.Value,true,false));
                var program=document.sequences.FirstOrDefault(x=>x.id==target)?.Compile(out _);
                if(program!=null)foreach(var pair in program.Remembered){
                    if(cells.TryGetValue(pair.Value,out var existing)){existing.declared=true;existing.name=pair.Key;}
                    else cells.Add(pair.Value,Cell(pair.Value,pair.Key,program.InitialState[pair.Key],false,true));
                }
                memoryCells=cells.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();memoryCacheKey=key;
            }
            view.count=memoryCells.Length;view.page=Math.Max(0,Math.Min(memoryPage,Math.Max(0,(view.count-1)/4)));view.cells=memoryCells.Skip(view.page*4).Take(4).ToArray();return view;
        }
    }
}
