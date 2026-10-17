// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Creation
{
    // Private preparation never creates a RoomWindowView or requests passthrough.
    // Only new native allocations belong to the candidate; accepted masks and their
    // shared bundle remain unchanged until the source-exact transfer succeeds.
    internal sealed class PreparedRoomWindows:IDisposable
    {
        internal sealed class Bundle:IDisposable
        {
            internal WindowResources.Lease Lease;internal Mesh Mesh;internal Material Material;
            internal bool Live=>Lease.Live&&Mesh&&Material;
            internal Bundle(RoomResourceOwner owner){
                Lease=WindowResources.Bundle(owner);
                try{
                    var source=Resources.Load<Material>("PassthroughWindow");var shader=source?source.shader:Shader.Find("Maestro/PassthroughWindow");if(!shader)throw new InvalidOperationException("Missing passthrough window shader");
                    Material=source?new Material(source):new Material(shader);Lease.Own(Material);Material.name="Room passthrough windows";Material.enableInstancing=true;
                    Mesh=new Mesh();Lease.Own(Mesh);Mesh.name="Passthrough plane";
                    Mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};Mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};Mesh.triangles=new[]{0,1,2,0,2,3};Mesh.RecalculateBounds();
                }catch{Dispose();throw;}
            }
            public void Dispose()=>Lease.Dispose();
        }
        internal sealed class Mask:IDisposable
        {
            internal WindowResources.Lease Lease;internal GameObject Root;internal MeshRenderer Renderer;internal MeshFilter Filter;internal RoomWindow Data;internal MaterialPropertyBlock Properties;
            internal bool Live(Bundle bundle)=>Lease.Live&&Root&&Renderer&&Filter&&bundle!=null&&bundle.Live&&Renderer.sharedMaterial==bundle.Material&&Filter.sharedMesh==bundle.Mesh;
            internal Mask(RoomResourceOwner owner,string id,Bundle bundle){
                Lease=WindowResources.Mask(owner,id);
                try{
                    Root=new GameObject("Passthrough "+id);Lease.Own(Root);Root.SetActive(false);
                    Filter=Root.AddComponent<MeshFilter>();Filter.sharedMesh=bundle.Mesh;Renderer=Root.AddComponent<MeshRenderer>();Renderer.enabled=false;Renderer.sharedMaterial=bundle.Material;
                    Renderer.shadowCastingMode=ShadowCastingMode.Off;Renderer.receiveShadows=false;Renderer.lightProbeUsage=LightProbeUsage.Off;Renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    Properties=new MaterialPropertyBlock();Properties.SetFloat("_Reveal",0);Properties.SetFloat("_Ellipse",0);Renderer.SetPropertyBlock(Properties);
                }catch{Dispose();throw;}
            }
            public void Dispose()=>Lease.Dispose();
        }
        internal sealed class Change {internal Mask Mask;internal RoomWindow Data;internal Transform Anchor;internal Vector3 Scale;}
        [Serializable]sealed class AnchorSource {public string id,part,shape;public Vector3 position;public Quaternion rotation;public float width,height;}
        readonly string target,encoded;readonly RoomWindowView previous;
        internal readonly Bundle Geometry;
        readonly bool createdBundle;readonly List<Mask> created=new();readonly List<Change> changes=new();bool ended;
        internal static string Encode(RoomObjectData data)=>string.Join("|",data.windows.Select(w=>JsonUtility.ToJson(w)))+"#"+string.Join("|",data.windows.Select(w=>{var s=data.surfaces.First(s=>s.id==w.surface&&s.Kind=="plane");return JsonUtility.ToJson(new AnchorSource{id=s.id,part=s.part,shape=s.Kind,position=s.position,rotation=s.rotation,width=s.width,height=s.height});}));
        internal PreparedRoomWindows(RoomWorldIdentity world,RoomObjectData data,RoomWindowView view,PreparedDrawingSurfaces surfaces=null){
            target=data.id;encoded=Encode(data);previous=view;var owner=new RoomResourceOwner(world,target,"window");
            try{
                if(data.windows.Length==0)return;
                // Check every planned anchor before reserving masks. New surface roots
                // can still be detached; they are adopted before the window transfer.
                foreach(var definition in data.windows){
                    var anchor=surfaces!=null?surfaces.PlannedSurface(definition.surface):view?view.GetComponent<DrawingSurfaceView>()?.Surface(definition.surface):null;
                    if(!anchor)throw new InvalidOperationException("The prepared window surface is unavailable");
                    var surface=data.surfaces.First(s=>s.id==definition.surface);changes.Add(new Change{Data=definition.Copy(),Anchor=anchor,Scale=new Vector3(surface.width,surface.height,1)});
                }
                Geometry=view?view.ReusableBundle:null;if(Geometry==null){Geometry=new Bundle(owner);createdBundle=true;}
                foreach(var change in changes){
                    var mask=view?view.Reusable(change.Data.id,Geometry):null;
                    if(mask==null){mask=new Mask(owner,change.Data.id,Geometry);created.Add(mask);}
                    change.Mask=mask;
                }
            }catch{Dispose();throw;}
        }
        internal void Validate(RoomObjectData data){
            if(ended||data.id!=target||encoded!=Encode(data)||changes.Any(c=>!c.Anchor||!c.Mask.Live(Geometry))||Geometry!=null&&!Geometry.Live)throw new InvalidOperationException("Window source changed or preparation has ended");
        }
        internal List<Change> Take(RoomWindowView view,RoomObjectData data){
            Validate(data);if(previous&&previous!=view||!view||changes.Any(c=>!c.Anchor.IsChildOf(view.transform)))throw new InvalidOperationException("Window owner or surface changed after preparation");
            if(createdBundle)Geometry.Lease.Ready(view);foreach(var mask in created)mask.Lease.Ready(mask.Root);
            ended=true;created.Clear();return changes;
        }
        public void Dispose(){if(ended)return;ended=true;foreach(var mask in created)mask.Dispose();created.Clear();if(createdBundle)Geometry.Dispose();changes.Clear();}
    }
}
