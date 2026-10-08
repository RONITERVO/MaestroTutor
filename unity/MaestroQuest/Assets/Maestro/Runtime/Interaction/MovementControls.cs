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
            editor.Editing+=Interrupt; animations.Starting+=Authoring; if (workshop) workshop.Changed+=RulesChanged;
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
        public void ToggleAvatar()=>ManualMode(AvatarEnabled?"maestro.disable":"maestro.enable");
        public void ToggleUser()=>ManualMode(UserEnabled?"user.disable":"user.enable");
        public void ToggleView()=>ManualMode(Virtual?"view.mixedReality":"view.virtual");
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
        bool RuntimeReady(){if(!editor.RuntimeGate.Held)return true;Recover(false);Say(editor.RuntimeGate.Reason);return false;}
        void RuntimeChanged(){if(editor.RuntimeGate.Held){Recover(false);Say(editor.RuntimeGate.Reason);}}
        void RulesChanged() => Changed?.Invoke();
        void Authoring(string _) => Interrupt();
        public void Interrupt()
        {
            userGate.Reset(); avatarGate.Reset(); Array.Clear(buttonReady,0,buttonReady.Length);
            avatar?.End(Owner); driving=false;
        }
        public void Recover()=>Recover(true);
        void Recover(bool invalidate)
        {
            bool layersChanged=editor&&editor.ResetLayerPresentation(invalidate);
            bool changed=layersChanged || AvatarEnabled || UserEnabled || view&&view.PresentationChanged;
            Interrupt(); AvatarEnabled=UserEnabled=false;
            if (changed) { input?.CancelAll(); view?.Exit(); Status=view&&view.MovementError!=null?view.MovementError:"Movement off — choose controls to enable again"; }
            CurrentModeId(); if(invalidate)modeId=Guid.NewGuid().ToString("N");
            if(changed)Changed?.Invoke();
        }
        void Update() => Tick(Mathf.Min(Time.deltaTime,.05f));
        public void Tick(float deltaTime)
        {
            if (preferences == null) return;
            CurrentModeId();
            if (!RuntimeReady()||paused || !focused || !HeadReady) { Recover(false); return; }
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
            var userAxis=userGate.Read(frame.Axis(preferences.userStick),UserEnabled && frame.Tracked(preferences.userStick),preferences.deadZone);
            if (userAxis.sqrMagnitude > 0 && float.IsFinite(deltaTime) && deltaTime > 0)
            {
                if (!view.Move(Direction(userAxis)*preferences.userSpeed*Mathf.Min(deltaTime,.05f))) Say(view.MovementError??"Your path is blocked or lacks accepted ground");
                else Say("World walking — B/Y or palm Recall brings your book and tools back");
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
                else if (UserEnabled && binding.command != ControllerCommand.None) { if(!view.Turn(binding.command == ControllerCommand.SnapLeft ? -30 : 30)&&view.MovementError!=null)Say(view.MovementError); }
            }
        }
        Vector3 Direction(Vector2 axis)
        {
            var forward=Vector3.ProjectOnPlane(room.Viewer.forward,Vector3.up);
            if (forward.sqrMagnitude < .01f) forward=Vector3.ProjectOnPlane(room.Viewer.up,Vector3.up);
            forward.Normalize(); return forward*axis.y+Vector3.Cross(Vector3.up,forward)*axis.x;
        }
        void Say(string message) { if (Status == message) return; Status=message; Changed?.Invoke(); }
        void OnApplicationPause(bool value) { paused=value; CurrentModeId(); if (value) Recover(); }
        void OnApplicationFocus(bool value) { focused=value; CurrentModeId(); if (!value) Recover(); }
        void OnDisable() => Recover();
        void OnDestroy()
        {
            Recover(); if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged; if (editor) editor.Editing-=Interrupt; if (animations) animations.Starting-=Authoring; if (workshop) workshop.Changed-=RulesChanged;
        }
    }
}
