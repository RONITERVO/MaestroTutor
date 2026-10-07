// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.AI;

namespace Maestro.Quest.Avatar
{
    public enum AvatarSpatialMode { Look, Follow, Manual }

    /// <summary>Explicitly owned movement; editing, grips, recovery and interruption take priority.</summary>
    [DefaultExecutionOrder(125)]
    public sealed class AvatarSpatialMotion : MonoBehaviour
    {
        MaestroAvatar avatar;
        RoomItem item;
        RoomEditor editor;
        AnimationWorkshop animations;
        RoomInteraction room;
        RoomNavigation navigation;
        Func<bool> tracked;
        string owner;
        RoomOwnership.Lease ownershipLease;
        Vector3 manualDirection;
        float manualAt;
        public bool OwnedBy(string identity) => owner == identity;
        public void ManualDirection(string identity,Vector3 direction) { if (owner == identity && mode == AvatarSpatialMode.Manual && float.IsFinite(direction.sqrMagnitude)) { manualDirection=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(direction,Vector3.up),1); manualAt=Time.unscaledTime; } }
        AvatarSpatialMode mode;
        bool paused, focused = true;
        NavMeshPath path;
        readonly Collider[] overlaps = new Collider[32];
        readonly RaycastHit[] hits = new RaycastHit[32];
        Vector3[] corners = Array.Empty<Vector3>();
        int corner;
        uint navigationRevision;
        float nextPath, nextRemember, yaw, pitch;
        public float Distance { get; private set; } = 1.3f;
        public float Speed { get; private set; } = .65f;
        public bool Active => owner != null;
        public AvatarSpatialMode Mode => mode;
        public string Status { get; private set; } = "Choose Look at me or Follow me";
        public event Action Changed;
        public void Initialize(RoomEditor source, AnimationWorkshop authoring, RoomInteraction interaction, RoomNavigation paths, Func<bool> headTracked = null)
        {
            path = new NavMeshPath();
            avatar = GetComponent<MaestroAvatar>(); item = GetComponent<RoomItem>();
            editor = source; animations = authoring; room = interaction; navigation = paths; tracked = headTracked ?? (() => room.Viewer && room.Viewer.gameObject.activeInHierarchy);
            editor.Editing += Stop; editor.ItemGrabbed += Grabbed; animations.Starting += Authoring;
            avatar.ModelChanged += ModelChanged;
            editor.Changed += ReadPreferences; ReadPreferences();
        }
        public bool CanBegin(AvatarSpatialMode value, out string error,bool allowAuthoringTakeover=false)
        {
            error = null;
            if(editor.RuntimeGate.Held)error=editor.RuntimeGate.Reason;
            else if (paused || !focused || !room || !room.Viewer || !tracked()) error = "Head tracking is unavailable; try again when tracking returns";
            else if (!avatar || !avatar.PoseRig || avatar.ModelBusy) error = "Wait for Maestro to finish loading";
            else if (item.Grab.isSelected || avatar.PoseRig.IsHolding || !allowAuthoringTakeover&&animations.ControlsTarget("maestro")) error = "Release Maestro and stop posing or recording first";
            else if (value != AvatarSpatialMode.Look && (!editor.PhysicsWorld || !editor.PhysicsWorld.Running)) error = "Load the room, check its alignment, then Start physics before walking";
            return error == null;
        }
        public bool Begin(string identity, AvatarSpatialMode value, out string error,RoomActorRole role=RoomActorRole.Control)
        {
            if (string.IsNullOrEmpty(identity)) { error = "Movement needs an action owner"; return false; }
            if (!CanBegin(value,out error,allowAuthoringTakeover:role==RoomActorRole.Control)) { Say(error); return false; }
            if (value != AvatarSpatialMode.Look)
            {
                float scale = transform.lossyScale.y;
                if (!navigation || !navigation.Prepare(.25f*scale,1.7f*scale,out error)) { Say(error ?? "Room navigation is unavailable"); return false; }
                if (!navigation.Sample(transform.position,.25f,out _)) { error = "Place Maestro's feet near a supported walking surface, then try walking"; Say(error); return false; }
            }
            var claims=value==AvatarSpatialMode.Look?new[]{new BehaviourCatalog.Claim("maestro","gaze")}:
                new[]{new BehaviourCatalog.Claim("maestro","locomotion"),new BehaviourCatalog.Claim("maestro","gaze")};
            // Restarting our own direct command is not a borrowed scheduler lease.
            bool ownRestart=ownershipLease?.Held==true&&ownershipLease.Id==identity;
            if(ownRestart&&!editor.Ownership.CanAcquire(identity,role,claims,out error,replaceControl:role==RoomActorRole.Control)){Say(error);return false;}
            if(ownRestart)Stop();
            bool borrowed=editor.Ownership.Covers(identity,claims);
            RoomOwnership.Lease acquired=null;
            if(!borrowed&&!editor.Ownership.TryAcquire(identity,"Maestro "+value.ToString().ToLowerInvariant(),role,claims,
                _=>Stop(),out acquired,out error,preservePlacement:true,replaceControl:role==RoomActorRole.Control)) {Say(error);return false;}
            // A successful manual takeover also retires older queued target work.
            // Do this only after readiness and acquisition so a refused input is inert.
            if(role==RoomActorRole.Control)editor.GetComponent<Maestro.Quest.Rules.RoomRules>()?.Scheduler.CancelQueuedConflicting(claims);
            // An acquired takeover already stopped an older actor via the arbiter.
            // A delegated module borrows its scheduler's reservation.
            Stop();ownershipLease=acquired;
            manualDirection=Vector3.zero; manualAt=Time.unscaledTime; owner = identity; mode = value; yaw = pitch = 0; corners = Array.Empty<Vector3>(); corner = 0; nextPath = 0;
            avatar.SetEditing(true,preserveUpperBody:true); avatar.SpatialWalk(0);
            Say(value == AvatarSpatialMode.Manual ? "Maestro stick active — center it to stop" : value == AvatarSpatialMode.Follow ? "Following you — Stop or grip Maestro to end" : "Looking at you — Stop or pose Maestro to end"); return true;
        }
        public void End(string identity) { if (owner == identity) Stop(); }
        public void Stop()
        {
            if (!Active) return;
            owner = null; corners = Array.Empty<Vector3>();ownershipLease?.Dispose();ownershipLease=null;
            if (avatar) { avatar.SpatialWalk(0); avatar.SetEditing(false,preserveUpperBody:true); }
            if (editor) editor.RememberPlacement("maestro");
            Say("Maestro stopped — placement saved");
        }
        public void SetPreferences(float distance, float speed)
        {
            Distance = Mathf.Clamp(distance,.8f,2.5f); Speed = Mathf.Clamp(speed,.2f,1.2f);
            nextPath = 0; Changed?.Invoke();
        }
        void ReadPreferences()
        {
            var data = editor.Read("maestro"); if (data == null) return;
            SetPreferences(data.followDistance == 0 ? 1.3f : data.followDistance,data.walkSpeed == 0 ? .65f : data.walkSpeed);
        }
        void Grabbed(RoomItem value) { if (value == item) Stop(); }
        void Authoring(string id) { if (id == "maestro") Stop(); }
        void ModelChanged() { if (avatar.ModelBusy) Stop(); }
        void Say(string message) { if (Status == message) return; Status = message; Changed?.Invoke(); }
        void Update()
        {
            if (!Active) return;
            if (editor.RuntimeGate.Held||paused || !focused || !tracked() || !avatar || avatar.ModelBusy || item.Grab.isSelected) { Stop(); return; }
            if (mode == AvatarSpatialMode.Look) return;
            if (!navigation || !navigation.Ready) { Stop(); Say("Walking stopped — check room alignment and Start physics again"); return; }
            if(navigationRevision!=navigation.SurfaceRevision) { navigationRevision=navigation.SurfaceRevision;corners=Array.Empty<Vector3>();corner=0;nextPath=0; }
            float dt = Mathf.Min(Time.deltaTime,.05f);
            if (mode == AvatarSpatialMode.Manual) { ManualStep(dt); return; }
            var delta = Vector3.ProjectOnPlane(room.Viewer.position-transform.position,Vector3.up);
            float moved = 0;
            if (delta.magnitude > Distance + .05f && dt > 0)
            {
                var goal = new Vector3(room.Viewer.position.x,transform.position.y,room.Viewer.position.z);
                if (Time.unscaledTime >= nextPath)
                {
                    nextPath = Time.unscaledTime + .35f;
                    corners = navigation.Path(transform.position,goal,path) ? path.corners : Array.Empty<Vector3>(); corner = 1;
                }
                while (corner < corners.Length && Vector3.Distance(transform.position,corners[corner]) < .06f) corner++;
                if (corner < corners.Length)
                {
                    var direction = corners[corner]-transform.position;
                    var next = Vector3.MoveTowards(transform.position,corners[corner],Mathf.Min(Speed*dt,delta.magnitude-Distance));
                    string blocked = "The walking surface has no space for this step — try Size or reposition Maestro";
                    if (navigation.Sample(next,.10f,out var floor) && ClearStep(floor,out blocked))
                    {
                        moved = Vector3.Distance(transform.position,floor); transform.position = floor;
                        direction.y = 0; if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),120*dt);
                        Say("Following you — Stop or grip Maestro to end");
                    }
                    else Say(blocked);
                }
                else Say("No connected path — try Size, or place Maestro on the same clear floor");
            }
            else
            {
                if (delta.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),90*dt);
                Say("Maestro is keeping your chosen distance");
            }
            avatar.SpatialWalk(dt > 0 ? moved/dt : 0);
            if (Time.unscaledTime >= nextRemember) { nextRemember = Time.unscaledTime+1; editor.RememberPlacement("maestro"); }
        }
        void ManualStep(float dt)
        {
            if (Time.unscaledTime-manualAt > .15f) { Stop(); return; }
            var next=transform.position+manualDirection*Speed*dt;
            string error="The walking surface has no space for this step"; float moved=0;
            if (manualDirection.sqrMagnitude > 0 && navigation.DirectStep(transform.position,next,out var floor) && ClearStep(floor,out error))
            {
                moved=Vector3.Distance(transform.position,floor); transform.position=floor;
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(manualDirection),180*dt);
                Say("Maestro stick active — center it to stop");
            }
            else if (manualDirection.sqrMagnitude > 0) Say(error);
            avatar.SpatialWalk(dt > 0 ? moved/dt : 0);
            if (Time.unscaledTime >= nextRemember) { nextRemember=Time.unscaledTime+1; editor.RememberPlacement("maestro"); }
        }
        internal RoomMotionFrame MotionFrame => new(editor?editor.transform:null);
        internal bool CanBeginAuthored(out string error)
        {
            error="Room navigation is unavailable";if(!editor||!navigation)return false;
            if(!CanBegin(AvatarSpatialMode.Manual,out error))return false;
            float scale=transform.lossyScale.y;
            if(!navigation||!navigation.Prepare(.25f*scale,1.7f*scale,out error)){error??="Room navigation is unavailable";return false;}
            if(!navigation.Sample(transform.position,.08f,out _)){error="Place Maestro on a supported walking surface before authored travel";return false;}
            return true;
        }
        internal bool CanContinueAuthored(out string error)
        {
            error=null;
            if(!CanBegin(AvatarSpatialMode.Manual,out error))return false;
            if(!navigation||!navigation.Ready){error="Authored motion stopped — check room alignment and Start physics again";return false;}
            return true;
        }
        internal bool TryAuthoredStep(Vector3 next,float extraHeight,out string error)
        {
            error="Authored motion blocked — no clear, level walking surface for this step";
            if(!float.IsFinite(next.sqrMagnitude)||!navigation.DirectStep(transform.position,next,out var floor)||Mathf.Abs(floor.y-next.y)>.03f)return false;
            // Respect the user's space along the whole step, including a clip with discontinuous keys.
            var from=Vector3.ProjectOnPlane(transform.position-room.Viewer.position,Vector3.up);var to=Vector3.ProjectOnPlane(next-room.Viewer.position,Vector3.up);var step=to-from;
            float closest=(from+step*(step.sqrMagnitude<.000001f?0:Mathf.Clamp01(-Vector3.Dot(from,step)/step.sqrMagnitude))).magnitude;
            float clearance=Mathf.Max(.6f,.35f*transform.lossyScale.y);
            if(closest<clearance&&!(to.magnitude>=from.magnitude&&closest>=from.magnitude-.001f)){error="Authored motion stopped to keep clear of you";return false;}
            return ClearStep(next,out error,extraHeight);
        }
        internal void RememberAuthoredPlacement(){if(editor)editor.RememberPlacement("maestro");}
        bool ClearStep(Vector3 next, out string blocked,float extraHeight=0)
        {
            blocked = "Path crowded — try Size or reposition Maestro";
            float scale = transform.lossyScale.y, r = .25f*scale, h = 1.7f*scale+extraHeight;
            var bottom = transform.position + Vector3.up*(r+.035f); var top = transform.position + Vector3.up*(h-r);
            var step = next-transform.position;
            int mask = (1<<RoomPhysicsLayers.Scanned) | (1<<RoomPhysicsLayers.Item) | (1<<RoomPhysicsLayers.Environment);
            mask=editor.PhysicsWorld.CollisionMask(mask);
            int count = Physics.CapsuleCastNonAlloc(bottom,top,r,step.normalized,hits,step.magnitude+.01f,mask,QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i=0;i<count;i++) if (BodyObstacle(hits[i].collider)) { blocked = Blocker(hits[i].collider); return false; }
            count = Physics.OverlapCapsuleNonAlloc(bottom+step,top+step,r,overlaps,mask,QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i=0;i<count;i++) if (BodyObstacle(overlaps[i])) { blocked = Blocker(overlaps[i]); return false; }
            return true;
        }
        bool BodyObstacle(Collider collider)
        {
            if(collider.transform.IsChildOf(transform))return false;
            // Carried props have their own swept trajectory checks. Their colliders
            // return to ordinary obstacles immediately on release or ownership change.
            var prop=collider.GetComponentInParent<HeldRoomProp>();
            return !prop||!prop.Holding||prop.HolderId!="maestro";
        }
        string Blocker(Collider collider)
        {
            string obstacle = collider.gameObject.layer == RoomPhysicsLayers.Scanned ? "scanned room surface" :
                collider.GetComponentInParent<CreatedRoomObject>() ? "room object" :
                editor.Find("book")&&collider.transform.IsChildOf(editor.Find("book").transform) ? "book" : "obstacle";
            return "Path blocked by " + obstacle + " — try Size or reposition Maestro";
        }
        void LateUpdate()
        {
            if(editor&&editor.RuntimeGate.Held)return;
            if (!Active || mode == AvatarSpatialMode.Manual || !avatar || !room.Viewer) return;
            var head = avatar.PoseRig.CanonicalBone(PoseJoint.Head);
            var target = transform.InverseTransformDirection(room.Viewer.position-head.position);
            float targetYaw = Mathf.Clamp(Mathf.Atan2(target.x,target.z)*Mathf.Rad2Deg,-60,60);
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(target.y,new Vector2(target.x,target.z).magnitude)*Mathf.Rad2Deg,-30,30);
            float blend = 1-Mathf.Exp(-6*Mathf.Min(Time.deltaTime,.05f));
            yaw = Mathf.Lerp(yaw,targetYaw,blend); pitch = Mathf.Lerp(pitch,targetPitch,blend);
            if (!avatar.ReducedMotion) head.rotation = Quaternion.AngleAxis(yaw,transform.up)*Quaternion.AngleAxis(pitch,transform.right)*head.rotation;
        }
        void OnApplicationPause(bool value) { paused = value; if (value) Stop(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Stop(); }
        void OnDisable() => Stop();
        void OnDestroy()
        {
            Stop(); if (editor) { editor.Editing -= Stop; editor.ItemGrabbed -= Grabbed; editor.Changed -= ReadPreferences; }
            if (animations) animations.Starting -= Authoring;
            if (avatar) avatar.ModelChanged -= ModelChanged;
        }
    }
}
