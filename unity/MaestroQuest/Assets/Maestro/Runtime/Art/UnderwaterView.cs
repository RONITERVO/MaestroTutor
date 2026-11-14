// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art {
    // This projects into virtual world shaders only. It never captures or alters
    // the physical camera/compositor, and never starts physics.
    public sealed class UnderwaterView:MonoBehaviour,INativeResourceOwner {
        static UnderwaterView owner;
        static readonly int Count=Shader.PropertyToID("_MaestroUnderwaterCount");
        readonly Camera[] cameras=new Camera[8];readonly int[] previous=new int[8];
        RoomEditor editor;UnderwaterViewProjection projection;int depth,overflowDepth,overflowPrevious;bool captured,disposed;
        internal void Initialize(RoomEditor value){editor=value;projection??=new();owner=this;}
        void OnEnable(){Camera.onPreRender+=Before;Camera.onPostRender+=After;if(editor)owner=this;}
        void Before(Camera camera){
            if(owner!=this||!editor||projection==null)return;
            if(overflowDepth>0||depth==cameras.Length){if(overflowDepth++==0)overflowPrevious=Shader.GetGlobalInt(Count);Shader.SetGlobalInt(Count,0);return;}
            cameras[depth]=camera;previous[depth]=Shader.GetGlobalInt(Count);depth++;
            if(depth==1)captured=editor.Frame.Valid&&editor.Liquids&&editor.Liquids.CaptureUnderwater(projection);
            Shader.SetGlobalInt(Count,captured&&projection.Count>0&&editor.Liquids.UnderwaterCameraReady(camera)?projection.Count:0);
        }
        void After(Camera camera){
            if(owner!=this)return;
            if(overflowDepth>0){if(--overflowDepth==0)Shader.SetGlobalInt(Count,overflowPrevious);return;}
            if(depth==0||cameras[depth-1]!=camera)return;
            Shader.SetGlobalInt(Count,previous[--depth]);cameras[depth]=null;
        }
        void OnDisable(){
            Camera.onPreRender-=Before;Camera.onPostRender-=After;depth=overflowDepth=0;
            if(owner==this){owner=null;Shader.SetGlobalInt(Count,0);}
        }
        void OnDestroy()=>ReleaseNativeResources();
        public void ReleaseNativeResources(){if(disposed)return;disposed=true;OnDisable();projection?.Dispose();projection=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){owner=null;Shader.SetGlobalInt(Count,0);}
    }
}
