// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool ResetLayerPresentation(bool invalidate=false) {
            bool changed=false;foreach(var layer in visibleLayers.Values)changed=layer.Reset(invalidate)||changed;return changed;
        }
        void TickLayerPresentation(float seconds) {
            var controls=GetComponent<MovementControls>();
            if(!controls||!controls.LayerPresentationReady||!isActiveAndEnabled||Ownership.Suspended){ResetLayerPresentation();return;}
            foreach(var layer in visibleLayers.Values)layer.Tick(seconds);
        }
        internal JObject ObserveLayerPresentation(string id) {
            var controls=GetComponent<MovementControls>();
            if(!controls||!controls.ConfigurationInitialized||!visibleLayers.TryGetValue(id,out var layer))return null;
            return new JObject{["id"]=id,["stateId"]=layer.StateId,["viewStateId"]=controls.ObservePresentation()["stateId"],
                ["opacity"]=layer.Opacity,["realDepth"]=layer.RealDepth,["progress"]=new JObject{
                    ["currentOpacity"]=layer.CurrentOpacity,["effectiveOpacity"]=layer.Visual.Opacity,["effectiveRealDepth"]=layer.Visual.RealDepth,
                    ["blending"]=layer.Blending,["remainingSeconds"]=layer.Remaining}};
        }
        internal bool CanPresentLayer(string id,string expected,string viewStateId,out string error) {
            error="Visual layer or viewing state changed; read visibility.presentation again";
            if(!isActiveAndEnabled||!visibleLayers.TryGetValue(id,out var layer)||layer.StateId!=expected)return false;
            var controls=GetComponent<MovementControls>();
            return controls&&controls.CanSetPresentation(viewStateId,controls.BackdropOpacity,out error);
        }
        internal bool PresentLayer(string id,string expected,string viewStateId,float opacity,bool depth,float seconds,out JObject result,out string error) {
            result=null;if(!CanPresentLayer(id,expected,viewStateId,out error))return false;
            visibleLayers[id].Set(opacity,depth,seconds);result=ObserveLayerPresentation(id);return true;
        }
    }
}
