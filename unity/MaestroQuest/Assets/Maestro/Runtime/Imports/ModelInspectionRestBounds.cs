// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    public sealed partial class ModelInspection
    {
        sealed partial class Inspector
        {
            sealed class RestBoundsUnavailable:Exception { internal RestBoundsUnavailable(string reason):base(reason){} }
            static void RestRequire(bool condition,string reason){if(!condition)throw new RestBoundsUnavailable(reason);}
            ModelAssetBounds InspectRestBounds()
            {
                if(motionOnly||info.IsAvatar)return ModelAssetBounds.Unknown("VRM constraints and motion-only files have no verified cold resting envelope.");
                var defaults=new Dictionary<int,float[]>();
                // Preserve source defaults explicitly, including the node override.
                // glTF requires a common target count across a mesh's primitives.
                for(int n=0;n<nodes.Count;n++)if(nodes[n]["mesh"]!=null){
                    var mesh=meshes[(int)nodes[n]["mesh"]];var primitives=(JArray)mesh["primitives"];
                    int count=primitives.Count==0?0:Array(primitives[0],"targets",128).Count;
                    Require(primitives.All(p=>Array(p,"targets",128).Count==count),"Morph target counts must match across a model's material parts.");
                    foreach(var values in new[]{mesh["weights"],nodes[n]["weights"]})if(values!=null){
                        Require(values is JArray a&&a.Count==count,"Default morph weights must match the mesh targets.");
                        foreach(var value in (JArray)values)Require(Math.Abs(Number(value))<=100,"Default morph weights exceed the supported range.");
                    }
                    if(count==0)continue;
                    var source=nodes[n]["weights"]??mesh["weights"];var weights=new float[count];
                    for(int i=0;i<count;i++)weights[i]=source==null?0:(float)source[i];defaults.Add(n,weights);
                }
                info.RestDefaults=new ModelRestDefaults(defaults);
                if(info.AssetBounds.Known)return info.AssetBounds;
                try{return DeriveRestBounds(defaults);}
                catch(RestBoundsUnavailable error){return ModelAssetBounds.Unknown(error.Message);}
            }
            ModelAssetBounds DeriveRestBounds(Dictionary<int,float[]> defaults)
            {
                var mappings=new Dictionary<int,Matrix4x4>();
                var arithmetic=new Dictionary<int,Matrix4x4>();float arithmeticReach=0;
                void Visit(int n,Matrix4x4 parent,Matrix4x4 magnitude){
                    if(mappings.ContainsKey(n))return;var node=nodes[n];
                    RestRequire(node["matrix"]==null&&node["extensions"]==null,"Matrix or extended nodes need a separate resting-envelope proof.");
                    Vector3 Vector(JToken value,Vector3 fallback)=>value is JArray a?new((float)a[0],(float)a[1],(float)a[2]):fallback;
                    var position=Vector(node["translation"],Vector3.zero);position.z=-position.z;
                    var q=node["rotation"] as JArray;var rotation=q==null?Quaternion.identity:new Quaternion(-(float)q[0],-(float)q[1],(float)q[2],(float)q[3]);
                    RestRequire(float.IsFinite(Quaternion.Dot(rotation,rotation))&&Quaternion.Dot(rotation,rotation)>1e-12f,"The resting model has a degenerate rotation.");
                    var local=Matrix4x4.TRS(position,rotation.normalized,Vector(node["scale"],Vector3.one));
                    var mapping=parent*local;magnitude*=RestAbs(local);
                    RestMatrix(mapping);mappings.Add(n,mapping);arithmetic.Add(n,magnitude);
                    foreach(var child in Array(node,"children",512))Visit((int)child,mapping,magnitude);
                }
                var scene=root["scenes"][Integer(root,"scene",0,((JArray)root["scenes"]).Count-1,0)];
                foreach(var n in Array(scene,"nodes",512))Visit((int)n,Matrix4x4.identity,Matrix4x4.identity);
                var extent=RoomSpatialExtent.Empty;
                foreach(var entry in mappings){
                    int n=entry.Key;var node=nodes[n];if(node["mesh"]==null)continue;
                    int meshIndex=(int)node["mesh"];var mesh=meshes[meshIndex];Matrix4x4[] joints=null,jointArithmetic=null;
                    if(node["skin"]!=null){
                        // UniGLTF shares mesh bindposes between node instances. Do
                        // not infer independent skin state where none exists yet.
                        RestRequire(nodes.Count(x=>(int?)x["mesh"]==meshIndex)==1,"Shared skinned mesh instances need independent native bindposes before cold bounds are known.");
                        var skin=root["skins"][(int)node["skin"]];var refs=(JArray)skin["joints"];joints=new Matrix4x4[refs.Count];jointArithmetic=new Matrix4x4[refs.Count];
                        for(int j=0;j<joints.Length;j++){
                            RestRequire(mappings.TryGetValue((int)refs[j],out var frame),"A skin joint is outside the selected scene.");
                            RestRequire(Mathf.Abs(frame.determinant)>1e-12f,"A skin joint has a singular resting frame.");
                            Matrix4x4 bind;
                            if(skin["inverseBindMatrices"]==null)bind=frame.inverse*entry.Value;
                            else {
                                var accessor=accessors[(int)skin["inverseBindMatrices"]];
                                RestRequire((int)accessor["componentType"]==5126,"Resting inverse binds require float matrices.");
                                bind=new Matrix4x4();for(int c=0;c<16;c++)bind[c]=RestValue(accessor,j,c);
                                // Coordinate conversion is S * M * S, S=diag(1,1,-1,1).
                                for(int row=0;row<4;row++)for(int col=0;col<4;col++)if((row==2)!=(col==2))bind[row,col]=-bind[row,col];
                            }
                            RestMatrix(bind);joints[j]=frame*bind;RestMatrix(joints[j]);
                            jointArithmetic[j]=arithmetic[(int)refs[j]]*RestAbs(bind);
                        }
                    }
                    defaults.TryGetValue(n,out var morphWeights);
                    foreach(var primitive in (JArray)mesh["primitives"]){
                        var attrs=(JObject)primitive["attributes"];var positions=accessors[(int)attrs["POSITION"]];
                        var targets=Array(primitive,"targets",128);var local=RoomSpatialExtent.Empty;
                        var jointAccessor=joints==null?null:accessors[(int)attrs["JOINTS_0"]];
                        var weightAccessor=joints==null?null:accessors[(int)attrs["WEIGHTS_0"]];
                        RestRequire(joints==null||!attrs.Properties().Any(p=>(p.Name.StartsWith("JOINTS_")&&p.Name!="JOINTS_0")||(p.Name.StartsWith("WEIGHTS_")&&p.Name!="WEIGHTS_0")),"Additional skin influence sets are not covered by resting bounds.");
                        for(int i=0;i<(int)positions["count"];i++){
                            var point=RestVector(positions,i);var pointReach=RestAbs(point);
                            for(int t=0;t<targets.Count;t++)if(morphWeights[t]!=0&&targets[t]["POSITION"]!=null){
                                var delta=accessors[(int)targets[t]["POSITION"]];
                                RestRequire((string)delta["type"]=="VEC3"&&(int)delta["componentType"]==5126,"Resting morph positions require float VEC3 data.");
                                var offset=RestVector(delta,i)*morphWeights[t];point+=offset;pointReach+=RestAbs(offset);
                            }
                            if(joints!=null){
                                var weights=new Vector4();float sum=0;
                                for(int c=0;c<4;c++){weights[c]=RestValue(weightAccessor,i,c,true);RestRequire(float.IsFinite(weights[c])&&weights[c]>=0,"Skin weights must be finite and nonnegative.");sum+=weights[c];}
                                RestRequire(float.IsFinite(sum)&&sum>1e-12f,"Zero or overflowing skin weights have no verified resting envelope.");
                                weights*=1/sum;var deformed=Vector3.zero;
                                for(int c=0;c<4;c++)if(weights[c]>0){int j=(int)RestValue(jointAccessor,i,c);deformed+=joints[j].MultiplyPoint3x4(point)*weights[c];}
                                point=deformed;
                                for(int c=0;c<4;c++)if(weights[c]>0){int j=(int)RestValue(jointAccessor,i,c);arithmeticReach=Mathf.Max(arithmeticReach,RestReach(jointArithmetic[j],pointReach));}
                            }
                            if(joints==null)arithmeticReach=Mathf.Max(arithmeticReach,RestReach(arithmetic[n],pointReach));
                            RestRequire(RoomRecipe.Finite(point),"Resting geometry overflows its supported range.");
                            local.Include(new Bounds(point,Vector3.zero),Matrix4x4.identity);
                        }
                        if(local.HasBounds)extent.Include(local.Bounds,joints==null?entry.Value:Matrix4x4.identity);
                    }
                }
                RestRequire(extent.Known,"Resting geometry has an invalid envelope.");
                if(extent.HasBounds){
                    RestRequire(ModelGeometryLayout.ValidSource(extent.Bounds),"The resting model has invalid source dimensions.");
                    var c=extent.Bounds.center;RestRequire(Mathf.Max(Mathf.Abs(c.x),Mathf.Abs(c.y),Mathf.Abs(c.z))<=10000,"The resting origin exceeds the supported precision range.");
                }
                // Keep fitting dimensions separate from the conservative visibility
                // margin. Absolute products retain cancelled terms and deep TRS
                // composition; observations do not treat cancellation as precision.
                float margin=arithmeticReach*.0001f;
                RestRequire(float.IsFinite(margin),"Resting arithmetic exceeds the supported precision range.");
                return new ModelAssetBounds(true,extent.HasBounds,extent.Bounds,"",margin);
            }
            static Vector3 RestAbs(Vector3 value)=>new(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
            static Matrix4x4 RestAbs(Matrix4x4 value){for(int i=0;i<16;i++)value[i]=Mathf.Abs(value[i]);return value;}
            static float RestReach(Matrix4x4 absolute,Vector3 point){var reach=absolute.MultiplyPoint3x4(point);return Mathf.Max(reach.x,reach.y,reach.z);}
            static void RestMatrix(Matrix4x4 value){
                for(int i=0;i<16;i++)RestRequire(float.IsFinite(value[i])&&Mathf.Abs(value[i])<=10000,"The resting transform exceeds the supported precision range.");
                RestRequire(value.m30==0&&value.m31==0&&value.m32==0&&value.m33==1,"Resting transforms must be affine.");
            }
            Vector3 RestVector(JToken accessor,int index)=>new(RestValue(accessor,index,0),RestValue(accessor,index,1),-RestValue(accessor,index,2));
            float RestValue(JToken accessor,int index,int component,bool weight=false){
                var view=views[(int)accessor["bufferView"]];int type=(int)accessor["componentType"],size=Size(type);
                int stride=(int?)view["byteStride"]??Components((string)accessor["type"])*size;
                int at=binary+((int?)view["byteOffset"]??0)+((int?)accessor["byteOffset"]??0)+index*stride+component*size;
                RestRequire(type is 5121 or 5123 or 5126,"Resting geometry has an unsupported component type.");
                float result=type==5126?BitConverter.ToSingle(bytes,at):type==5123?BitConverter.ToUInt16(bytes,at):bytes[at];
                return weight&&type!=5126?result/(type==5123?65535f:255f):result;
            }
        }
    }
}
