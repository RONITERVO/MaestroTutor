// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    // Restore before any source animation or capture. A separate late presenter
    // applies only the owned joint after retargeting, walking and ground fitting.
    [DefaultExecutionOrder(-210)]
    internal sealed class ObjectJointAttention:MonoBehaviour
    {
        RoomEditor editor;RoomItem item;AnimationWorkshop workshop;
        string target,owner,part,session;ImportedModel model;RecipeObject recipe;
        AvatarAttentionTarget focus;JointAttentionFrame binding;
        Quaternion source,appliedRotation;bool hasSource,sampled,limited,applied;
        float yaw,pitch;Vector3 point;bool paused,focused=true;
        internal string Error{get;private set;}
        internal bool Active=>owner!=null;
        internal bool OwnedBy(string identity)=>Active&&owner==identity;
        static ImportedModel Model(RoomItem item)=>item.GetComponent<CreatedRoomObject>()?.Model;
        internal static bool Resolve(RoomEditor editor,RoomItem item,string part,out JointAttentionFrame frame,out string error)
        {
            frame=null;error="Attention needs an available character or recipe joint";
            if(!editor||!editor.isActiveAndEnabled||!item||!item.isActiveAndEnabled||editor.RuntimeGate.Held||!editor.ViewerTrackingReady||!editor.Frame.Valid)return false;
            var existing=item.GetComponent<ObjectJointAttention>();
            if(existing&&(!existing.isActiveAndEnabled||existing.paused||!existing.focused)){error="Attention is paused or inactive";return false;}
            if(item.Grab?.isSelected==true||item.PoseLocked){error="Release the character and stop posing first";return false;}
            if(editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(editor.Identity(item))==true){error="Stop authoring this character first";return false;}
            if(string.IsNullOrEmpty(part)){
                var model=Model(item);
                if(!model||!model.Ready||!model.IsHumanoid||model.IsPlaying||model.PlaybackIssue!=null){error="Load a humanoid model and stop its full-body clip first";return false;}
                frame=model.HeadAttention;
            }else{
                var recipe=item.GetComponent<RecipeObject>();
                if(!recipe||!recipe.isActiveAndEnabled||recipe.PartPlaying(part)){error="Choose an existing recipe part and stop animation on that part first";return false;}
                frame=recipe.AttentionFrame(part);
            }
            if(frame?.Valid!=true){error="The chosen joint needs a supported uniformly scaled rest frame";return false;}
            error=null;return true;
        }
        internal static bool Begin(RoomEditor editor,RoomItem item,string identity,string part,AvatarAttentionTarget focus,out ObjectJointAttention view,out string error)
        {
            view=null;
            if(string.IsNullOrEmpty(identity)){error="Attention needs an owner";return false;}
            if(!Resolve(editor,item,part,out var frame,out error)||!focus.Read(editor,editor.Viewer,out _,out error))return false;
            view=item.GetComponent<ObjectJointAttention>();
            if(view&&view.Active){error="This character already has an attention owner";return false;}
            if(!view){
                view=item.gameObject.AddComponent<ObjectJointAttention>();
                view.editor=editor;view.item=item;view.target=editor.Identity(item);
                view.item.GrabStarted+=view.Grabbed;editor.RuntimeGate.Changed+=view.GateChanged;
                item.gameObject.AddComponent<ObjectAttentionPresentation>().Owner=view;
            }
            view.Restore();view.BindWorkshop();
            view.owner=identity;view.session=editor.TemporarySessionId;view.part=part;view.focus=focus;view.binding=frame;
            view.model=Model(item);view.recipe=item.GetComponent<RecipeObject>();
            view.yaw=view.pitch=0;view.sampled=view.applied=view.limited=false;view.Error=null;return true;
        }
        void BindWorkshop()
        {
            var current=editor?editor.GetComponent<AnimationWorkshop>():null;
            if(ReferenceEquals(current,workshop))return;
            if(workshop)workshop.Starting-=Authoring;
            workshop=current;if(workshop)workshop.Starting+=Authoring;
        }
        internal static void RestoreFor(Component source)
        {
            if(source)source.GetComponentInParent<ObjectJointAttention>()?.Restore();
        }
        internal void Restore()
        {
            if(!hasSource)return;
            // An intervening explicit writer owns its newer pose. Never undo it.
            if(binding?.Joint&&Quaternion.Angle(binding.Joint.localRotation,appliedRotation)<.001f)binding.Joint.localRotation=source;
            hasSource=false;
        }
        void Update(){Restore();BindWorkshop();if(Active&&!Ready(out var error))Stop(error);}
        internal bool Ready(out string error)
        {
            error=Error??"The attention owner ended";
            if(!Active)return false;
            error="The character, world, tracking or joint changed";
            if(!editor||editor.TemporarySessionId!=session||editor.Find(target)!=item)return false;
            if(!Resolve(editor,item,part,out var current,out error))return false;
            if(!ReferenceEquals(current,binding)){error="The attention joint was replaced";return false;}
            if(model!=Model(item)||recipe!=item.GetComponent<RecipeObject>()){error="The character model was replaced";return false;}
            return focus.Read(editor,editor.Viewer,out _,out error);
        }
        internal void Present(float delta)
        {
            Restore();if(!Active)return;
            if(!Ready(out var error)||!focus.Read(editor,editor.Viewer,out var world,out error)){Stop(error);return;}
            if(!binding.Angles(world,out var targetYaw,out var targetPitch,out limited)){Stop("The attention point coincides with the chosen joint");return;}
            float blend=1-Mathf.Exp(-6*Mathf.Clamp(delta,0,.05f));
            yaw=Mathf.Lerp(yaw,targetYaw,blend);pitch=Mathf.Lerp(pitch,targetPitch,blend);
            point=editor.Frame.PointToRoom(world);sampled=true;
            var tutor=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            applied=!(tutor&&tutor.ReducedMotion);
            if(!applied)return;
            source=binding.Joint.localRotation;hasSource=true;
            binding.Joint.rotation=binding.Rotation(yaw,pitch);
            appliedRotation=binding.Joint.localRotation;
        }
        internal JObject Observe()=>new(){
            ["active"]=Active,["focus"]=Active?focus.Kind:"none",["objectId"]=Active?focus.ObjectId:"",
            ["point"]=new JObject{["x"]=Active&&sampled?point.x:0,["y"]=Active&&sampled?point.y:0,["z"]=Active&&sampled?point.z:0},
            ["yaw"]=Active?yaw:0,["pitch"]=Active?pitch:0,["limited"]=Active&&limited,["applied"]=Active&&sampled&&applied};
        internal void End(string identity){if(OwnedBy(identity))Stop(null);}
        void Stop(string error){Restore();owner=null;Error=error;applied=false;}
        void Authoring(string id){if(id==target)Stop("Character authoring began");}
        void Grabbed(RoomItem _)=>Stop("The character was grabbed");
        void GateChanged(){if(editor.RuntimeGate.Held)Stop("Attention was interrupted");}
        void OnDisable()=>Stop("Attention became inactive");
        void OnApplicationPause(bool value){paused=value;if(value)Stop("Application paused");}
        void OnApplicationFocus(bool value){focused=value;if(!value)Stop("Application lost focus");}
        void OnDestroy(){
            Stop("Attention was removed");
            if(workshop)workshop.Starting-=Authoring;
            if(item)item.GrabStarted-=Grabbed;
            if(editor)editor.RuntimeGate.Changed-=GateChanged;
        }
    }
}
