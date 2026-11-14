// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Each retained patch and stroke stays owned by its accepted view. This
    // candidate owns only new roots/ink until the complete source is accepted.
    internal sealed class PreparedDrawingSurfaces:IDisposable
    {
        internal sealed class Stroke:IDisposable
        {
            internal string Key;internal PencilMarks Marks;
            public void Dispose(){if(!Marks)return;var go=Marks.gameObject;go.SetActive(false);NativeResourceLifetime.Release(go);ArtResources.Release(go);}
        }
        internal sealed class Patch
        {
            internal DrawingSurface Data;internal Transform Root,Anchor;
            internal InkResources.Lease Lease;
            internal Dictionary<string,Stroke> Ink=new();
            internal void Release(){Lease.Dispose();foreach(var stroke in Ink.Values)stroke.Dispose();Ink.Clear();}
        }
        internal sealed class Change {internal Patch Patch;internal DrawingSurface Definition;internal Dictionary<string,Stroke> Ink=new();}
        [Serializable]sealed class GeometrySource {public string shape;public float curvatureRadius,radius;public Vector3[] points;}
        readonly string target,encoded;readonly DrawingSurfaceView previous;
        readonly List<Change> changes=new();readonly List<Patch> createdPatches=new();readonly List<Stroke> createdInk=new();
        bool ended;
        internal static string Key(DrawingSurface surface,SurfaceStroke stroke)=>JsonUtility.ToJson(new GeometrySource{shape=surface.Kind,curvatureRadius=surface.curvatureRadius,radius=stroke.radius,points=stroke.points});
        internal static string Encode(DrawingSurface[] definitions)=>string.Join("|",(definitions??Array.Empty<DrawingSurface>()).Select(s=>JsonUtility.ToJson(s)));
        internal PreparedDrawingSurfaces(RoomWorldIdentity world,string identity,DrawingSurface[] definitions,DrawingSurfaceView view){
            target=identity;encoded=Encode(definitions);previous=view;var owner=new RoomResourceOwner(world,identity,"ink");
            try{
                foreach(var definition in definitions??Array.Empty<DrawingSurface>()){
                    var patch=view?view.Reusable(definition.id):null;
                    if(patch==null){
                        var lease=InkResources.Surface(owner,"surface:"+definition.id);patch=new Patch{Lease=lease};createdPatches.Add(patch);
                        var root=new GameObject("Drawing surface "+definition.id);lease.Own(root);root.SetActive(false);patch.Root=root.transform;
                    }
                    var change=new Change{Patch=patch,Definition=definition.Copy()};changes.Add(change);
                    foreach(var source in definition.strokes){
                        string key=Key(definition,source);
                        if(patch.Ink.TryGetValue(source.id,out var same)&&same.Marks&&same.Marks.GeometryReady&&same.Key==key){change.Ink.Add(source.id,same);continue;}
                        int count=DrawingSurfaceGeometry.PointCount(definition,source.points,source.radius);
                        using var geometry=new PencilGeometry(count,()=>new[]{DrawingSurfaceGeometry.Path(definition,source.points,source.radius)},source.radius,owner,"surface:"+definition.id+"/stroke:"+source.id);
                        var go=new GameObject("Ink "+source.id);go.SetActive(false);PencilMarks marks=null;
                        try{marks=go.AddComponent<PencilMarks>();marks.SetPrepared(geometry);marks.SetColor(source.color);}
                        catch{NativeResourceLifetime.Release(go);ArtResources.Release(go);throw;}
                        var stroke=new Stroke{Key=key,Marks=marks};createdInk.Add(stroke);change.Ink.Add(source.id,stroke);
                    }
                }
            }catch{Dispose();throw;}
        }
        internal Transform PlannedSurface(string id)=>!ended?changes.FirstOrDefault(c=>c.Definition.id==id)?.Patch.Root:null;
        internal void Validate(string identity,DrawingSurface[] definitions){
            if(ended||target!=identity||encoded!=Encode(definitions)||changes.Any(c=>!c.Patch.Root||c.Ink.Values.Any(s=>!s.Marks||!s.Marks.GeometryReady)))throw new InvalidOperationException("Surface source changed or preparation has ended");
        }
        internal List<Change> Take(DrawingSurfaceView view,string identity,DrawingSurface[] definitions){
            Validate(identity,definitions);if(previous&&previous!=view)throw new InvalidOperationException("Surface owner changed after preparation");
            foreach(var patch in createdPatches)patch.Lease.Ready(patch.Root.gameObject);
            ended=true;createdPatches.Clear();createdInk.Clear();return changes;
        }
        public void Dispose(){if(ended)return;ended=true;foreach(var stroke in createdInk)stroke.Dispose();foreach(var patch in createdPatches)patch.Release();createdInk.Clear();createdPatches.Clear();changes.Clear();}
    }
}
