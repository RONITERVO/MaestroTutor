// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class SpatialSculpting {
        public bool PackingEnabled {get;private set;}
        public float PackingRadius {get;private set;}=.12f;
        public double PackingLitres {get;private set;}=.25;
        public float PackingMass {get;private set;}=.15f;
        JObject packingArgs;
        Vector3 packingPosition,packingScale;
        Quaternion packingRotation;
        double packingAmount;
        GameObject packingPreview;Material packingPaint;
        string lastPackSession="",lastPackedObject="";
        double lastPackedAmount;
        readonly Collider[] packingOverlaps=new Collider[64];
        bool Packing=>packingArgs!=null;
        internal bool PackingDraft=>Busy&&Packing;
        internal bool CanGripWhilePacking(RoomItem item)=>PackingEnabled&&item&&item.GetComponent<HeightFieldView>()?.Accepted==null;
        internal bool ConfigurePacking(bool enabled,float size,double litres,float mass,out string error){
            if(!CanConfigure(out error))return false;
            if(!float.IsFinite(size)||size<.005f||size>2||!double.IsFinite(litres)||litres<.001||litres>20||!float.IsFinite(mass)||mass<.05f||mass>20){error="Choose radius 0.005–2 m, 0.001–20 litres and mass 0.05–20 kg";return false;}
            if(enabled){if(!Editor.ConfigureDrawing("off",Editor.Paint,Editor.DrawingRadius,out error))return false;Editor.SuspendConstructionPicking();Mode="off";}
            PackingEnabled=enabled;PackingRadius=size;PackingLitres=litres;PackingMass=mass;fingerBlocked[0]=fingerBlocked[1]=true;
            Editor.ReportStatus(enabled?"Pack material: touch a surface then lift, or hold/release trigger near it. One ball per contact.":"Packing tool put away");return true;
        }
        void BeginPacking(int id,string at,Vector2 point){
            if(!Editor||Busy||Editor.DrawingInProgress||Editor.Ownership.Suspended||Editor.WriteGate.Frozen)return;
            if(!Editor.CanEditObject(at,true,out var error)){Editor.ReportStatus(error);return;}
            write=Editor.WriteGate.TryWrite(out error);if(write==null){Editor.ReportStatus(error);return;}
            SessionId=Guid.NewGuid().ToString("N");target=at;tool=null;mode="pack";radius=PackingRadius;height=0;role=RoomActorRole.Control;roomSession=Editor.TemporarySessionId;
            source=Editor.Read(at)?.heightFields?.FirstOrDefault()?.Copy();if(source==null){Clear();return;}before=JsonUtility.ToJson(source);
            if(!Editor.Ownership.TryAcquire("sculpt:"+SessionId,"Your material packing tool",role,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},_=>Finish(false),out lease,out error,preservePlacement:true)||!Unchanged(out error)){Editor.ReportStatus(error);Clear();return;}
            var item=Editor.Find(at);var view=item.GetComponent<HeightFieldView>();
            packingPosition=item.transform.position;packingRotation=item.transform.rotation;packingScale=item.transform.lossyScale;
            // Physical contact/clearance use world space; the shared evaluator saves room-local poses.
            var position=view.Surface.TransformPoint(new Vector3(point.x,source.HeightAt(point),point.y));
            packingArgs=new JObject{["source"]=new JObject{["target"]=at,["revision"]=Editor.ObjectRevision(at),["centre"]=new JObject{["x"]=point.x,["z"]=point.y},["radius"]=radius},["amountLitres"]=PackingLitres,["mass"]=PackingMass,["name"]="Packed "+source.material,["position"]=JObject.Parse(JsonUtility.ToJson(Editor.transform.InverseTransformPoint(position)))};
            if(!Editor.PrepareMaterialPack(packingArgs,out var field,out var ball,out packingAmount,out error)){Editor.ReportStatus(error);Clear();return;}
            float ballRadius=ball.recipe.parts[0].size.x/2;
            position+=view.Surface.up*(ballRadius+.025f);packingArgs["position"]=JObject.Parse(JsonUtility.ToJson(Editor.transform.InverseTransformPoint(position)));
            if(!Editor.PrepareMaterialPack(packingArgs,out _,out _,out _,out error)||!PackingSpaceClear(position,ballRadius,out error)){Editor.ReportStatus(error);Clear();return;}
            draft=field.heightFields[0].Copy();owner=id;points.Clear();points.Add(point);errorText="";
            packingPreview=GameObject.CreatePrimitive(PrimitiveType.Sphere);packingPreview.name="Material packing preview (unsaved)";
            var collider=packingPreview.GetComponent<Collider>();collider.enabled=false;ArtResources.Release(collider);
            packingPreview.transform.SetParent(transform,false);packingPreview.transform.position=position;packingPreview.transform.localScale=Vector3.one*(ballRadius*2);
            packingPaint=IllustratedMaterials.Create(source.color,0);packingPreview.GetComponent<Renderer>().sharedMaterial=packingPaint;
            Editor.Find(target)?.GetComponent<HeightFieldView>()?.Preview(draft);
            Editor.ReportStatus("Ball preview — lift or release to save; then grab the ball normally");
        }
        bool PackingSpaceClear(Vector3 position,float size,out string error){
            Physics.SyncTransforms();int count=Physics.OverlapSphereNonAlloc(position,size,packingOverlaps,~0,QueryTriggerInteraction.Ignore);
            error=count>0?"The packed ball needs clear space above the surface; move the obstruction or choose a smaller amount":null;
            Array.Clear(packingOverlaps,0,packingOverlaps.Length);return error==null;
        }
        static bool SamePackingRotation(Quaternion a,Quaternion b){var av=new Vector4(a.x,a.y,a.z,a.w);var bv=new Vector4(b.x,b.y,b.z,b.w);return (av-bv).sqrMagnitude<1e-10f||(av+bv).sqrMagnitude<1e-10f;}
        bool PackingUnchanged(out string error){
            error=null;if(!Packing)return true;var item=Editor.Find(target);
            if(!item||(item.transform.position-packingPosition).sqrMagnitude>1e-10f||!SamePackingRotation(item.transform.rotation,packingRotation)||(item.transform.lossyScale-packingScale).sqrMagnitude>1e-10f){error="The packing surface moved; discard this draft and touch it again";return false;}return true;
        }
        bool PreparePacking(out string error){
            var args=(JObject)packingArgs.DeepClone();args["source"]["revision"]=Editor.ObjectRevision(target);
            if(!Editor.PrepareMaterialPack(args,out var field,out var ball,out var amount,out error))return false;
            if(amount!=packingAmount||!field.heightFields[0].heights.SequenceEqual(draft.heights)){error="The packing result changed; discard this draft";return false;}
            return PackingSpaceClear(Editor.transform.TransformPoint(ball.position),ball.recipe.parts[0].size.x/2,out error);
        }
        bool CommitPacking(out int changed,out string error){
            changed=0;if(!PreparePacking(out error))return false;
            var args=(JObject)packingArgs.DeepClone();args["source"]["revision"]=Editor.ObjectRevision(target);
            if(!Editor.PackMaterial(args,out var id,out var amount,out error))return false;
            lastPackSession=SessionId;lastPackedObject=id;lastPackedAmount=amount;
            changed=source.heights.Zip(draft.heights,(a,b)=>a!=b?1:0).Sum();return true;
        }
        void ClearPacking(){
            if(packingPreview){packingPreview.SetActive(false);ArtResources.Release(packingPreview);}packingPreview=null;ArtResources.Release(packingPaint);packingPaint=null;
            packingArgs=null;packingAmount=0;
        }
        internal JObject ObservePacking()=>new(){["sessionId"]=SessionId,["phase"]=Packing?(Active?"contact":"unsaved"):"idle",["field"]=Packing?target:"",["requestedLitres"]=Packing?(double)packingArgs["amountLitres"]:0,["amountLitres"]=packingAmount,["position"]=Packing?packingArgs["position"].DeepClone():new JObject{["x"]=0,["y"]=0,["z"]=0},["lastSaved"]=new JObject{["sessionId"]=lastPackSession,["objectId"]=lastPackedObject,["amountLitres"]=lastPackedAmount},["error"]=Maestro.Quest.Imports.ImportObservation.Text(Packing?errorText:"")};
    }
}
