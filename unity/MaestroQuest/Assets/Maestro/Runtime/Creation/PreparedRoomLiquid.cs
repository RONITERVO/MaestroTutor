// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal sealed class PreparedRoomLiquid:IDisposable
    {
        readonly string target,encoded;readonly ContainerFillView previous;
        internal readonly LiquidResources.Lease Lease;
        readonly bool created;bool ended;
        internal static string Encode(RoomContainer[] values)=>string.Join("|",(values??Array.Empty<RoomContainer>()).Select(JsonUtility.ToJson));
        internal PreparedRoomLiquid(RoomWorldIdentity world,string identity,RoomContainer[] values,ContainerFillView view){
            target=identity??"";encoded=Encode(values);previous=view;
            if((values?.Length??0)==0)return;
            if(values.Length!=1||values[0]==null||!values[0].Validate(out _))throw new ArgumentException("Invalid liquid definition");
            Lease=view?view.ReusableReservation:null;if(Lease!=null)return;
            Lease=LiquidResources.Begin(new RoomResourceOwner(world,target,"liquid"));created=true;
            try{
                Lease.Surface=new GameObject("Measured liquid surface");Lease.Surface.SetActive(false);
                Lease.Mesh=new Mesh{name="Bounded liquid level"};Lease.Mesh.MarkDynamic();Lease.Surface.AddComponent<MeshFilter>().sharedMesh=Lease.Mesh;
                Lease.Renderer=Lease.Surface.AddComponent<MeshRenderer>();Lease.Renderer.enabled=false;
                Lease.SurfaceMaterial=IllustratedMaterials.Create(values[0].color,0);Lease.Renderer.sharedMaterial=Lease.SurfaceMaterial;Lease.Renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Lease.Renderer.receiveShadows=false;
                Lease.Stream=new GameObject("Liquid stream");Lease.Stream.SetActive(false);Lease.Line=Lease.Stream.AddComponent<LineRenderer>();Lease.Line.enabled=false;Lease.Line.useWorldSpace=true;Lease.Line.numCapVertices=2;Lease.Line.numCornerVertices=2;Lease.Line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Lease.Line.receiveShadows=false;
                Lease.StreamMaterial=IllustratedMaterials.Create(values[0].color,0);Lease.Line.sharedMaterial=Lease.StreamMaterial;Lease.Line.positionCount=LiquidResources.StreamPoints;
                Lease.Complete();
            }catch{Dispose();throw;}
        }
        internal void Validate(string identity,RoomContainer[] values){if(ended||target!=(identity??"")||encoded!=Encode(values)||Lease!=null&&!Lease.Available)throw new InvalidOperationException("Liquid source changed or preparation has ended");}
        internal LiquidResources.Lease Take(ContainerFillView view,string identity,RoomContainer[] values){Validate(identity,values);if(!view||previous&&previous!=view)throw new InvalidOperationException("Liquid owner changed after preparation");if(created)Lease.Ready(view);ended=true;return Lease;}
        public void Dispose(){if(ended)return;ended=true;if(created)Lease.Dispose();}
    }
}
