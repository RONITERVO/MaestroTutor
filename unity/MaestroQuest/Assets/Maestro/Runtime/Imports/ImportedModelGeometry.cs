// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Imports
{
    public sealed partial class ImportedModel
    {
        internal const int MaximumCollisionMeshes=32,MaximumCollisionTriangles=50000;
        internal const float MaximumGeometrySize=ModelGeometryLayout.MaximumSize;
        Quaternion objectRotation=Quaternion.identity;
        bool geometryFrozen;
        internal Bounds SourceBounds=>rawBounds;
        internal string PlaybackIssue=>geometryFrozen?"Disable rigid mesh collision before playing embedded or library animation":null;
        internal bool CanApplyGeometry(RoomModelGeometry settings,out Bounds bounds,out string error)
        {
            bounds=default;error="Wait for a loaded imported model";if(!Ready)return false;
            if(!ModelGeometryLayout.TryCreate(rawBounds,objectRotation,settings,out var layout,out error))return false;
            bounds=layout.Bounds;
            if(settings.meshCollision&&!CollisionMeshes(out _,out error))return false;
            error=null;return true;
        }
        internal bool CollisionMeshes(out MeshFilter[] result,out string error)
        {
            result=null;error="Rigid mesh collision needs readable triangle meshes without a skin or blend shapes";
            if(!Ready)return false;var found=new List<MeshFilter>();ulong triangles=0;
            foreach(var renderer in instance.Renderers)
            {
                if(renderer is not MeshRenderer)return false;
                var filter=renderer.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;
                if(!mesh||!mesh.isReadable||mesh.blendShapeCount>0)return false;
                float determinant=(instance.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix).determinant;if(!float.IsFinite(determinant)||Mathf.Abs(determinant)<1e-12f)return false;
                for(int sub=0;sub<mesh.subMeshCount;sub++){if(mesh.GetTopology(sub)!=MeshTopology.Triangles)return false;triangles+=mesh.GetIndexCount(sub)/3;}
                found.Add(filter);
            }
            if(found.Count==0||found.Count>MaximumCollisionMeshes||triangles==0||triangles>MaximumCollisionTriangles){error="Rigid model collision supports at most 32 meshes and 50,000 triangles per object";return false;}
            result=found.ToArray();error=null;return true;
        }
        internal bool ApplyGeometry(RoomModelGeometry settings,out string error)
        {
            if(!CanApplyGeometry(settings,out var bounds,out error))return false;
            if(!ModelGeometryLayout.TryCreate(rawBounds,objectRotation,settings,out var layout,out error))return false;
            Stop();instance.transform.localRotation=objectRotation;instance.transform.localScale=Vector3.one*layout.Factor;
            instance.transform.localPosition=layout.Position;
            LocalBounds=bounds;geometryFrozen=settings.meshCollision;return true;
        }
    }
}
