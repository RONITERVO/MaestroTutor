// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Same mesh evaluators as native views, with no model renderer/material import.
    // Each temporary custom Unity mesh is converted to value arrays and released.
    internal static class RoomAcousticGeometry
    {
        internal static async Task<AcousticMeshInput[]> Prepare(RoomObjectData data,RecipeObject.IdleState idle,ModelLibrary models,RoomPreparationBudget budget,CancellationToken cancellation)
        {
            var result=new List<AcousticMeshInput>();int vertices=0,triangles=0;
            void Add(AcousticMeshInput input){
                cancellation.ThrowIfCancellationRequested();
                if(!RoomAcoustics.ValidPose(input.Local)||!RoomAcoustics.ValidateMeshData(input.Vertices,input.Indices,out _))throw new InvalidOperationException("Unsupported acoustic geometry");
                vertices+=input.Vertices.Length/3;triangles+=input.Indices.Length/3;
                if(result.Count>=RoomAcoustics.MaximumVirtualSurfaces||vertices>98304||triangles>49152)throw new InvalidOperationException("This object's acoustic geometry exceeds its budget");
                result.Add(input);
            }
            AcousticMeshInput Read(Mesh mesh,Matrix4x4 pose){
                if(!RoomAcoustics.ReadMesh(mesh,out var points,out var indices,out var error))throw new InvalidOperationException(error);
                return new AcousticMeshInput(points,indices,pose);
            }
            AcousticMeshInput Shape(string shape,RecipePart part,Matrix4x4 pose){
                if(RecipeGeometry.Custom(shape)){
                    var mesh=RecipeGeometry.Build(part);try{return Read(mesh,pose);}finally{ArtResources.Release(mesh);}
                }
                // Ask Unity for the exact engine primitive used by the live view.
                // The detached helper never survives a preparation unit or frame.
                var helper=GameObject.CreatePrimitive(shape=="sphere"?PrimitiveType.Sphere:shape=="cylinder"?PrimitiveType.Cylinder:PrimitiveType.Cube);
                helper.SetActive(false);
                try{return Read(helper.GetComponent<MeshFilter>().sharedMesh,pose);}finally{ArtResources.Release(helper);}
            }
            await budget.Step();cancellation.ThrowIfCancellationRequested();
            if(data.IsBuiltIn)throw new InvalidOperationException("World-owned objects do not use creation acoustic preparation");
            if(!ScanDrawingAnchor.Has(data)){
                if(data.kind==RoomObjectKind.ImportedModel){
                    if(models==null)throw new InvalidOperationException("The model library is unavailable");
                    var imported=await models.PrepareAcousticsAsync(data.modelHash,cancellation);
                    await budget.Step();
                    if(!imported.Known)throw new InvalidOperationException(imported.Issue);
                    if(imported.Meshes.Length>0){
                        if(!ModelGeometryLayout.TryCreate(imported.Resting.SourceBounds,Quaternion.identity,data.modelGeometry,out var layout,out var error))throw new InvalidOperationException(error);
                        var fit=Matrix4x4.TRS(layout.Position,Quaternion.identity,Vector3.one*layout.Factor);
                        foreach(var input in imported.Meshes){await budget.Step();Add(new AcousticMeshInput(input.Vertices,input.Indices,fit*input.Local));}
                    }
                }else if(data.kind==RoomObjectKind.Assembly){
                    var recipe=data.recipe;if(recipe==null||!recipe.Validate(out _))throw new InvalidOperationException("Invalid acoustic recipe");
                    bool remembered=idle!=null&&idle.Source==JsonUtility.ToJson(recipe);
                    var frames=new Dictionary<string,Matrix4x4>(StringComparer.Ordinal);
                    foreach(var part in recipe.parts){
                        await budget.Step();var position=part.position;var rotation=part.rotation;var scale=Vector3.one;
                        if(remembered&&idle.Parts.TryGetValue(part.id,out var pose)){position=pose.position;rotation=pose.rotation;scale=pose.scale;}
                        var node=(string.IsNullOrEmpty(part.parent)?Matrix4x4.identity:frames[part.parent])*Matrix4x4.TRS(position,rotation,scale);frames.Add(part.id,node);
                        Add(Shape(part.shape,part,node*Matrix4x4.Scale(Vector3.Scale(part.size,part.shape=="cylinder"?new Vector3(1,.5f,1):Vector3.one))));
                    }
                }else if(data.kind!=RoomObjectKind.Drawing){
                    string shape=data.kind==RoomObjectKind.Ball?"sphere":data.kind==RoomObjectKind.Cylinder?"cylinder":"box";
                    Add(Shape(shape,null,Matrix4x4.Scale(shape=="cylinder"?new Vector3(.13f,.065f,.13f):Vector3.one*.13f)));
                }
            }
            foreach(var field in data.heightFields??Array.Empty<RoomHeightField>()){
                await budget.Step();if(field==null||!field.Validate(out var error))throw new InvalidOperationException("Invalid acoustic terrain");
                var mesh=new Mesh();try{HeightFieldGeometry.Fill(mesh,field);Add(Read(mesh,Matrix4x4.TRS(field.frame.position,field.frame.rotation,Vector3.one)));}finally{ArtResources.Release(mesh);}
            }
            return result.ToArray();
        }
    }
}
