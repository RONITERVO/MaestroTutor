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
        RoomEditor editor;int revision=-1;Quaternion rotation;bool valid;
        internal void Initialize(RoomEditor value){editor=value;Activate();}
        void OnEnable(){if(editor)Activate();}
        void Activate(){owner=this;revision=-1;Refresh();}
        void LateUpdate(){if(owner==this)Refresh();}
        internal void Refresh() {
            if(owner!=this||!editor)return;
            bool frameValid=editor.Frame.Valid;var orientation=editor.transform.rotation;
            if(revision==editor.LightingRevision&&rotation==orientation&&valid==frameValid)return;
            revision=editor.LightingRevision;rotation=orientation;valid=frameValid;
            var lighting=editor.Lighting;
            bool enabled=frameValid&&lighting!=null&&lighting.Valid&&lighting.enabled;
            Shader.SetGlobalFloat(Enabled,enabled?1:0);
            if(!enabled)return;
            Shader.SetGlobalVector(Ambient,Energy(lighting.ambientColor,lighting.ambientIntensity));
            Shader.SetGlobalVector(Sun,Energy(lighting.sunColor,lighting.sunIntensity));
            Shader.SetGlobalVector(Direction,orientation*lighting.SunDirection);
        }
        static Vector4 Energy(string hex,float intensity){ColorUtility.TryParseHtmlString(hex,out var color);if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;return new Vector4(color.r*intensity,color.g*intensity,color.b*intensity,0);}
        void OnDisable(){if(owner!=this)return;owner=null;Shader.SetGlobalFloat(Enabled,0);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){owner=null;Shader.SetGlobalFloat(Enabled,0);}
    }
}
