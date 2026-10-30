// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art
{
    // Detached mesh/material candidate. The receiving PencilMarks takes its
    // lease only after its exact authored input has been accepted.
    internal sealed class PencilGeometry:IDisposable
    {
        InkResources.Lease lease;Mesh mesh;Material material;PencilMarks pigmentOwner;bool ended;
        internal PencilGeometry(int pointCount,Func<IReadOnlyList<Vector3[]>> paths,float radius,RoomResourceOwner owner,string source,uint seed=71,PencilMarks reusePigment=null){
            lease=InkResources.Stroke(pointCount,owner,source);
            try{
                var points=paths();if(PencilStrokeMesh.PointCount(points,radius)>pointCount)throw new ArgumentException("Ink source changed after reservation");
                mesh=PencilStrokeMesh.Build(points,radius,seed,m=>lease.Own(m));lease.CheckMesh(mesh);
                material=reusePigment?reusePigment.ReusablePigment:null;
                if(material)pigmentOwner=reusePigment;
                else{material=IllustratedMaterials.Create(IllustratedMaterials.Ink,.08f,m=>lease.Own(m));material.SetFloat("_HasRestCoordinates",1);material.SetShaderPassEnabled(IllustratedSurface.PencilPass,false);}
            }catch{Dispose();throw;}
        }
        internal static PencilGeometry Prepare(IReadOnlyList<Vector3[]> paths,float radius,RoomResourceOwner owner,string source,uint seed=71,PencilMarks reusePigment=null)=>new(PencilStrokeMesh.PointCount(paths,radius),()=>paths,radius,owner,source,seed,reusePigment);
        internal bool Ready=>!ended&&mesh&&material;
        internal InkResources.Lease Take(PencilMarks host,out Mesh geometry,out Material pigment){
            if(!Ready||!host)throw new InvalidOperationException("Ink preparation has ended");
            if(pigmentOwner){if(pigmentOwner!=host)throw new InvalidOperationException("Ink pigment belongs to a different host");pigmentOwner.TransferPigment(material,lease);}
            lease.Ready(host);ended=true;geometry=mesh;pigment=material;return lease;
        }
        public void Dispose(){if(ended)return;ended=true;lease?.Dispose();}
    }
}
