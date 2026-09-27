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
        public bool AnyButtonHeld => buttons.Values.Any(x => x && x.IsHeld);
        public void Initialize(RuleWorkshop source, RoomEditor roomEditor, AnimationWorkshop animationWorkshop, NativeBookBrowser book, RoomInteraction interaction, BookControllerInput controllerInput, Func<int,Transform> controllerAnchors = null)
        {
            workshop = source; editor = roomEditor; animations = animationWorkshop; browser = book; room = interaction; input = controllerInput;
            anchors = controllerAnchors ?? (index => input ? input.ControllerAnchor(index) : null);
            actions = new RoomRuleActions(editor,animations); Scheduler = new RuleScheduler(actions,new InvocationReceipts(editor.SaveDirectory)); workshop.Runtime = this;
            workshop.DocumentChanged += Reload;
            editor.Editing += StopAll; editor.ItemGrabbed += Grabbed; editor.ItemReleased += Released; editor.ItemTapped += Tapped;
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
            Scheduler.StopTarget(id,true); Scheduler.Emit(RuleEventKind.ItemGrabbed,id,Time.unscaledTime); ShowError();
        }
        void Released(string id) { Scheduler.Emit(RuleEventKind.ItemReleased,id,Time.unscaledTime); ShowError(); }
        void Tapped(string id) { Scheduler.Emit(RuleEventKind.ItemTapped,id,Time.unscaledTime); ShowError(); }
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
        public bool CanRun(Maestro.Quest.Programs.CapabilityCall step,out string error)
        {
            error="Action runtime is not ready";if(actions==null||Scheduler==null)return false;
            if(paused||!focused||!isActiveAndEnabled) {error="Actions are paused";return false;}
            return actions.CanRun(step,out error);
        }
        public void StopAll() => Scheduler?.StopAll();
        public void ObserveSnapshot(BookSnapshot snapshot)
        {
            if (Scheduler == null || paused || !focused) return;
            // A paused/recreated browser has no reliable activity. Clear its baseline
            // without manufacturing an Idle event or stopping manual room actions.
            if (snapshot == null || snapshot.audioPaused) Scheduler.ForgetActivity();
            else Scheduler.SetActivity(snapshot.activity,Time.unscaledTime);
        }
        void Update()
        {
            if (Scheduler == null || paused || !focused) return;
            if (browser) ObserveSnapshot(browser.Snapshot);
            Scheduler.Tick(Time.unscaledTime); actions.Tick();
            if (Scheduler.LastError != shownError) { shownError = Scheduler.LastError; if (shownError != null) workshop.Say(shownError); }
        }
        void OnApplicationPause(bool value) { paused = value; Scheduler?.Suspend(paused || !focused); }
        void OnApplicationFocus(bool value) { focused = value; Scheduler?.Suspend(paused || !focused); }
        void OnDisable() => StopAll();
        void OnDestroy()
        {
            StopAll();
            if (workshop) workshop.DocumentChanged -= Reload;
            if (editor) { editor.Editing -= StopAll; editor.ItemGrabbed -= Grabbed; editor.ItemReleased -= Released; editor.ItemTapped -= Tapped; }
            if (animations) animations.Starting -= Authoring;
            if (room) { room.Restoring -= StopAll; room.Restored -= RecoverButtons; }
        }
    }
}
