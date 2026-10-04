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
    public sealed class DrawingSurfaceView:MonoBehaviour
    {
        sealed class Patch {public DrawingSurface Data;public Transform Root,Anchor;public string Encoded;public readonly Dictionary<string,GameObject> Ink=new();}
        readonly Dictionary<string,Patch> patches=new();
        public int StrokeCount=>patches.Values.Sum(p=>p.Data.strokes.Length);
        public void Apply(DrawingSurface[] surfaces)
        {
            var active=(surfaces??Array.Empty<DrawingSurface>()).Select(s=>s.id).ToHashSet();
            foreach(var id in patches.Keys.Where(id=>!active.Contains(id)).ToArray()){Release(patches[id]);patches.Remove(id);}
            foreach(var surface in surfaces??Array.Empty<DrawingSurface>()) {
                var recipe=GetComponent<RecipeObject>();var anchor=string.IsNullOrEmpty(surface.part)?transform:recipe?recipe.Part(surface.part):null;
                string encoded=JsonUtility.ToJson(surface);
                if(patches.TryGetValue(surface.id,out var prior)&&prior.Root&&prior.Anchor==anchor&&prior.Encoded==encoded)continue;
                if(prior!=null)Release(prior);patches.Remove(surface.id);if(!anchor)continue;
                var root=new GameObject("Drawing surface "+surface.id).transform;root.SetParent(anchor,false);root.SetLocalPositionAndRotation(surface.position,surface.rotation);
                var patch=new Patch {Data=surface.Copy(),Root=root,Anchor=anchor,Encoded=encoded};
                try {
                    foreach(var stroke in surface.strokes) {
                        var ink=new GameObject("Ink "+stroke.id).transform;ink.SetParent(root,false);var marks=ink.gameObject.AddComponent<PencilMarks>();
                        marks.SetPaths(new[]{DrawingSurfaceGeometry.Path(surface,stroke.points,stroke.radius)},stroke.radius);marks.SetColor(stroke.color);patch.Ink.Add(stroke.id,ink.gameObject);
                    }
                }catch{ArtResources.Release(root.gameObject);throw;}
                patches.Add(surface.id,patch);
            }
        }
        static void Release(Patch patch){if(patch.Root){patch.Root.gameObject.SetActive(false);ArtResources.Release(patch.Root.gameObject);}}
        internal void PreviewErasure(string id,IReadOnlyCollection<string> removed){if(patches.TryGetValue(id,out var p))foreach(var pair in p.Ink)if(pair.Value)pair.Value.SetActive(!removed.Contains(pair.Key));}
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
        void OnDestroy(){foreach(var p in patches.Values)Release(p);patches.Clear();}
    }
}
