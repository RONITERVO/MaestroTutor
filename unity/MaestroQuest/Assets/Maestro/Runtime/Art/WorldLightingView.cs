// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art
{
    /// <summary>One active workspace projects accepted region lighting into shared shader uniforms.</summary>
    public sealed class WorldLightingView:MonoBehaviour
    {
        static WorldLightingView owner;
        static readonly int Enabled=Shader.PropertyToID("_MaestroLightingEnabled"),Ambient=Shader.PropertyToID("_MaestroAmbient"),Sun=Shader.PropertyToID("_MaestroSun"),Direction=Shader.PropertyToID("_MaestroSunDirection");
        RoomEditor editor;int revision=-1,timeRevision=-1;Quaternion rotation;bool valid;double second=-1;
        WorldLightingProjection projection;
        internal void Initialize(RoomEditor value){editor=value;Activate();}
        void OnEnable(){if(editor)Activate();}
        void Activate(){owner=this;revision=timeRevision=-1;Refresh();}
        void LateUpdate(){if(owner==this)Refresh();}
        internal void Refresh(){
            if(owner!=this||!editor)return;
            bool changed=revision!=editor.LightingRevision||timeRevision!=editor.WorldTimeRevision;
            if(changed){revision=editor.LightingRevision;timeRevision=editor.WorldTimeRevision;projection=new WorldLightingProjection(editor.Lighting,editor.WorldTime.settings);}
            bool frameValid=editor.Frame.Valid;var orientation=editor.transform.rotation;double at=projection.Cycle?editor.WorldSecond:-1;
            if(!changed&&rotation==orientation&&valid==frameValid&&second==at)return;
            rotation=orientation;valid=frameValid;second=at;var light=projection.Sample(at);
            Shader.SetGlobalFloat(Enabled,frameValid&&light.Enabled?1:0);
            if(!frameValid||!light.Enabled)return;
            Shader.SetGlobalVector(Ambient,light.Ambient);Shader.SetGlobalVector(Sun,light.Sun);Shader.SetGlobalVector(Direction,orientation*light.Direction);
        }
        void OnDisable(){if(owner!=this)return;owner=null;Shader.SetGlobalFloat(Enabled,0);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){owner=null;Shader.SetGlobalFloat(Enabled,0);}
    }
}
