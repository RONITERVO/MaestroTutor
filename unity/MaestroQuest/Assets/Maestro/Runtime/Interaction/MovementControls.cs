// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    [DefaultExecutionOrder(-150)]
    public sealed partial class MovementControls : MonoBehaviour
    {
        const string Owner="controller movement";
        RoomInteraction room;
        RoomEditor editor;
        RoomRuntimeGate runtimeGate;
        AnimationWorkshop animations;
        AvatarSpatialMotion avatar;
        RoomRules rules;
        RuleWorkshop workshop;
        BookControllerInput input;
        VirtualRoomView view;
        Func<bool> headTracked;
        Func<ControllerFrame> sample;
        ControllerPreferenceStorage storage;
        ControllerPreferences preferences;
        readonly NeutralMovementGate userGate=new(), avatarGate=new();
        readonly bool[] buttonReady=new bool[4];
        bool driving,paused,focused=true;
        public bool AvatarEnabled { get; private set; }
        public bool UserEnabled { get; private set; }
        public bool Virtual => view && view.Active;
        public string Status { get; private set; }="Movement off — choose controls, then enable";
        public event Action Changed;
        public ControllerPreferences Preferences => preferences.Copy();
        internal RoomEditor ArchiveEditor => editor;
        internal bool ArchiveReady => storage!=null&&!storage.ReadOnly&&preferences!=null;
        public void Initialize(RoomInteraction interaction,RoomEditor source,AnimationWorkshop authoring,AvatarSpatialMotion motion,RoomRules behaviours,RuleWorkshop ruleEditor,BookControllerInput controller,VirtualRoomView presentation,Func<bool> tracked,Func<ControllerFrame> frames=null,string directory=null)
        {
            room=interaction; editor=source; animations=authoring; avatar=motion; rules=behaviours; workshop=ruleEditor; input=controller; view=presentation; headTracked=tracked;
            sample=frames ?? (() => input ? input.ReadMovement() : default);
            storage=new ControllerPreferenceStorage(directory ?? source.SaveDirectory); preferences=storage.Load(out var message);
            runtimeGate=editor.RuntimeGate;runtimeGate.Changed+=RuntimeChanged;RuntimeChanged();
            room.Restoring+=Recover; editor.Editing+=Interrupt; animations.Starting+=Authoring; if (workshop) workshop.Changed+=RulesChanged;
            if (message != null) Say(message);
        }
        public bool Apply(ControllerPreferences next)
        {
            if(TryApplyPreferences(next,out var error))return true;Say(error);return false;
        }
        public void CycleStick(bool user)
        {
            var next=Preferences; int at=(int)(user ? next.userStick : next.avatarStick);
            MovementStick other=user ? next.avatarStick : next.userStick, choice;
            do { at=at == 1 ? -1 : at+1; choice=(MovementStick)at; } while (choice != MovementStick.None && choice == other);
            if (user) next.userStick=choice; else next.avatarStick=choice; Apply(next);
        }
        public void SwapSticks() { var next=Preferences; (next.userStick,next.avatarStick)=(next.avatarStick,next.userStick); Apply(next); }
        public void ToggleAvatar()
        {
            if(!RuntimeReady())return;
            if (!AvatarEnabled && preferences.avatarStick == MovementStick.None) { Say("Choose a Maestro binding first"); return; }
            Interrupt(); AvatarEnabled=!AvatarEnabled;
            if (AvatarEnabled) { animations.Stop(); rules?.Scheduler.StopConflicting(new RuleStep {action=RuleActionKind.FollowUser,targetId="maestro"},true); avatar.Stop(); }
            Say(AvatarEnabled ? "Maestro stick on — center the stick, then move" : "Maestro stick off");
        }
        public void ToggleUser()
        {
            if(!RuntimeReady())return;
            if (!Virtual) { Say("Choose Virtual view before enabling your own movement"); return; }
            if (!UserEnabled && preferences.userStick == MovementStick.None) { Say("Choose your movement binding first"); return; }
            Interrupt(); UserEnabled=!UserEnabled; Say(UserEnabled ? "Your movement on — center the stick, then move" : "Your movement off");
        }
        public void ToggleView()
        {
            if(!RuntimeReady())return;
            if (Virtual) { Recover(); Say("Mixed reality restored — check scan alignment before Start physics"); return; }
            // Physical actions run on release, before BookControllerInput clears PageHeld.
            // That finishing click is allowed; a held grab or drawing is not.
            if (paused || !focused || !headTracked() || sample().manipulating || editor.AnyHeld || rules && rules.AnyButtonHeld)
            { Say("Release held items and wait for tracking before changing view"); return; }
            Interrupt(); animations.Stop(); rules?.StopAll(); avatar.Stop(); input?.CancelAll();
            if (!view.Enter()) { Say("Wait for the room scan to finish before changing view"); return; }
            Say("Virtual view — real room hidden; enable Your movement to walk");
        }
        public void BindButton(int index,ControllerCommand command,string sequenceId=null)
        {
            if (index < 0 || index >= 4) return;
            var next=Preferences; next.buttons[index]=new ControllerBinding { command=command,sequenceId=sequenceId }; Apply(next);
        }
        public void BindSelected(int index)
        {
            if (workshop?.Selected == null) { Say("Choose an action on the rules tray first"); return; }
            BindButton(index,ControllerCommand.Sequence,workshop.Selected.id);
        }
        public string ButtonLabel(int index)
        {
            var binding=preferences.buttons[index];
            if (binding.command != ControllerCommand.Sequence) return binding.command == ControllerCommand.SnapLeft ? "Turn left 30°" : binding.command == ControllerCommand.SnapRight ? "Turn right 30°" : "None";
            var action=workshop?.Snapshot().sequences; var found=action == null ? null : Array.Find(action,x => x.id == binding.sequenceId);
            return found == null ? "Missing action" : found.name;
        }
        bool RuntimeReady(){if(!editor.RuntimeGate.Held)return true;Recover();Say(editor.RuntimeGate.Reason);return false;}
        void RuntimeChanged(){if(editor.RuntimeGate.Held){Recover();Say(editor.RuntimeGate.Reason);}}
        void RulesChanged() => Changed?.Invoke();
        void Authoring(string _) => Interrupt();
        public void Interrupt()
        {
            userGate.Reset(); avatarGate.Reset(); Array.Clear(buttonReady,0,buttonReady.Length);
            avatar?.End(Owner); driving=false;
        }
        public void Recover()
        {
            bool changed=AvatarEnabled || UserEnabled || Virtual;
            Interrupt(); AvatarEnabled=UserEnabled=false; if (changed) { input?.CancelAll(); view?.Exit(); Status="Movement off — choose controls to enable again"; Changed?.Invoke(); }
        }
        void Update() => Tick(Mathf.Min(Time.deltaTime,.05f));
        public void Tick(float deltaTime)
        {
            if (preferences == null) return;
            if (!RuntimeReady()||paused || !focused || !headTracked()) { Recover(); return; }
            var frame=sample();
            if (frame.busy || editor.AnyHeld || rules && rules.AnyButtonHeld) { Interrupt(); return; }
            if (driving && !avatar.OwnedBy(Owner)) { driving=false; avatarGate.Reset(); }
            var avatarAxis=avatarGate.Read(frame.Axis(preferences.avatarStick),AvatarEnabled && frame.Tracked(preferences.avatarStick),preferences.deadZone);
            if (avatarAxis.sqrMagnitude > 0)
            {
                if (!driving)
                {
                    driving=avatar.Begin(Owner,AvatarSpatialMode.Manual,out var error);
                    if (!driving) { avatarGate.Reset(); Say(error); }
                }
                if (driving) avatar.ManualDirection(Owner,Direction(avatarAxis));
            }
            else if (driving) { avatar.End(Owner); driving=false; }
            var userAxis=userGate.Read(frame.Axis(preferences.userStick),UserEnabled && Virtual && frame.Tracked(preferences.userStick),preferences.deadZone);
            if (userAxis.sqrMagnitude > 0 && float.IsFinite(deltaTime) && deltaTime > 0)
            {
                if (!view.Move(Direction(userAxis)*preferences.userSpeed*Mathf.Min(deltaTime,.05f))) Say("Your path is blocked or at the virtual floor edge");
                else Say("Virtual walking — B/Y or palm Recall returns to your real room");
            }
            for (int i=0;i<4;i++)
            {
                bool tracked=i == 0 || i == 2 ? frame.leftTracked : frame.rightTracked;
                if (!tracked) { buttonReady[i]=false; continue; }
                if (!frame.Button(i)) { buttonReady[i]=true; continue; }
                if (!buttonReady[i]) continue; buttonReady[i]=false;
                var binding=preferences.buttons[i];
                if (binding.command == ControllerCommand.Sequence)
                {
                    // User-authored button actions use exactly the same scheduler as mounted buttons.
                    if (rules && rules.Trigger(binding.sequenceId)) Say("Controller action triggered"); else Say("Action unavailable — assign an existing action");
                }
                else if (Virtual && UserEnabled && binding.command != ControllerCommand.None) view.Turn(binding.command == ControllerCommand.SnapLeft ? -30 : 30);
            }
        }
        Vector3 Direction(Vector2 axis)
        {
            var forward=Vector3.ProjectOnPlane(room.Viewer.forward,Vector3.up);
            if (forward.sqrMagnitude < .01f) forward=Vector3.ProjectOnPlane(room.Viewer.up,Vector3.up);
            forward.Normalize(); return forward*axis.y+Vector3.Cross(Vector3.up,forward)*axis.x;
        }
        void Say(string message) { if (Status == message) return; Status=message; Changed?.Invoke(); }
        void OnApplicationPause(bool value) { paused=value; if (value) Recover(); }
        void OnApplicationFocus(bool value) { focused=value; if (!value) Recover(); }
        void OnDisable() => Recover();
        void OnDestroy()
        {
            Recover(); if (room) room.Restoring-=Recover; if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged; if (editor) editor.Editing-=Interrupt; if (animations) animations.Starting-=Authoring; if (workshop) workshop.Changed-=RulesChanged;
        }
    }
}
