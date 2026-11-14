// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class LiquidPouring {
        bool ShoreAvailable(RoomItem actor,string habitat,Bounds bounds,out string reason){
            reason="The actor or liquid workspace is unavailable";
            if(!editor||!actor||!editor.Frame.Valid||publishing||blocked||editor.RuntimeGate.Held||editor.WriteGate.Frozen||editor.Ownership.Suspended)return false;
            reason="An actor cannot use itself as its habitat";if(habitat==editor.Identity(actor))return false;
            CaptureMedia(false);
            var availability=CapturedMediumAvailability(bounds,editor.Identity(actor),habitat);
            if(!availability.Ready){reason=availability.Reason;return false;}
            reason=null;return true;
        }
        internal bool ShoreSurface(RoomItem actor,string habitat,Vector3 foot,RoomUprightBody body,float immersion,out float height,out string reason){
            height=0;if(!ShoreAvailable(actor,habitat,body.Bounds(foot),out reason))return false;
            for(int i=0;i<mediaCount;i++)if(media[i].Id==habitat)return media[i].SurfaceCapsuleFoot(body,immersion,out height,out reason);
            reason="The chosen shore habitat has no available liquid";return false;
        }
        internal bool ShoreAdmission(RoomItem actor,string habitat,Vector3 from,Vector3 to,RoomUprightBody body,bool floating,out string reason){
            if(!ShoreAvailable(actor,habitat,body.SweepBounds(from,to),out reason))return false;
            bool found=false;
            for(int i=0;i<mediaCount;i++)if(media[i].Id==habitat){
                found=true;
                // Dry ground may approach the selected water. A buoyant sweep
                // must stay in that exact cavity; native solids are checked by
                // the shared capsule motor, including the complete terrain bed.
                if(floating&&!media[i].SurfaceCapsuleCavity(from,to,body,out reason))return false;
                if(!floating&&!media[i].SurfaceCapsuleFoot(body,.5f,out _,out reason))return false;
                break;
            }
            if(!found){reason="The chosen shore habitat has no available liquid";return false;}
            if(!CheckTraversal(actor,from,to,body.Radius,body.Height,out var traversal,habitat)){reason=traversal.Reason;return false;}
            reason=null;return true;
        }
    }
}
