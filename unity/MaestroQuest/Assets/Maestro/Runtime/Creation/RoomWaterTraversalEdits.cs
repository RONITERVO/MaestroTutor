// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool CanConfigureWaterTraversal(string id,int revision,RoomWaterTraversal settings,out string error){
            if(!CurrentSettings(id,revision,false,out error))return false;
            error="Choose valid water traversal settings for Maestro or a creation";
            if(id=="book"||settings==null||!settings.Valid)return false;error=null;return true;
        }
        internal bool ConfigureWaterTraversal(string id,int revision,RoomWaterTraversal settings,out string error){
            if(!CanConfigureWaterTraversal(id,revision,settings,out error))return false;
            return EditObject(id,false,data=>data.waterTraversal=settings.Copy(),"Water traversal settings saved",false,out error);
        }
        internal JObject ObserveWaterTraversal(string id){
            var data=Read(id);if(data==null||id=="book"||!Find(id))return null;
            return new JObject{["target"]=id,["revision"]=ObjectRevision(id),["mode"]=data.waterTraversal.mode,["effectiveMode"]=RoomWaterTraversal.Effective(data).mode,["maxDepthMetres"]=System.Math.Round(data.waterTraversal.maxDepthMetres,6),["temporary"]=TemporaryRoom};
        }
        // Recorded prop movement is opt-in. The upright collision envelope is
        // shared by preview, programs and read-only path inspection.
        internal bool WaterMotionStep(RoomItem item,Vector3 to,out WaterTraversalResult result){
            result=default;
            if(!item||!RoomRecipe.Finite(to)){result=WaterTraversalResult.Refused("The movement target or position is unavailable");return false;}
            if(item.WaterTraversal.mode=="ignore")return true;
            // Shared commands can move transforms before the next physics step.
            // Refresh both the actor envelope and supporting collision geometry.
            Physics.SyncTransforms();
            var from=item.transform.position;float radius,height;
            if(Identity(item)=="maestro"){radius=.25f*item.transform.lossyScale.y;height=1.7f*item.transform.lossyScale.y;}
            else{
                bool found=false;Bounds bounds=default;
                if(!item.Grab){result=WaterTraversalResult.Refused("Water traversal needs ready collision geometry");return false;}
                foreach(var c in item.Grab.colliders){if(!c||!c.enabled||c.isTrigger)continue;if(found)bounds.Encapsulate(c.bounds);else{bounds=c.bounds;found=true;}}
                if(!found){result=WaterTraversalResult.Refused("Water traversal needs ready collision geometry");return false;}
                var offset=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-from;from+=offset;to+=offset;
                radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;height=bounds.size.y;
            }
            if(!Liquids){result=WaterTraversalResult.Refused("Water traversal is unavailable");return false;}
            return Liquids.CheckTraversal(item,from,to,Mathf.Max(.001f,radius),Mathf.Max(.002f,height),out result);
        }
        internal JObject ObserveWaterPath(string id,Vector3 authoredTo){
            var item=Find(id);if(!item||id=="book"||!Frame.Valid||!RoomViewpoint.ValidPosition(authoredTo))return null;
            bool allowed=WaterMotionStep(item,Frame.PointToWorld(authoredTo),out var result);
            return new JObject{["allowed"]=allowed,["bodyId"]=result.BodyId??"",["depthMetres"]=System.Math.Round(result.Depth/Frame.MetresPerUnit,6),["reason"]=result.Reason??""};
        }
    }
    internal readonly struct WaterTraversalResult {
        internal readonly string BodyId,Reason;internal readonly float Depth;internal readonly Vector3 Foot;
        internal WaterTraversalResult(string id,float depth,string reason,Vector3 foot=default){BodyId=id;Depth=depth;Reason=reason;Foot=foot;}
        internal static WaterTraversalResult Refused(string reason)=>new("",0,reason);
    }
    public sealed partial class LiquidPouring {
        internal void RouteFootprint(string id,float padding,float height,float footHeight,System.Collections.Generic.List<Vector3> result){
            result.Clear();CaptureMedia(false);
            for(int i=0;i<mediaCount;i++)if(media[i].Id==id){LiquidRouteFootprint.Build(in media[i],padding,height,footHeight,result);return;}
        }
        internal bool CheckTraversal(RoomItem actor,Vector3 from,Vector3 to,float radius,float height,out WaterTraversalResult result){
            result=default;var policy=actor?actor.WaterTraversal:null;
            if(policy==null||!policy.Valid||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!float.IsFinite(radius)||!float.IsFinite(height)||radius<=0||height<=0){
                result=WaterTraversalResult.Refused("Water traversal needs a valid actor and walking body");return false;
            }
            if(policy.mode=="ignore")return true;
            if(!editor||!editor.Frame.Valid||blocked||publishing||editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen){
                result=WaterTraversalResult.Refused("Water traversal is unavailable while the room is changing");return false;
            }
            if(vessels.Count>0&&(!float.IsFinite(Physics.gravity.sqrMagnitude)||Physics.gravity.sqrMagnitude<.01f)){
                result=WaterTraversalResult.Refused("Water traversal requires a known gravity direction");return false;
            }
            CaptureMedia(false);using var environment=mediumEnvironment.Begin(world);
            float allowedDepth=Mathf.Min(policy.maxDepthMetres*editor.Frame.MetresPerUnit,height*.6f);
            for(int i=0;i<mediaCount;i++){
                var medium=media[i];if(medium.Item==actor||!medium.Sweep(from,to,radius,height,out float depth,out var point,out var foot))continue;
                string reason=!medium.Item.GetComponent<RigidRoomItem>().GeometryReady||!environment.CanOccupy(point,actor,medium.Item)?"The water or actor environment is unavailable":
                    policy.mode!="wade"?"This actor avoids water":depth>allowedDepth+.001f?"This water is too deep for the actor to wade; swimming is not available":null;
                if(reason!=null){result=new(medium.Id,depth,reason,foot);return false;}
                if(depth>result.Depth)result=new(medium.Id,depth,null,foot);
            }
            return true;
        }
    }
}
