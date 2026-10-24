// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Preview and program playback use one native-input owner. This prepares
    // the root collider envelope; it does not infer a deforming-limb envelope.
    internal sealed class RoomRecordedMotionInputs:MonoBehaviour {
        RoomEditor editor;RoomItem item;RoomMotionFrame space;RoomMotion motion;
        RoomPlaybackClock clock;Func<bool> owned;RoomContactInputs inputs;
        bool ended;string issue;
        internal static RoomRecordedMotionInputs Begin(RoomEditor editor,RoomItem item,RoomPlaybackClock clock,RoomMotion motion,Func<bool> owned){
            var value=item.gameObject.AddComponent<RoomRecordedMotionInputs>();
            value.editor=editor;value.item=item;value.clock=clock;value.motion=motion;value.owned=owned;value.space=new RoomMotionFrame(editor.transform);
            clock.Start();value.Renew();return value;
        }
        bool Owned=>!ended&&this&&isActiveAndEnabled&&editor&&item&&item.isActiveAndEnabled&&editor.Find(editor.Identity(item))==item&&
            item.Grab&&!item.Grab.isSelected&&editor.RuntimeGate.Held==false&&editor.WriteGate.Frozen==false&&owned()&&
            (!item.GetComponent<RigidRoomItem>()||item.GetComponent<RigidRoomItem>().GeometryReady);
        void Renew(){inputs?.Dispose();inputs=new RoomContactInputs(editor,editor.Identity(item),()=>Owned);}
        RuleActionState Fail(string error,out string reason){issue=reason=error;inputs?.Dispose();clock.Pause(this,true);return RuleActionState.Failed;}
        internal RuleActionState Prepare(MotionFrame candidate,out string error){
            error=issue;if(issue!=null)return RuleActionState.Failed;
            if(!Owned)return Fail("Recorded playback lost its target or ownership",out error);
            if(!space.TryRead(out var frame)||!frame.Read(item.transform,out _,out _,out float currentScale))return Fail(RoomMotionFrame.Changed,out error);
            if(!clock.Valid(out error))return Fail(error,out error);
            if(candidate==null||!RoomRecipe.Finite(candidate.position)||!MotionFrame.ValidRotation(candidate.rotation)||!float.IsFinite(candidate.scale)||candidate.scale<=0)return Fail("The recorded root pose is unavailable",out error);
            if(motion?.frames==null||motion.frames.Length==0)return Fail("The recorded root path is unavailable",out error);
            float maximumScale=candidate.scale;
            foreach(var key in motion.frames){
                if(key==null||!RoomRecipe.Finite(key.position)||!MotionFrame.ValidRotation(key.rotation)||!float.IsFinite(key.scale)||key.scale<=0)return Fail("The recorded root path is unavailable",out error);
                maximumScale=Mathf.Max(maximumScale,key.scale);
            }
            // The bounded recording is known before playback. Its complete root
            // path covers skipped keys and loop boundaries; the pivot sphere
            // covers every intermediate rotation and its largest uniform scale.
            var localScale=item.transform.localScale*(maximumScale/currentScale);
            if(!RoomContactSweep.Item(item,new Pose(item.transform.position,item.transform.rotation),localScale,out var sweep))return Fail("Recorded root collision bounds are unavailable",out error);
            var size=sweep.size;
            foreach(var key in motion.frames)sweep.Encapsulate(new Bounds(frame.PointToWorld(key.position),size));
            sweep.Encapsulate(new Bounds(frame.PointToWorld(candidate.position),size));
            if(inputs.SourceChanged){clock.Pause(this,true);Renew();}
            if(!inputs.Require(sweep,out error)){
                if(!inputs.Pending)return Fail(error,out error);
                clock.Pause(this,true);return RuleActionState.Preparing;
            }
            clock.Pause(this,false);return RuleActionState.Ready;
        }
        internal JObject Observe()=>ended?RoomContactInputs.State("inactive",false,null):issue!=null?RoomContactInputs.State("unavailable",false,issue):inputs?.Observe()??RoomContactInputs.State("unavailable",false,null);
        internal void End(){if(ended)return;ended=true;inputs?.Dispose();clock?.Pause(this,false);enabled=false;Destroy(this);}
        void OnDisable(){End();}
        void OnDestroy(){End();}
    }
}
