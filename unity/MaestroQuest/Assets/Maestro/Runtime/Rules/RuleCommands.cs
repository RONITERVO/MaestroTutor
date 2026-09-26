// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    [Serializable] public sealed class RuleEdit
    {
        public string kind,reference,target;
        public RuleSequence sequence;
        public RuleBinding binding;
        public ButtonMount mount;
    }
    [Serializable] public sealed class RuleRequest
    {
        public string action,target;
        public int revision,page;
        public RuleEdit[] edits;
    }
    [Serializable] public sealed class RuleSummary { public string id,name; public int steps; public bool repeat; }
    [Serializable] public sealed class RuleRunView { public string id,sequenceId,stepId; public bool preparing; }
    [Serializable] public sealed class RuleView
    {
        public int revision,bindingPage,bindingCount,queued;
        public bool canUndo,canRedo,readOnly;
        public string status;
        public RuleSummary[] sequences;
        public RuleSequence selected;
        public RuleBinding[] bindings;
        public RuleButtonData[] buttons;
        public RuleRunView[] running;
    }
    public sealed partial class RuleWorkshop
    {
        int viewPage;
        public RuleView Observe(bool inspect=true)
        {
            var selected=inspect ? Selected : null;
            var bindings=selected==null ? Array.Empty<RuleBinding>() : document.bindings.Where(x=>x.sequenceId==selected.id).ToArray();
            int page=Mathf.Clamp(viewPage,0,Mathf.Max(0,(bindings.Length-1)/8));
            return new RuleView {
                revision=Revision,canUndo=CanUndo,canRedo=CanRedo,readOnly=ReadOnly,status=Status,
                sequences=document.sequences.Select(x=>new RuleSummary {id=x.id,name=x.name,steps=x.steps.Length,repeat=x.repeat}).ToArray(),
                selected=selected,bindings=bindings.Skip(page*8).Take(8).Select(x=>x.Copy()).ToArray(),bindingPage=page,bindingCount=bindings.Length,
                buttons=selected==null ? Array.Empty<RuleButtonData>() : document.buttons.Where(x=>x.sequenceId==selected.id).Select(x=>x.Copy()).ToArray(),
                running=Runtime?.Scheduler?.ObserveRuns() ?? Array.Empty<RuleRunView>(),queued=Runtime?.Scheduler?.QueuedCount ?? 0
            };
        }
        public bool Execute(RuleRequest request,out string error,out string[] created)
        {
            created=Array.Empty<string>(); error="Invalid behaviour request";
            if(request==null)return false;
            if(request.action=="inspect")
            {
                if(!string.IsNullOrEmpty(request.target)) {
                    int at=Array.FindIndex(document.sequences,x=>x.id==request.target);
                    if(at<0) {error="That behaviour no longer exists";return false;}
                    sequenceIndex=at;stepIndex=0;bindingIndex=-1;
                }
                viewPage=Mathf.Max(0,request.page);error="Behaviour inspected";Changed?.Invoke();return true;
            }
            if(request.action=="stop") {Runtime?.StopAll();Say("Behaviour playback stopped");error=Status;return true;}
            if(request.revision!=Revision) {error="Behaviours changed. Inspect the latest version before editing or playing.";return false;}
            if(request.action=="play") {
                if(!Runtime) {error="Behaviour playback is unavailable";return false;}
                bool started=Runtime.Trigger(request.target);error=Status;return started;
            }
            if(request.action=="undo" || request.action=="redo") {
                if(ReadOnly || Runtime && Runtime.AnyButtonHeld) {error="Release buttons and open editable rules before changing history";return false;}
                bool undoing=request.action=="undo";
                if(undoing ? !CanUndo : !CanRedo) {error="There is no behaviour edit to "+request.action;return false;}
                if(undoing)Undo();else Redo();error=Status;return true;
            }
            if(request.action!="edit" || request.edits==null || request.edits.Length<1 || request.edits.Length>16)return false;
            var candidate=document.Copy();var aliases=new Dictionary<string,string>();var added=new List<string>();string selectedId=Selected?.id;
            string Resolve(string id) => id!=null && aliases.TryGetValue(id,out var resolved) ? resolved : id;
            foreach(var change in request.edits)
            {
                if(change==null)return false;
                if(change.kind=="save")
                {
                    var input=change.sequence;
                    if(input==null || input.steps==null || input.steps.Any(x=>x==null))return false;
                    var value=input.Copy();bool isNew=string.IsNullOrEmpty(value.id);
                    var previous=candidate.sequences.FirstOrDefault(x=>x.id==value.id);
                    if(!isNew && previous==null) {error="That behaviour no longer exists";return false;}
                    if(isNew) {
                        if(!Creation.RoomRecipe.ValidId(change.reference) || aliases.ContainsKey(change.reference))return false;
                        value.id=Guid.NewGuid().ToString("N");aliases.Add(change.reference,value.id);added.Add(value.id);
                    }
                    foreach(var step in value.steps) {
                        if(isNew || string.IsNullOrEmpty(step.id))step.id=Guid.NewGuid().ToString("N");
                        else if(!previous.steps.Any(x=>x.id==step.id)) {error="An unknown step identity cannot replace an existing step";return false;}
                    }
                    candidate.sequences=isNew ? candidate.sequences.Append(value).ToArray() : candidate.sequences.Select(x=>x.id==value.id ? value : x).ToArray();
                    selectedId=value.id;
                }
                else if(change.kind=="delete") {
                    string id=Resolve(change.target);if(!candidate.sequences.Any(x=>x.id==id))return false;
                    candidate.sequences=candidate.sequences.Where(x=>x.id!=id).ToArray();candidate.bindings=candidate.bindings.Where(x=>x.sequenceId!=id).ToArray();candidate.buttons=candidate.buttons.Where(x=>x.sequenceId!=id).ToArray();
                }
                else if(change.kind=="bind") {
                    if(change.binding==null)return false;var value=change.binding.Copy();value.sequenceId=Resolve(value.sequenceId);
                    if(string.IsNullOrEmpty(value.id)) {value.id=Guid.NewGuid().ToString("N");candidate.bindings=candidate.bindings.Append(value).ToArray();}
                    else {if(!candidate.bindings.Any(x=>x.id==value.id && x.sequenceId==value.sequenceId))return false;candidate.bindings=candidate.bindings.Select(x=>x.id==value.id ? value : x).ToArray();}
                }
                else if(change.kind=="unbind") {if(!candidate.bindings.Any(x=>x.id==change.target))return false;candidate.bindings=candidate.bindings.Where(x=>x.id!=change.target).ToArray();}
                else if(change.kind=="button") {
                    var mount=change.mount;int slot=candidate.buttons.Count(x=>x.mount==mount);
                    candidate.buttons=candidate.buttons.Append(new RuleButtonData {id=Guid.NewGuid().ToString("N"),sequenceId=Resolve(change.target),mount=mount,
                        position=mount==ButtonMount.Room ? new Vector3(.3f+slot*.08f,1.25f,.7f) : new Vector3(mount==ButtonMount.LeftController ? -.12f : .12f,.06f+(slot%2)*.08f,.05f+(slot/2)*.08f)}).ToArray();
                }
                else if(change.kind=="unbutton") {if(!candidate.buttons.Any(x=>x.id==change.target))return false;candidate.buttons=candidate.buttons.Where(x=>x.id!=change.target).ToArray();}
                else return false;
            }
            if(!candidate.Validate(out error))return false;
            if(!Edit(value=>{value.sequences=candidate.sequences;value.bindings=candidate.bindings;value.buttons=candidate.buttons;},"Behaviour edit applied. Undo restores this batch.")) {error=Status;return false;}
            sequenceIndex=Array.FindIndex(document.sequences,x=>x.id==selectedId);if(sequenceIndex<0&&document.sequences.Length>0)sequenceIndex=0;
            stepIndex=0;viewPage=0;created=added.Where(id=>document.sequences.Any(x=>x.id==id)).ToArray();error=Status;Changed?.Invoke();return true;
        }
    }
}
