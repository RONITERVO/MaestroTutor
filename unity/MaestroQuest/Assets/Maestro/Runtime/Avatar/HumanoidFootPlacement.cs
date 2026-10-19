// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    [DefaultExecutionOrder(-200)]
    internal sealed class HumanoidFootPlacement:MonoBehaviour
    {
        RoomEditor editor;
        RoomItem item;
        string target;
        RoomFootPlacement settings=new();
        HumanoidFootRig rig;
        HumanoidFootRig.Pose source;
        readonly HumanoidGroundPose contacts=new();
        AnimationWorkshop workshop;
        HumanoidGroundPose.Probe probe;
        bool paused,focused=true;
        internal string Status{get;private set;}="authored";
        internal static void Configure(RoomEditor editor,RoomItem item,RoomObjectData data)
        {
            var view=item.GetComponent<HumanoidFootPlacement>();
            if(data.footPlacement.enabled&&!view){
                view=item.gameObject.AddComponent<HumanoidFootPlacement>();
                view.editor=editor;view.item=item;view.target=data.id;view.probe=view.Probe;
                view.BindWorkshop();
                item.GrabStarted+=view.Grabbed;editor.RuntimeGate.Changed+=view.GateChanged;
                var late=item.gameObject.AddComponent<HumanoidFootPresentation>();late.Owner=view;
            }
            if(!view)return;
            view.Restore();view.contacts.Clear();view.settings=data.footPlacement.Copy();
            view.enabled=view.settings.enabled;
            if(!view.enabled)view.Status="authored";
        }
        internal static bool Available(RoomItem item,RoomFootPlacement settings,out HumanoidFootRig rig,out string error)
        {
            rig=null;error="Load a supported humanoid character to measure its feet";
            if(!item||!item.isActiveAndEnabled||settings?.Valid!=true)return false;
            var avatar=item.GetComponent<MaestroAvatar>();var model=avatar?avatar.CustomModel:item.GetComponent<CreatedRoomObject>()?.Model;
            string hash=avatar?avatar.ModelHash:model?model.AssetHash:null;
            if(avatar&&avatar.ModelBusy)return false;
            if(settings.enabled&&settings.modelHash!=(hash??"")){error="The model changed; configure foot placement for this exact model";return false;}
            if(model){
                if(!model.Ready||model.PlaybackIssue!=null){error=model.PlaybackIssue??error;return false;}
                rig=model.FootRig;
            }else if(avatar&&string.IsNullOrEmpty(hash))rig=avatar.PoseRig?avatar.PoseRig.FootRig:null;
            if(rig?.Valid!=true){error="This rig or its rest bounds cannot provide reliable foot contacts";return false;}
            error=null;return true;
        }
        internal static void RestoreFor(Component source)
        {
            if(source)source.GetComponentInParent<HumanoidFootPlacement>()?.Restore();
        }
        internal void Restore()=>source?.Restore();
        void BindWorkshop()
        {
            var current=editor?editor.GetComponent<AnimationWorkshop>():null;
            if(ReferenceEquals(current,workshop))return;
            if(workshop)workshop.Starting-=Authoring;
            workshop=current;if(workshop)workshop.Starting+=Authoring;
        }
        void Update(){Restore();BindWorkshop();} // Before authoring, explicit samples and Animator evaluation.
        void Authoring(string id){if(id==target)Suspend("authoring");}
        void Grabbed(RoomItem _)=>Suspend("held");
        void GateChanged(){if(editor&&editor.RuntimeGate.Held)Suspend("paused");}
        void Suspend(string reason){Restore();contacts.Clear();Status=reason;}
        bool CanPresent(out string reason)
        {
            reason="paused";
            if(!editor||!editor.isActiveAndEnabled||!item||!item.isActiveAndEnabled||editor.RuntimeGate.Held||paused||!focused)return false;
            reason="authored pose";
            var avatar=item.GetComponent<MaestroAvatar>();
            if(item.Grab?.isSelected==true||item.PoseLocked||workshop&&workshop.ControlsTarget(target)||
                avatar&&(avatar.PoseRig&&avatar.PoseRig.IsPosing||avatar.PoseRig&&avatar.PoseRig.IsHolding||!avatar.FootPresentationAllowed))return false;
            var body=item.GetComponent<Rigidbody>();var rigid=item.GetComponent<RigidRoomItem>();
            if(body&&!body.isKinematic||rigid&&(rigid.Dynamic||rigid.AnimationOwned)&&!ImportedObjectWalk.Owns(item)||
                item.GetComponent<HeldRoomProp>()?.Holding==true||item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)return false;
            var model=avatar?avatar.CustomModel:item.GetComponent<CreatedRoomObject>()?.Model;
            if(model&&model.IsPlaying)return false;
            reason="Foot placement needs an upright uniformly scaled actor";
            return TwoBoneFootSolver.Uniform(item.transform)&&Vector3.Dot(item.transform.up,Vector3.up)>.99999f;
        }
        bool Probe(Vector3 point,float band,out RaycastHit hit)=>editor.SampleFootGround(item,point,band,out hit);
        internal void Present(float dt)
        {
            Restore();BindWorkshop();
            if(!enabled||!settings.enabled)return;
            if(!CanPresent(out var reason)||!Available(item,settings,out var current,out reason)){Suspend(reason);return;}
            if(!ReferenceEquals(rig,current)){rig=current;source=new(rig);contacts.Clear();}
            float baseline;
            var avatar=item.GetComponent<MaestroAvatar>();var model=avatar?avatar.CustomModel:item.GetComponent<CreatedRoomObject>()?.Model;
            baseline=model?model.transform.TransformPoint(Vector3.up*model.LocalBounds.min.y).y:item.transform.position.y;
            float band=.16f*rig.Scale+.1f;
            var l=rig.Left.Contact;var r=rig.Right.Contact;l.y=r.y=baseline;
            if(!editor.SampleFootGround(item,l,band,out var left)||!editor.SampleFootGround(item,r,band,out var right)){
                Suspend("accepted ground unavailable");return;
            }
            source.Capture();
            if(!contacts.Apply(rig,left,right,baseline,settings,dt,avatar?avatar.FootWalking:ImportedObjectWalk.StatusFor(item)=="walking",probe,out var status))Restore();
            Status=status;
        }
        void OnDisable()=>Suspend("inactive");
        void OnApplicationPause(bool value){paused=value;if(value)Suspend("paused");}
        void OnApplicationFocus(bool value){focused=value;if(!value)Suspend("unfocused");}
        void OnDestroy()
        {
            Restore();contacts.Clear();
            if(workshop)workshop.Starting-=Authoring;
            if(item)item.GrabStarted-=Grabbed;
            if(editor)editor.RuntimeGate.Changed-=GateChanged;
        }
    }
}
