// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class VirtualRoomView
    {
        Color homeBackground;
        CameraClearFlags homeFlags;
        bool passthroughWasEnabled, presentationOwned;
        public float BackdropOpacity { get; private set; }
        public bool RealDepth { get; private set; } = true;
        // Full virtual presentation never acquires room-shaped holes. The selected
        // depth preference is retained and becomes eligible again below full opacity.
        internal bool WantsRealDepth => RealDepth && BackdropOpacity < 1;
        internal bool PresentationChanged => presentationOwned;
        internal string PresentationId { get; private set; } = Guid.NewGuid().ToString("N");
        internal static bool ValidOpacity(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
        internal bool SetPresentation(float opacity,bool realDepth)
        {
            if(!ValidOpacity(opacity)||!CanEnter)return false;
            if(opacity==BackdropOpacity&&realDepth==RealDepth)return true;
            if(opacity==0&&realDepth){ResetPresentation();return true;}
            if(!presentationOwned){
                homeBackground=viewer.backgroundColor;homeFlags=viewer.clearFlags;
                passthroughWasEnabled=passthrough&&passthrough.enabled;presentationOwned=true;
            }
            BackdropOpacity=opacity;RealDepth=realDepth;
            if(passthrough)passthrough.enabled=passthroughWasEnabled&&opacity<1;
            // Meta OpenXR's camera subsystem premultiplies clear RGB by alpha.
            // This fades only the neutral backdrop: authored surfaces and the book
            // keep their independently configured materials and opacity.
            viewer.clearFlags=CameraClearFlags.SolidColor;
            viewer.backgroundColor=new Color(.91f,.90f,.86f,opacity);
            scan?.SetVirtualView(Active);
            PresentationId=Guid.NewGuid().ToString("N");return true;
        }
        void ResetPresentation()
        {
            bool changed=presentationOwned||BackdropOpacity!=0||!RealDepth;
            if(presentationOwned){
                if(viewer){viewer.backgroundColor=homeBackground;viewer.clearFlags=homeFlags;}
                if(passthrough)passthrough.enabled=passthroughWasEnabled;
            }
            presentationOwned=false;BackdropOpacity=0;RealDepth=true;scan?.SetVirtualView(false);
            if(changed)PresentationId=Guid.NewGuid().ToString("N");
        }
    }
}
