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
        internal const float MaximumGeometrySize=12.5f;
        Quaternion objectRotation=Quaternion.identity;
        bool geometryFrozen;
        internal Bounds SourceBounds=>rawBounds;
        internal string PlaybackIssue=>geometryFrozen?"Disable rigid mesh collision before playing embedded or library animation":null;
        internal bool CanApplyGeometry(RoomModelGeometry settings,out Bounds bounds,out string error)
        {
            bounds=default;error="Wait for a loaded imported model";if(!Ready)return false;
            if(settings==null||!settings.Valid){error="Invalid model scale, pivot or collision settings";return false;}
            float factor=settings.scaleMode=="source"?settings.metresPerUnit:.35f/Mathf.Max(rawBounds.size.x,rawBounds.size.y,rawBounds.size.z);
            var size=rawBounds.size*factor;
            if(!RoomRecipe.Finite(size)||Mathf.Max(size.x,size.y,size.z)>MaximumGeometrySize){error="The configured model must fit within 12.5 metres before the object's own scale; use a smaller source scale or split the environment";return false;}
            // Object orientation stays compatible with the existing GLB/VRM import.
            var x=objectRotation*(Vector3.right*size.x);var y=objectRotation*(Vector3.up*size.y);var z=objectRotation*(Vector3.forward*size.z);
            size=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            var centre=settings.pivot=="source"?objectRotation*rawBounds.center*factor:settings.pivot=="base"?Vector3.up*size.y*.5f:Vector3.zero;
            if(!RoomRecipe.Finite(centre)||centre.magnitude>MaximumGeometrySize){error="The source pivot is too far from the model; choose center or base";return false;}
            bounds=new Bounds(centre,size);
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
            Stop();float factor=settings.scaleMode=="source"?settings.metresPerUnit:.35f/Mathf.Max(rawBounds.size.x,rawBounds.size.y,rawBounds.size.z);
            instance.transform.localRotation=objectRotation;instance.transform.localScale=Vector3.one*factor;
            instance.transform.localPosition=bounds.center-objectRotation*rawBounds.center*factor;
            LocalBounds=bounds;geometryFrozen=settings.meshCollision;return true;
        }
    }
}
