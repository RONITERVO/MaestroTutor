// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    [DefaultExecutionOrder(50)]
    public sealed class RoomRules : MonoBehaviour
    {
        RuleWorkshop workshop;
        RoomEditor editor;
        RoomRuntimeGate runtimeGate;
        AnimationWorkshop animations;
        NativeBookBrowser browser;
        RoomInteraction room;
        BookControllerInput input;
        Func<int,Transform> anchors;
        RoomRuleActions actions;
        readonly Dictionary<string,RuleButton> buttons = new();
        public RuleScheduler Scheduler { get; private set; }
        bool paused, focused = true;
        string shownError;
        internal IEnumerable<Renderer> ViewButtonRenderers=>buttons.Values.Where(button=>button).SelectMany(button=>button.GetComponentsInChildren<Renderer>()).Where(renderer=>renderer&&renderer.enabled&&renderer.gameObject.activeInHierarchy);
        public bool AnyButtonHeld => buttons.Values.Any(x => x && x.IsHeld);
        public void Initialize(RuleWorkshop source, RoomEditor roomEditor, AnimationWorkshop animationWorkshop, NativeBookBrowser book, RoomInteraction interaction, BookControllerInput controllerInput, Func<int,Transform> controllerAnchors = null, Programs.IProgramClock clock = null)
        {
            workshop = source; editor = roomEditor; animations = animationWorkshop; browser = book; room = interaction; input = controllerInput;
            anchors = controllerAnchors ?? (index => input ? input.ControllerAnchor(index) : null);
            actions = new RoomRuleActions(editor,animations,clock); Scheduler = new RuleScheduler(actions,new InvocationReceipts(editor.ReceiptDirectory)); workshop.Runtime = this;Scheduler.ConfigureMemory(workshop.Memory,()=>workshop.MemoryBlocked);
            runtimeGate=editor.RuntimeGate;runtimeGate.Changed+=RefreshSuspension;RefreshSuspension();
            workshop.DocumentChanged += Reload;
            editor.Editing += StopAll; editor.ItemGrabbed += Grabbed; editor.ItemReleased += Released; editor.ItemTapped += Tapped; editor.ItemCollided += Collided;editor.ItemCaught+=Caught;editor.ConnectionBroken+=ConnectionBroke;editor.ContainerPoured+=ContainerPoured;editor.ContainerScooped+=ContainerScooped;
            animations.Starting += Authoring; room.Restoring += StopAll; room.Restored += RecoverButtons;
            Reload();
        }
        void Reload()
        {
            var document = workshop.Snapshot(); Scheduler.Configure(document);
            var ids = document.buttons.Select(x => x.id).ToHashSet();
            foreach (var id in buttons.Keys.Where(x => !ids.Contains(x)).ToArray()) { if (buttons[id]) { buttons[id].gameObject.SetActive(false); Destroy(buttons[id].gameObject); } buttons.Remove(id); }
            foreach (var data in document.buttons)
            {
                if (!buttons.TryGetValue(data.id,out var button) || !button)
                {
                    var root = new GameObject("User action button"); root.transform.SetParent(transform,false);
                    button = root.AddComponent<RuleButton>(); button.Build(this,workshop,anchors); buttons[data.id] = button;
                }
                button.Configure(data,document.sequences.First(x => x.id == data.sequenceId).name);
            }
        }
        void Grabbed(RoomItem item)
        {
            var id = editor.Identity(item); if (id == null) return;
            Scheduler.GrabTarget(id); Scheduler.Emit(RuleEventKind.ItemGrabbed,id,Time.unscaledTime); ShowError();
        }
        void Released(string id) { Scheduler.Emit(RuleEventKind.ItemReleased,id,Time.unscaledTime); ShowError(); }
        void Tapped(string id) { Scheduler.Emit(RuleEventKind.ItemTapped,id,Time.unscaledTime); ShowError(); }
        void Collided(string id,string otherId,string kind,Vector3 point,float speed) {
            if(editor.RuntimeGate.Held||paused||!focused||!isActiveAndEnabled||Scheduler==null||!Scheduler.IsListening("object.collided",id))return;
            var frame=editor.Frame;if(!frame.Valid)return;point=frame.PointToRoom(point);
            var fields=new Newtonsoft.Json.Linq.JObject {["otherId"]=otherId,["otherKind"]=kind,["speed"]=speed,["x"]=point.x,["y"]=point.y,["z"]=point.z};
            Scheduler.EmitNative("object.collided",id,new Programs.ProgramValue(id),fields,Time.unscaledTime,out _);
        }
        void Caught(string id,string holder,string part,Vector3 point,float speed){
            if(editor.RuntimeGate.Held||paused||!focused||!isActiveAndEnabled||Scheduler==null||!Scheduler.IsListening("object.caught",id))return;
            var frame=editor.Frame;if(!frame.Valid)return;point=frame.PointToRoom(point);
            Scheduler.EmitNative("object.caught",id,new Programs.ProgramValue(id),new Newtonsoft.Json.Linq.JObject{["holder"]=holder,["part"]=part,["speed"]=speed,["x"]=point.x,["y"]=point.y,["z"]=point.z},Time.unscaledTime,out _);
        }
        void ConnectionBroke(string id,string connected,string kind,float force,float torque){
            if(editor.RuntimeGate.Held||paused||!focused||!isActiveAndEnabled||Scheduler==null||!Scheduler.IsListening("object.connection.broken",id))return;
            Scheduler.EmitNative("object.connection.broken",id,new Programs.ProgramValue(id),new Newtonsoft.Json.Linq.JObject {["connected"]=connected,["kind"]=kind,["forceLimit"]=force,["torqueLimit"]=torque},Time.unscaledTime,out _);
        }
        void ContainerPoured(string id,double received,double spilled,int receivers,string liquid){
            if(editor.RuntimeGate.Held||paused||!focused||!isActiveAndEnabled||Scheduler==null||!Scheduler.IsListening("object.container.poured",id))return;
            Scheduler.EmitNative("object.container.poured",id,new Programs.ProgramValue(id),new Newtonsoft.Json.Linq.JObject{["transferredMl"]=received,["spilledMl"]=spilled,["receivers"]=receivers,["liquid"]=liquid,["temporary"]=editor.TemporaryRoom},Time.unscaledTime,out _);
        }
        void ContainerScooped(string id,double amount,int donors,string liquid){
            if(editor.RuntimeGate.Held||paused||!focused||!isActiveAndEnabled||Scheduler==null||!Scheduler.IsListening("object.container.scooped",id))return;
            Scheduler.EmitNative("object.container.scooped",id,new Programs.ProgramValue(id),new Newtonsoft.Json.Linq.JObject{["scoopedMl"]=amount,["donors"]=donors,["liquid"]=liquid,["temporary"]=editor.TemporaryRoom},Time.unscaledTime,out _);
        }
        void Authoring(string id) => Scheduler.StopTarget(id,false);
        void RecoverButtons() => workshop.RecoverButtons();
        void ShowError() { if (Scheduler.LastError != null) workshop.Say(Scheduler.LastError); }
        public bool Trigger(string sequenceId)
        {
            bool accepted = Scheduler.Trigger(sequenceId,Time.unscaledTime);
            workshop.Say(accepted ? Scheduler.PreparingCount > 0 ? "Loading action motion…" : "Action triggered" : Scheduler.LastError ?? "Action could not start"); return accepted;
        }
        public void TrySelected()
        {
            if (workshop.Selected == null) return;
            var issue=workshop.Snapshot().ProgramError(workshop.Selected);
            if(issue!=null){workshop.Say(issue);return;}
            animations.Stop(); Trigger(workshop.Selected.id);
        }
        public bool TryReadFact(string name,out Programs.ProgramValue value) {
            value=default;return !paused&&focused&&isActiveAndEnabled&&Scheduler!=null&&Scheduler.TryRead(name,out value);
        }
        public bool TryReadFact(string name,int version,Newtonsoft.Json.Linq.JObject arguments,out Programs.ProgramValue value) {
            value=default;return !paused&&focused&&isActiveAndEnabled&&Scheduler!=null&&Scheduler.TryRead(name,version,arguments,out value);
        }
        public bool CanRun(Maestro.Quest.Programs.CapabilityCall step,out string error)
        {
            error="Action runtime is not ready";if(actions==null||Scheduler==null)return false;
            if(editor.RuntimeGate.Held){error=editor.RuntimeGate.Reason;return false;}
            if(paused||!focused||!isActiveAndEnabled) {error="Actions are paused";return false;}
            return actions.CanRun(step,out error);
        }
        public void StopAll() => Scheduler?.StopAll();
        public void ObserveSnapshot(BookSnapshot snapshot)
        {
            if (Scheduler == null || paused || !focused || editor.RuntimeGate.Held) return;
            // A paused/recreated browser has no reliable activity. Clear its baseline
            // without manufacturing an Idle event or stopping manual room actions.
            if (snapshot == null || snapshot.audioPaused) Scheduler.ForgetActivity();
            else Scheduler.SetActivity(snapshot.activity,Time.unscaledTime);
        }
        void Update()
        {
            if (Scheduler == null || paused || !focused || editor.RuntimeGate.Held) return;
            if (browser) ObserveSnapshot(browser.Snapshot);
            Scheduler.Tick(Time.unscaledTime); actions.Tick();
            if (Scheduler.LastError != shownError) { shownError = Scheduler.LastError; if (shownError != null) workshop.Say(shownError); }
        }
        void RefreshSuspension()=>Scheduler?.Suspend(paused||!focused||editor.RuntimeGate.Held);
        void OnApplicationPause(bool value) { paused = value; RefreshSuspension(); }
        void OnApplicationFocus(bool value) { focused = value; RefreshSuspension(); }
        void OnDisable() => StopAll();
        void OnDestroy()
        {
            StopAll();
            if (workshop) workshop.DocumentChanged -= Reload;
            if(runtimeGate!=null)runtimeGate.Changed-=RefreshSuspension;
            if (editor) { editor.Editing -= StopAll; editor.ItemGrabbed -= Grabbed; editor.ItemReleased -= Released; editor.ItemTapped -= Tapped; editor.ItemCollided -= Collided;editor.ItemCaught-=Caught;editor.ConnectionBroken-=ConnectionBroke;editor.ContainerPoured-=ContainerPoured;editor.ContainerScooped-=ContainerScooped; }
            if (animations) animations.Starting -= Authoring;
            if (room) { room.Restoring -= StopAll; room.Restored -= RecoverButtons; }
        }
    }
}
