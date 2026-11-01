// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Owned ink meshes follow the configured anchor. No colliders or rigid bodies are created for ink.</summary>
    public sealed class DrawingSurfaceView:MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner
    {
        readonly Dictionary<string,PreparedDrawingSurfaces.Patch> patches=new();
        public int StrokeCount=>patches.Values.Sum(p=>p.Data.strokes.Length);
        internal PreparedDrawingSurfaces.Patch Reusable(string id)=>patches.TryGetValue(id,out var patch)&&patch.Root?patch:null;
        public void Apply(DrawingSurface[] definitions){
            var editor=GetComponentInParent<RoomEditor>();var item=GetComponent<Maestro.Quest.Interaction.RoomItem>();string target=editor&&item?editor.Identity(item):"";
            using var candidate=new PreparedDrawingSurfaces(editor?editor.WorldIdentity:null,target,definitions,this);Apply(target,definitions,candidate);
        }
        internal void Apply(string target,DrawingSurface[] definitions,PreparedDrawingSurfaces candidate){
            var changes=candidate.Take(this,target,definitions);var active=changes.Select(c=>c.Definition.id).ToHashSet();
            foreach(var id in patches.Keys.Where(id=>!active.Contains(id)).ToArray()){patches[id].Release();patches.Remove(id);}
            foreach(var change in changes){
                var patch=change.Patch;
                foreach(var pair in patch.Ink)if(!change.Ink.TryGetValue(pair.Key,out var kept)||kept!=pair.Value)pair.Value.Dispose();
                patch.Data=change.Definition;patch.Ink=change.Ink;patches[patch.Data.id]=patch;
                foreach(var source in patch.Data.strokes){var stroke=patch.Ink[source.id];stroke.Marks.transform.SetParent(patch.Root,false);stroke.Marks.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);stroke.Marks.transform.localScale=Vector3.one;stroke.Marks.SetColor(source.color);stroke.Marks.gameObject.SetActive(true);}
            }
            Reanchor();RoomAppearanceView.VisualsChanged(this);
        }
        // Detach before replacing recipe parts. Otherwise immediate destruction
        // (or deferred parent destruction) can take retained ink/windows with it.
        internal void DetachAnchors(){foreach(var p in patches.Values)if(p.Root)p.Root.SetParent(transform,true);}
        internal void Reanchor(){
            var recipe=GetComponent<RecipeObject>();
            foreach(var p in patches.Values){if(!p.Root||p.Data==null)continue;var anchor=string.IsNullOrEmpty(p.Data.part)?transform:recipe?recipe.Part(p.Data.part):null;p.Anchor=anchor;p.Root.SetParent(anchor?anchor:transform,false);p.Root.SetLocalPositionAndRotation(p.Data.position,p.Data.rotation);p.Root.localScale=Vector3.one;p.Root.gameObject.SetActive(anchor);}
        }
        internal void PreviewErasure(string id,IReadOnlyCollection<string> removed){if(patches.TryGetValue(id,out var p))foreach(var pair in p.Ink)if(pair.Value.Marks)pair.Value.Marks.gameObject.SetActive(!removed.Contains(pair.Key));}
        public Transform Surface(string id)=>patches.TryGetValue(id,out var p)&&p.Root?p.Root:null;
        public bool Hit(Ray ray,float maximum,out string id,out Vector3 local,out float distance,float radius=.003f)
        {
            GetComponent<ScannedDrawingView>()?.Sync();
            id=null;local=default;distance=maximum;
            if(!float.IsFinite(ray.origin.sqrMagnitude)||!float.IsFinite(ray.direction.sqrMagnitude)||ray.direction.sqrMagnitude<.00001f)return false;
            ray.direction=ray.direction.normalized;
            foreach(var patch in patches.Values) {
                if(!patch.Data.enabled||!patch.Root||!patch.Root.gameObject.activeInHierarchy)continue;
                var origin=patch.Root.InverseTransformPoint(ray.origin);var direction=patch.Root.InverseTransformVector(ray.direction);
                if(!DrawingSurfaceGeometry.Hit(patch.Data,origin,direction,distance,radius,out var point,out float t))continue;
                id=patch.Data.id;local=point;distance=t;
            }
            return id!=null;
        }
        void OnDestroy()=>ReleaseNativeResources();
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){foreach(var p in patches.Values)p.Release();patches.Clear();}
    }
}
