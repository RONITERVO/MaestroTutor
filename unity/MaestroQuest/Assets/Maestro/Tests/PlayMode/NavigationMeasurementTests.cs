// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Maestro.Quest.Tests {
    // Original offline corpus. This measures the actual copy/build/retirement path,
    // not Quest performance and not Recast's unobservable peak working memory.
    public sealed class NavigationMeasurementTests {
        readonly List<Object> owned=new();
        readonly List<IDisposable> releases=new();
        sealed class Case {
            internal string Name,Boundary;internal NavMeshBuildSource[] Sources;internal Bounds Bounds;internal float Radius=.25f;internal Vector3 Start,End;internal bool RequireRoute=true;
        }
        static NavMeshBuildSource Source(Mesh mesh,Vector3 offset)=>new(){shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.Translate(offset)};
        static JObject Costs(NavigationBuildInput input)=>new(){["sources"]=input.Cost.Sources,["boxes"]=input.Cost.Boxes,["meshInstances"]=input.Cost.MeshInstances,["uniqueMeshes"]=input.Cost.UniqueMeshes,["expandedTriangles"]=input.Cost.ExpandedTriangles,["uniqueTriangles"]=input.Cost.UniqueTriangles,["vertexBytes"]=input.Cost.VertexBytes,["indexBytes"]=input.Cost.IndexBytes,["auxiliaryBytes"]=input.Cost.AuxiliaryBytes,["copyBytes"]=input.Cost.CopyBytes,["voxelCells"]=input.Cost.VoxelCells,["voxelSize"]=input.VoxelSize,["expandedBoundsSize"]=new JArray(input.Bounds.size.x,input.Bounds.size.y,input.Bounds.size.z)};
        static JObject Budget(NavigationResourcePool pool){var v=pool.Totals;return new(){["maps"]=v.maps,["sources"]=v.sources,["triangles"]=v.triangles,["copyBytes"]=v.copyBytes,["cells"]=v.cells,["operations"]=pool.ActiveOperations};}
        static JArray V(Vector3 value)=>new(value.x,value.y,value.z);
        static JObject B(Bounds value)=>new(){["center"]=V(value.center),["size"]=V(value.size)};
        static JObject Settings(NavigationBuildInput input,int agent){var value=input.Settings(agent);return new(){["agentTypeID"]=value.agentTypeID,["radius"]=value.agentRadius,["height"]=value.agentHeight,["climb"]=value.agentClimb,["slope"]=value.agentSlope,["overrideVoxelSize"]=value.overrideVoxelSize,["voxelSize"]=value.voxelSize,["minRegionArea"]=value.minRegionArea,["overrideTileSize"]=value.overrideTileSize,["tileSize"]=value.tileSize};}
        static JArray SourceSnapshot(NavigationBuildInput input){
            var sources=new JArray();int index=0;
            foreach(var source in input.Sources){
                var matrix=new JArray();for(int i=0;i<16;i++)matrix.Add(source.transform[i]);
                var mesh=source.sourceObject as Mesh;var bounds=mesh?mesh.bounds:new Bounds(Vector3.zero,source.size);
                sources.Add(new JObject{["index"]=index++,["shape"]=source.shape.ToString(),["meshInstanceId"]=mesh?mesh.GetInstanceID():0,["vertexCount"]=mesh?mesh.vertexCount:0,["area"]=source.area,["matrixColumnMajor"]=matrix,["localBounds"]=B(bounds),["shapeSize"]=V(source.size)});
            }return sources;
        }
        static JObject Usability(RoomNavigationMap map,Vector3 start,Vector3 end,bool required,string name){
            var filter=new NavMeshQueryFilter{agentTypeID=map.AgentType,areaMask=NavMesh.AllAreas};
            bool first=NavMesh.SamplePosition(start,out var a,.25f,filter),second=NavMesh.SamplePosition(end,out var b,.25f,filter);
            var path=new NavMeshPath();bool calculated=first&&second&&NavMesh.CalculatePath(a.position,b.position,filter,path);
            var result=new JObject{["agentTypeID"]=map.AgentType,["required"]=required,["requestedStart"]=V(start),["requestedEnd"]=V(end),["sampleDistance"]=.25f,["startFound"]=first,["endFound"]=second,["sampledStart"]=first?V(a.position):null,["sampledEnd"]=second?V(b.position):null,["pathCalculated"]=calculated,["pathStatus"]=path.status.ToString(),["corners"]=new JArray(path.corners.Select(V)),["usable"]=calculated&&path.status==NavMeshPathStatus.PathComplete&&path.corners.Length>=2&&Vector3.Distance(a.position,b.position)>.01f};
            // The caller records the full result before asserting it.
            return result;
        }
        static void RequireUsable(JObject result,string name){if((bool)result["required"])Assert.IsTrue((bool)result["usable"],name+" did not build a usable map for its exact agent: "+result.ToString(Newtonsoft.Json.Formatting.None));}
        static void WriteReport(JArray results,bool completed,string corpus="source-corpus"){
            var report=new JObject{["version"]=3,["corpus"]=corpus,["completed"]=completed,["policyMeasured"]=NavigationResources.PolicyMeasured,["boundary"]="Offline desktop Unity Editor corpus. Actual agent-filtered sample/path proof required for usable rows; diagnostic tiny patches and warmup labelled separately. Reported NavMeshData bytes are incomplete/opaque and are NOT output-memory estimates. Not Quest performance or Recast peak allocation.",["unity"]=Application.unityVersion,["cases"]=results};
            if(corpus=="mixed-and-global-policy-envelope")report["proposedPolicy"]=new JObject{["perBuild"]=new JObject{["sources"]=768,["triangles"]=262144,["copyBytes"]=16L*1024*1024,["voxelCells"]=32L*1024*1024},["global"]=new JObject{["sources"]=3072,["triangles"]=1048576,["copyBytes"]=32L*1024*1024,["voxelCells"]=128L*1024*1024,["maps"]=8,["operations"]=2},["boundary"]="Test-injected candidate limits. This run does not enable production policy."};
            string output=Environment.GetEnvironmentVariable("MAESTRO_NAVIGATION_MEASUREMENT");if(!string.IsNullOrEmpty(output)){using var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read);using var writer=new StreamWriter(file,new UTF8Encoding(false));writer.Write(report.ToString());}
            UnityEngine.Debug.Log("MAESTRO_NAVIGATION_MEASUREMENT completed="+completed+" cases="+results.Count);
        }
        static byte[] DenseBuilding(){
            // Subdivide the original five-box building without changing its shape.
            // 60 * 28^2 = 47,040 triangles, below the existing rigid import cap.
            byte[] original=BuildingModelFixture.Create();int jsonLength=BitConverter.ToInt32(original,12),binary=20+jsonLength+8;
            var points=new List<Vector3>();var indices=new List<uint>();const int n=28;
            for(int triangle=0;triangle<60;triangle++){
                Vector3 Read(int corner){int at=binary+(triangle*3+corner)*12;return new(BitConverter.ToSingle(original,at),BitConverter.ToSingle(original,at+4),BitConverter.ToSingle(original,at+8));}
                var a=Read(0);var b=Read(1);var c=Read(2);var grid=new Dictionary<(int,int),uint>();
                for(int i=0;i<=n;i++)for(int j=0;j<=n-i;j++){grid[(i,j)]=(uint)points.Count;points.Add(a+(b-a)*((float)i/n)+(c-a)*((float)j/n));}
                for(int i=0;i<n;i++)for(int j=0;j<n-i;j++){
                    indices.AddRange(new[]{grid[(i,j)],grid[(i+1,j)],grid[(i,j+1)]});
                    if(i+j<n-1)indices.AddRange(new[]{grid[(i+1,j)],grid[(i+1,j+1)],grid[(i,j+1)]});
                }
            }
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            foreach(var point in points){writer.Write(point.x);writer.Write(point.y);writer.Write(point.z);}int indexOffset=(int)stream.Length;foreach(uint index in indices)writer.Write(index);
            var root=new JObject{["asset"]=new JObject{["version"]="2.0",["copyright"]="Original Maestro building fixture subdivision; Apache-2.0"},["scene"]=0,["scenes"]=new JArray(new JObject{["nodes"]=new JArray(0)}),["nodes"]=new JArray(new JObject{["mesh"]=0}),
                ["meshes"]=new JArray(new JObject{["primitives"]=new JArray(new JObject{["attributes"]=new JObject{["POSITION"]=0},["indices"]=1})}),
                ["accessors"]=new JArray(new JObject{["bufferView"]=0,["componentType"]=5126,["count"]=points.Count,["type"]="VEC3",["min"]=new JArray(-3,-.1,-3),["max"]=new JArray(3,3,3)},new JObject{["bufferView"]=1,["componentType"]=5125,["count"]=indices.Count,["type"]="SCALAR"}),
                ["bufferViews"]=new JArray(new JObject{["buffer"]=0,["byteOffset"]=0,["byteLength"]=indexOffset},new JObject{["buffer"]=0,["byteOffset"]=indexOffset,["byteLength"]=stream.Length-indexOffset}),["buffers"]=new JArray(new JObject{["byteLength"]=stream.Length})};
            Assert.AreEqual(47040,indices.Count/3);return ModelFixture.Pack(root,stream.ToArray());
        }
        IEnumerator Import(byte[] bytes,Action<ImportedModel> accept){
            var asset=ModelLibrary.Inspect("Original navigation measurement.glb",bytes);var go=new GameObject("Navigation measurement imported fixture");owned.Add(go);
            var model=go.AddComponent<ImportedModel>();var task=model.LoadAsync(asset);float until=Time.realtimeSinceStartup+20;
            while(!task.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(task.IsCompleted);Assert.IsNull(task.Exception);Assert.IsTrue(model.CollisionMeshes(out _,out var reason),reason);accept(model);
        }
        IEnumerator Measure(Case item,JArray rows){
            var timer=Stopwatch.StartNew();long allocation=GC.GetAllocatedBytesForCurrentThread();
            Assert.IsTrue(NavigationBuildInput.TryCapture(item.Sources,item.Bounds,item.Radius,1.7f,out var input,out var reason),reason);
            allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;timer.Stop();double projectionMs=timer.Elapsed.TotalMilliseconds;
            var pool=new NavigationResourcePool(NavigationLimits.Unmeasured,NavigationLimits.Unmeasured,2);RoomNavigationBuild build=null;RoomNavigationMap map=null;
            try{
                timer.Restart();Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,item.Name,pool,out build,out reason),reason);timer.Stop();double startMs=timer.Elapsed.TotalMilliseconds;
                var started=Budget(pool);int frames=0;timer.Restart();float deadline=Time.realtimeSinceStartup+30;
                while(!build.Done&&Time.realtimeSinceStartup<deadline){frames++;yield return null;}timer.Stop();Assert.IsTrue(build.Done,item.Name+" native completion deadline");
                map=build.Take();Assert.IsTrue(map.TryInstall(Vector3.zero,Quaternion.identity,out _));Assert.IsTrue(map.Installed);long mapBytes=Profiler.GetRuntimeMemorySizeLong(map.Data);
                var held=Budget(pool);var proof=Usability(map,item.Start,item.End,item.RequireRoute,item.Name);
                var row=new JObject{["case"]=item.Name,["boundary"]=item.Boundary,["cost"]=Costs(input),["buildBounds"]=B(input.Bounds),["sources"]=SourceSnapshot(input),["settings"]=Settings(input,map.AgentType),["usability"]=proof,["projectionMs"]=projectionMs,["projectionManagedBytes"]=allocation,["startAndCopiesMs"]=startMs,["completionWaitMs"]=timer.Elapsed.TotalMilliseconds,["completionFrames"]=frames,["reportedMapObjectBytesNotOutputMemory"]=mapBytes,["started"]=started,["completed"]=held};rows.Add(row);
                RequireUsable(proof,item.Name);map.Dispose();row["retiring"]=Budget(pool);yield return null;pool.Collect();Assert.AreEqual(0,pool.Count,item.Name+" native retirement");row["retired"]=Budget(pool);
            }finally{map?.Dispose();build?.Dispose();}
        }
        IEnumerator Overlap(Case item,JArray rows,bool proposedPolicy=false){
            Assert.IsTrue(NavigationBuildInput.TryCapture(item.Sources,item.Bounds,item.Radius,1.7f,out var input,out var reason),reason);
            var pool=proposedPolicy?ProposedPool():new NavigationResourcePool(NavigationLimits.Unmeasured,NavigationLimits.Unmeasured,2);
            RoomNavigationBuild initial=null,first=null,second=null;RoomNavigationMap held=null;
            try{
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,"held-map",pool,out initial,out reason),reason);
                float until=Time.realtimeSinceStartup+30;while(!initial.Done&&Time.realtimeSinceStartup<until)yield return null;Assert.IsTrue(initial.Done);
                held=initial.Take();Assert.IsTrue(held.TryInstall(Vector3.zero,Quaternion.identity,out _));var proof=Usability(held,item.Start,item.End,true,"overlap held map");RequireUsable(proof,"overlap held map");yield return null;pool.Collect();
                Assert.AreEqual(1,pool.Totals.maps);Assert.AreEqual(0,pool.Totals.copyBytes);
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,"overlap-a",pool,out first,out reason),reason);
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,"overlap-b",pool,out second,out reason),reason);
                var overlapSettings=Settings(input,held.AgentType);var overlapping=Budget(pool);Assert.AreEqual(3,pool.Totals.maps);Assert.LessOrEqual(pool.ActiveOperations,2);
                // Map retirement and cancellation overlap with pending native work.
                held.Dispose();first.Dispose();second.Dispose();var retiring=Budget(pool);
                until=Time.realtimeSinceStartup+30;while(pool.Count!=0&&Time.realtimeSinceStartup<until){pool.Collect();yield return null;}Assert.AreEqual(0,pool.Count);
                rows.Add(new JObject{["case"]="retained-map-plus-two-builds-"+item.Name,["costPerBuild"]=Costs(input),["usability"]=proof,["settings"]=overlapSettings,["buildBounds"]=B(input.Bounds),["sources"]=SourceSnapshot(input),["overlap"]=overlapping,["retiring"]=retiring,["retired"]=Budget(pool)});
            }finally{held?.Dispose();initial?.Dispose();first?.Dispose();second?.Dispose();}
        }
        IEnumerator MapCountSweep(JArray rows){
            var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(4,.1f,4),transform=Matrix4x4.identity};
            Assert.IsTrue(NavigationBuildInput.TryCapture(new[]{source},new Bounds(Vector3.zero,new Vector3(4,.1f,4)),.25f,1.7f,out var input,out var reason),reason);
            // Staged measurement counts, not a production map-count policy.
            var pool=new NavigationResourcePool(NavigationLimits.Unmeasured,NavigationLimits.Unmeasured,2);var maps=new List<RoomNavigationMap>();var proofs=new JArray();RoomNavigationBuild pending=null;JObject last=null;
            try{
                for(int count=1;count<=8;count++){
                    Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,"map-count-"+count,pool,out pending,out reason),reason);
                    float until=Time.realtimeSinceStartup+30;while(!pending.Done&&Time.realtimeSinceStartup<until)yield return null;Assert.IsTrue(pending.Done);
                    var map=pending.Take();pending=null;maps.Add(map);Assert.IsTrue(map.TryInstall(Vector3.zero,Quaternion.identity,out _));
                    var proof=Usability(map,new Vector3(-1,.05f,0),new Vector3(1,.05f,0),true,"map-count-"+count);proofs.Add(proof);RequireUsable(proof,"map-count-"+count);
                    if(count is 1 or 2 or 4 or 8){long bytes=0;foreach(var held in maps)bytes+=Profiler.GetRuntimeMemorySizeLong(held.Data);
                        last=new JObject{["case"]="retained-tiny-box-maps-"+count,["costPerBuild"]=Costs(input),["held"]=Budget(pool),["reportedMapObjectBytesNotOutputMemory"]=bytes,["usability"]=proofs.DeepClone(),["sources"]=SourceSnapshot(input),["buildBounds"]=B(input.Bounds),["settings"]=Settings(input,map.AgentType),["boundary"]="Independent completed/installed NavMeshData and settings; source/copy cost alone does not bound this fixed overhead"};rows.Add(last);
                    }
                }
                foreach(var map in maps)map.Dispose();last["retiring"]=Budget(pool);yield return null;pool.Collect();Assert.AreEqual(0,pool.Count);last["retired"]=Budget(pool);
            }finally{pending?.Dispose();foreach(var map in maps)map.Dispose();}
        }
        [UnityTest] public IEnumerator SourceCostAndNativeLifetimeCorpus(){
            var cases=new List<Case>();var results=new JArray();
            foreach(int count in new[]{1,64,512}){
                var sources=new NavMeshBuildSource[count];for(int i=0;i<count;i++)sources[i]=new(){shape=NavMeshBuildSourceShape.Box,size=count==1?new Vector3(4,.1f,4):new Vector3(1,.1f,1),transform=Matrix4x4.Translate(count==1?Vector3.zero:new Vector3(i%8-.5f*7,0,(i/8)%8-.5f*7))};
                cases.Add(new(){Name="boxes-"+count,Boundary="Contiguous accepted box floor; repeated coincident layers are deliberate source-work stress, not a complete authored room",Sources=sources,Bounds=new Bounds(Vector3.zero,new Vector3(count==1?4:8,.1f,count==1?4:8)),Start=new Vector3(-1,.05f,0),End=new Vector3(1,.05f,0)});
            }
            foreach(int cells in new[]{4,8,16}){
                var field=new RoomHeightField{cells=cells,width=4,depth=4,maxHeight=.5f,heights=Enumerable.Repeat(.2f,(cells+1)*(cells+1)).ToArray()};Assert.IsTrue(field.Validate(out var reason),reason);
                var mesh=new Mesh{name="Navigation terrain measurement"};owned.Add(mesh);HeightFieldGeometry.Fill(mesh,field);
                foreach(int count in new[]{1,4}){
                    var sources=Enumerable.Range(0,count).Select(i=>Source(mesh,new Vector3((i%2)*4,0,(i/2)*4))).ToArray();
                    cases.Add(new(){Name="terrain-"+cells+"-shared-"+count,Boundary="Existing validated height-field geometry; four is the unchanged room height-field cap",Sources=sources,Bounds=new Bounds(count==1?Vector3.zero:new Vector3(2,0,2),new Vector3(count==1?4:8,.5f,count==1?4:8)),Start=new Vector3(-1,.2f,-1),End=new Vector3(1,.2f,-1)});
                }
            }
            foreach(bool dense in new[]{false,true}){
                ImportedModel model=null;yield return Import(dense?DenseBuilding():BuildingModelFixture.Create(),value=>model=value);Assert.IsTrue(model.CollisionMeshes(out var filters,out var reason),reason);
                Assert.AreEqual(1,filters.Length);var mesh=filters[0].sharedMesh;
                foreach(int count in new[]{1,4})foreach(bool shared in new[]{true,false}){
                    if(count==1&&!shared)continue;
                    var sources=new NavMeshBuildSource[count];for(int i=0;i<count;i++){var selected=mesh;if(!shared){selected=Object.Instantiate(mesh);owned.Add(selected);}sources[i]=Source(selected,new Vector3((i%2)*6,0,(i/2)*6));}
                    cases.Add(new(){Name=(dense?"dense":"original")+"-building-"+(shared?"shared":"distinct")+"-"+count,Boundary="GLB passed actual inspector/importer and existing rigid collision validation; repeated source instances isolate map-copy cost from import residency",Sources=sources,Bounds=new Bounds(count==1?new Vector3(0,1.45f,0):new Vector3(3,1.45f,3),new Vector3(count==1?6:12,3.1f,count==1?6:12)),Start=new Vector3(-1,0,-1),End=new Vector3(1,0,-1)});
                }
            }
            var recipe=new CollisionRecipe{shapes=new[]{new CollisionShape{id="cylinder",shape="cylinder",segments=24,size=new Vector3(2,.2f,2)},new CollisionShape{id="ring",shape="ring",segments=24,innerRadius=.35f,size=new Vector3(2,.2f,2),position=Vector3.right}}};Assert.IsTrue(recipe.Validate(out var recipeReason),recipeReason);
            var parent=new GameObject("Navigation compound measurement");owned.Add(parent);var geometry=new CollisionGeometry(recipe,parent.transform);releases.Add(geometry);geometry.SetActive(true);Physics.SyncTransforms();
            var compound=new List<NavMeshBuildSource>();foreach(var collider in geometry.Colliders){var mesh=((MeshCollider)collider).sharedMesh;compound.Add(new(){shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=collider.transform.localToWorldMatrix});}
            cases.Add(new(){Name="compound-cylinder-ring-24",Boundary="Existing validated source-owned collision recipe, 25 convex source pieces",Sources=compound.ToArray(),Bounds=new Bounds(Vector3.right*.5f,new Vector3(3,.2f,2)),Start=new Vector3(-.3f,.1f,0),End=new Vector3(.3f,.1f,0)});
            var small=cases.First(c=>c.Name=="dense-building-distinct-4");cases.Add(new(){Name="dense-building-distinct-4-minimum-voxel",Boundary=small.Boundary,Radius=.1f,Sources=small.Sources,Bounds=small.Bounds,Start=small.Start,End=small.End});
            cases.Insert(0,new(){Name="tiny-region-diagnostic-excluded",Boundary="Original isolated0.9m patch; after radius erosion/minimum region filtering this may produce no route. Excluded from usable output/performance acceptance.",RequireRoute=false,Sources=new[]{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(.9f,.1f,.9f),transform=Matrix4x4.identity}},Bounds=new Bounds(Vector3.zero,new Vector3(.9f,.1f,.9f)),Start=new Vector3(-.05f,.05f,0),End=new Vector3(.05f,.05f,0)});
            bool completed=false;
            try{
                var warm=cases.First(c=>c.Name=="boxes-1");yield return Measure(new(){Name="warmup-box-baseline",Boundary="Initialization warmup, excluded from steady-state comparisons",Sources=warm.Sources,Bounds=warm.Bounds,Start=warm.Start,End=warm.End},results);
                foreach(var item in cases)yield return Measure(item,results);
                foreach(string name in new[]{"boxes-64","dense-building-distinct-4","dense-building-distinct-4-minimum-voxel"})for(int repeat=1;repeat<=2;repeat++){
                    var item=cases.First(c=>c.Name==name);yield return Measure(new(){Name=name+"-repeat-"+repeat,Boundary="Bounded warmed repetition; "+item.Boundary,Radius=item.Radius,Sources=item.Sources,Bounds=item.Bounds,Start=item.Start,End=item.End},results);
                }
                yield return Overlap(small,results);yield return MapCountSweep(results);completed=true;
            }finally{WriteReport(results,completed);}
        }
        static NavigationLimits ProposedBuild=>new(768,262144,16L*1024*1024,32L*1024*1024);
        static NavigationLimits ProposedGlobal=>new(3072,1048576,32L*1024*1024,128L*1024*1024);
        static NavigationResourcePool ProposedPool()=>new(ProposedBuild,ProposedGlobal,2,maximumMaps:8);
        Mesh[] SplitPreservingBuffers(Mesh original,int count){
            // Partition the imported triangles while preserving every raw vertex
            // stream, not just POSITION. This is a source-layout derivative, not
            // a claim that another GLB import or full authored room was replayed.
            var sourceIndices=original.GetTriangles(0);var positions=original.vertices;var attributes=original.GetVertexAttributes();
            using var read=Mesh.AcquireReadOnlyMeshData(original);var source=read[0];var raw=new byte[source.vertexBufferCount][];
            for(int stream=0;stream<raw.Length;stream++)raw[stream]=source.GetVertexData<byte>(stream).ToArray();
            var pieces=new Mesh[count];int triangles=sourceIndices.Length/3;
            for(int piece=0;piece<count;piece++){
                int begin=piece*triangles/count*3,end=(piece+1)*triangles/count*3;var remap=new Dictionary<int,int>();var vertices=new List<int>();var indices=new int[end-begin];
                for(int i=begin;i<end;i++){int vertex=sourceIndices[i];if(!remap.TryGetValue(vertex,out var mapped)){mapped=vertices.Count;remap.Add(vertex,mapped);vertices.Add(vertex);}indices[i-begin]=mapped;}
                var mesh=new Mesh{name="Partitioned imported navigation source "+piece};owned.Add(mesh);pieces[piece]=mesh;mesh.SetVertexBufferParams(vertices.Count,attributes);
                for(int stream=0;stream<raw.Length;stream++){int stride=source.GetVertexBufferStride(stream);var buffer=new byte[checked(vertices.Count*stride)];for(int i=0;i<vertices.Count;i++)Array.Copy(raw[stream],vertices[i]*stride,buffer,i*stride,stride);mesh.SetVertexBufferData(buffer,0,0,buffer.Length,stream,MeshUpdateFlags.DontRecalculateBounds);}
                mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt32);mesh.SetIndexBufferData(indices,0,0,indices.Length,MeshUpdateFlags.DontRecalculateBounds);mesh.subMeshCount=1;
                mesh.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length,MeshTopology.Triangles),MeshUpdateFlags.DontRecalculateBounds);
                var bounds=new Bounds(positions[vertices[0]],Vector3.zero);foreach(int index in vertices)bounds.Encapsulate(positions[index]);mesh.bounds=bounds;
            }
            Assert.AreEqual(triangles,pieces.Sum(mesh=>(long)mesh.GetIndexCount(0)/3));Assert.LessOrEqual(pieces.Length,ImportedModel.MaximumCollisionMeshes);Assert.LessOrEqual(triangles,ImportedModel.MaximumCollisionTriangles);return pieces;
        }
        IEnumerator FourHeavyOwners(Case heavy,JArray rows){
            Assert.IsTrue(NavigationBuildInput.TryCapture(heavy.Sources,heavy.Bounds,heavy.Radius,1.7f,out var input,out var reason),reason);
            var smallSource=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(4,.1f,4),transform=Matrix4x4.identity};
            Assert.IsTrue(NavigationBuildInput.TryCapture(new[]{smallSource},new Bounds(Vector3.zero,new Vector3(4,.1f,4)),.25f,1.7f,out var small,out reason),reason);
            var pool=ProposedPool();var maps=new List<RoomNavigationMap>();var active=new List<RoomNavigationBuild>();var proofs=new JArray();var row=new JObject{["case"]="four-heavy-owners-small-overlap-refusal-retry",["heavyCost"]=Costs(input),["smallCost"]=Costs(small),["usability"]=proofs};rows.Add(row);
            IEnumerator Finish(RoomNavigationBuild build,Vector3 a,Vector3 b,string name){float until=Time.realtimeSinceStartup+30;while(!build.Done&&Time.realtimeSinceStartup<until)yield return null;Assert.IsTrue(build.Done);var map=build.Take();maps.Add(map);Assert.IsTrue(map.TryInstall(Vector3.zero,Quaternion.identity,out _));var proof=Usability(map,a,b,true,name);proofs.Add(proof);RequireUsable(proof,name);}
            try{
                for(int i=0;i<4;i++){Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,"heavy-owner-"+i,pool,out var build,out reason),reason);active.Add(build);yield return Finish(build,heavy.Start,heavy.End,"heavy-owner-"+i);yield return null;pool.Collect();}
                row["fourRetained"]=Budget(pool);Assert.AreEqual(4,pool.Totals.maps);Assert.AreEqual(0,pool.Totals.copyBytes);
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(small,"small-a",pool,out var aBuild,out reason),reason);active.Add(aBuild);
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(small,"small-b",pool,out var bBuild,out reason),reason);active.Add(bBuild);row["overlap"]=Budget(pool);
                int settings=NavMesh.GetSettingsCount();Assert.AreEqual(NavigationAdmission.Rejected,RoomNavigationBuild.TryStart(input,"over-capacity",pool,out var rejected,out reason));Assert.IsNull(rejected);Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);Assert.AreEqual(settings,NavMesh.GetSettingsCount());row["refusal"]=reason;row["afterRefusal"]=Budget(pool);
                yield return Finish(aBuild,new Vector3(-1,.05f,0),new Vector3(1,.05f,0),"small-a");yield return Finish(bBuild,new Vector3(-1,.05f,0),new Vector3(1,.05f,0),"small-b");
                maps[0].Dispose();row["heavyRetiring"]=Budget(pool);Assert.AreEqual(NavigationAdmission.Rejected,RoomNavigationBuild.TryStart(input,"still-retiring",pool,out rejected,out reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);
                yield return null;pool.Collect();row["afterPhysicalRelease"]=Budget(pool);
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(input,"released-capacity-retry",pool,out var retry,out reason),reason);active.Add(retry);yield return Finish(retry,heavy.Start,heavy.End,"released-capacity-retry");row["retrySucceeded"]=Budget(pool);
                foreach(var map in maps)map.Dispose();row["retiringAll"]=Budget(pool);yield return null;pool.Collect();Assert.AreEqual(0,pool.Count);row["retired"]=Budget(pool);
            }finally{foreach(var map in maps)map.Dispose();foreach(var build in active)build.Dispose();}
        }
        [UnityTest] public IEnumerator MixedSourceAndGlobalPolicyEnvelope(){
            var results=new JArray();bool completed=false;
            try{
                ImportedModel model=null;yield return Import(DenseBuilding(),value=>model=value);Assert.IsTrue(model.CollisionMeshes(out var filters,out var reason),reason);Assert.AreEqual(1,filters.Length);
                var pieces=SplitPreservingBuffers(filters[0].sharedMesh,32);var sources=new List<NavMeshBuildSource>();
                //508 ordinary proxy pieces plus4 imported owners preserve the512
                //room piece reservation. Terrain components attach to existing owners.
                for(int i=0;i<508;i++)sources.Add(new(){shape=NavMeshBuildSourceShape.Box,size=new Vector3(.5f,.1f,.5f),transform=Matrix4x4.Translate(new Vector3(-2.75f+(i%24)*.5f,-.05f,-2.75f+(i/24)*.5f))});
                for(int owner=0;owner<4;owner++)foreach(var original in pieces){var copy=Object.Instantiate(original);owned.Add(copy);sources.Add(Source(copy,new Vector3((owner%2)*6,0,(owner/2)*6)));}
                for(int i=0;i<4;i++){var field=new RoomHeightField{cells=16,width=4,depth=4,maxHeight=.5f,heights=Enumerable.Repeat(.2f,289).ToArray()};Assert.IsTrue(field.Validate(out reason),reason);var mesh=new Mesh{name="Mixed bounded terrain"};owned.Add(mesh);HeightFieldGeometry.Fill(mesh,field);sources.Add(Source(mesh,new Vector3((i%2)*6,0,(i/2)*6)));}
                void Box(Vector3 center,Vector3 size)=>sources.Add(new(){shape=NavMeshBuildSourceShape.Box,size=size,transform=Matrix4x4.Translate(center)});
                Box(new Vector3(3,-.15f,3),new Vector3(12,.1f,12));Box(new Vector3(3,2.95f,3),new Vector3(12,.1f,12));
                Box(new Vector3(-2.95f,1.45f,3),new Vector3(.1f,3,12));Box(new Vector3(8.95f,1.45f,3),new Vector3(.1f,3,12));Box(new Vector3(3,1.45f,-2.95f),new Vector3(12,3,.1f));Box(new Vector3(3,1.45f,8.95f),new Vector3(12,3,.1f));
                for(int i=0;i<6;i++)Box(new Vector3(5.5f+(i%3)*.5f,.25f,5.5f+(i/3)*.5f),new Vector3(.3f,.5f,.3f));
                Assert.AreEqual(652,sources.Count);
                var normal=new Case{Name="mixed-652-normal-voxel",Boundary="508 ordinary boxes + four32-mesh partitions of inspected dense rigid model + four validated terrain meshes +12 synthetic scanned floor/wall/furniture boxes; direct aggregate geometry, not a saved-room or real MRUK scan replay",Sources=sources.ToArray(),Bounds=new Bounds(new Vector3(3,1.4f,3),new Vector3(12,3.2f,12)),Start=new Vector3(-1,.2f,-1),End=new Vector3(1,.2f,-1)};
                var high=new Case{Name="mixed-652-minimum-voxel",Boundary=normal.Boundary,Radius=.1f,Sources=normal.Sources,Bounds=normal.Bounds,Start=normal.Start,End=normal.End};
                Assert.IsTrue(NavigationBuildInput.TryCapture(high.Sources,high.Bounds,high.Radius,1.7f,out var projected,out reason),reason);Assert.IsTrue(ProposedBuild.Contains(projected.Cost),"Mixed fixture exceeded proposed policy: "+Costs(projected));
                var warm=new Case{Name="mixed-warmup",Boundary="Initialization warmup, excluded from steady-state comparisons",Sources=new[]{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(4,.1f,4),transform=Matrix4x4.identity}},Bounds=new Bounds(Vector3.zero,new Vector3(4,.1f,4)),Start=new Vector3(-1,.05f,0),End=new Vector3(1,.05f,0)};
                yield return Measure(warm,results);yield return Measure(normal,results);yield return Measure(high,results);yield return Measure(new(){Name=high.Name+"-repeat",Boundary=high.Boundary,Radius=high.Radius,Sources=high.Sources,Bounds=high.Bounds,Start=high.Start,End=high.End},results);
                yield return Overlap(high,results,true);yield return FourHeavyOwners(high,results);completed=true;
            }finally{WriteReport(results,completed,"mixed-and-global-policy-envelope");}
        }
        [UnityTearDown] public IEnumerator Cleanup(){foreach(var value in releases)value.Dispose();releases.Clear();foreach(var value in owned)if(value)Object.Destroy(value);owned.Clear();yield return null;yield return null;NavigationResources.Collect();CollisionResources.Collect();}
    }
}
