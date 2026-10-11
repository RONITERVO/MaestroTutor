// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
namespace Maestro.Quest.Creation {
    [Serializable] public sealed class ConstructionManipulationView {public string stateId,error;public bool visible,holding;}
    public sealed partial class RoomEditor {
        public ConstructionManipulationView ObserveConstructionManipulation(){var tool=GetComponent<ConstructionManipulator>();return new(){stateId=constructionSelectionId,visible=tool&&tool.Visible,holding=tool&&tool.Holding,error=tool?tool.Error:""};}
        internal bool CanShowConstructionManipulator(string stateId,string[] members,bool visible,out string error){
            error="Read the current construction selection before showing its move handle";
            if(stateId!=constructionSelectionId||members==null||!members.SequenceEqual(constructionMembers))return false;
            if(GetComponent<ConstructionManipulator>()?.Holding==true){error="Release the construction handle first";return false;}
            if(RuntimeGate.Held||Ownership.Suspended||WriteGate.Frozen){error="Room interaction is paused";return false;}
            if(!visible){error=null;return true;}
            if(collectingConstruction){error="Finish collecting pieces before moving them together";return false;}
            return GroupSources(members.Select(id=>new TransformMember {target=id,revision=ObjectRevision(id)}).ToArray(),out _,out error);
        }
        internal bool ShowConstructionManipulator(string stateId,string[] members,bool visible,out string error){
            if(!CanShowConstructionManipulator(stateId,members,visible,out error))return false;
            var tool=GetComponent<ConstructionManipulator>();if(!tool&&visible){tool=gameObject.AddComponent<ConstructionManipulator>();tool.Initialize(this,room);}
            if(tool){if(visible)tool.Show(stateId,members);else tool.Close();}
            SetStatus(visible?"Grip the construction handle to arrange all pieces; release saves one Undo":"Construction handle hidden");return true;
        }
    }
    /// <summary>Transient arrangement preview; release executes the same native transform as a program.</summary>
    [DefaultExecutionOrder(200)] public sealed class ConstructionManipulator:MonoBehaviour,IXRSelectFilter {
        RoomEditor editor;RoomInteraction room;Material material;TextMesh label;string selectionId;string[] ids=Array.Empty<string>();
        RoomLayout before,preview;RoomGroupTransform request;RoomOwnership.Lease owner;bool busy;readonly Vector3 offset=new(0,.24f,0);
        Vector3 lastPosition,lastScale;Quaternion lastRotation;
        ConstructionSnapPreview snapping;GameObject snapMarker;
        internal RoomSnapPlacement SnapPreview=>snapping?.Request;
        public RoomItem Handle {get;private set;}
        public bool Visible {get;private set;}
        public bool Holding=>before!=null;
        public string Error {get;private set;}="";
        public bool canProcess=>isActiveAndEnabled;
        public bool Process(IXRSelectInteractor interactor,IXRSelectInteractable interactable)=>Visible&&!busy&&editor&&!editor.RuntimeGate.Held&&!editor.Ownership.Suspended&&!editor.WriteGate.Frozen;
        internal void Initialize(RoomEditor source,RoomInteraction interaction){
            editor=source;room=interaction;var root=new GameObject("Construction move handle");root.transform.SetParent(transform,false);
            material=IllustratedMaterials.Create(IllustratedMaterials.Hex("2B8D88"));
            foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward}){
                var shape=GameObject.CreatePrimitive(PrimitiveType.Cube);shape.transform.SetParent(root.transform,false);shape.transform.localScale=Vector3.one*.025f+axis*.09f;shape.GetComponent<Renderer>().sharedMaterial=material;shape.GetComponent<Collider>().enabled=false;ArtResources.Release(shape.GetComponent<Collider>());
            }
            var collider=root.AddComponent<BoxCollider>();collider.size=Vector3.one*.14f;
            var text=new GameObject("Handle marking",typeof(TextMesh));text.transform.SetParent(root.transform,false);text.transform.localPosition=new Vector3(0,.1f,0);label=text.GetComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=48;label.characterSize=.006f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.text="Move pieces\nGrip · two hands resize";label.color=IllustratedMaterials.TextColor(IllustratedMaterials.Ink);text.GetComponent<MeshRenderer>().sharedMaterial=IllustratedMaterials.TextMaterial(label.font);
            Handle=root.AddComponent<RoomItem>();Handle.Configure(new[]{collider},.1f,4);var resizing=root.GetComponent<XRGeneralGrabTransformer>();resizing.scaleMultiplier=1;resizing.thresholdMoveRatioForScale=.02f;Handle.ConfigureWrites(editor.WriteGate);Handle.Grab.selectFilters.Add(this);Handle.Grab.firstSelectEntered.AddListener(Grab);Handle.Grab.lastSelectExited.AddListener(Release);room.Register(Handle);
            editor.Changed+=Changed;editor.Editing+=Close;editor.RuntimeGate.Changed+=GateChanged;if(editor.PhysicsWorld)editor.PhysicsWorld.Changed+=GateChanged;
            snapMarker=new GameObject("Compatible snap point");snapMarker.transform.SetParent(transform,false);
            foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward}){var mark=GameObject.CreatePrimitive(PrimitiveType.Cube);mark.transform.SetParent(snapMarker.transform,false);mark.transform.localScale=Vector3.one*.005f+axis*.025f;mark.GetComponent<Renderer>().sharedMaterial=material;mark.GetComponent<Collider>().enabled=false;ArtResources.Release(mark.GetComponent<Collider>());}
            snapMarker.SetActive(false);root.SetActive(false);
        }
        internal void Show(string stateId,string[] members){selectionId=stateId;ids=(string[])members.Clone();Error="";Visible=true;Handle.gameObject.SetActive(true);Follow();}
        void Report(string error){Error=(error??"");if(Error.Length>240)Error=Error.Substring(0,240);editor?.ReportStatus(Error);}
        void Follow(){if(!Visible||Holding||!Handle||ids.Length==0)return;var first=editor.Find(ids[0]);if(!first){Close();return;}Handle.transform.SetLocalPositionAndRotation(first.transform.localPosition+first.transform.localRotation*offset,first.transform.localRotation);Handle.transform.localScale=Vector3.one;Handle.SetHome(Handle.transform.localPosition,Handle.transform.localRotation,Vector3.one);}
        void Changed(){
            if(busy||!Visible)return;var selection=editor.ObserveConstructionSelection();
            if(selection.stateId!=selectionId||selection.collecting||editor.DrawingMode||editor.SculptMode||editor.DrawingInProgress||ids.Any(id=>!editor.Find(id))||Holding&&request.members.Any(m=>editor.ObjectRevision(m.target)!=m.revision)){Close();return;}
            if(Holding)snapping?.Refresh();Follow();
        }
        void GateChanged(){if(editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen||editor.PhysicsWorld&&editor.PhysicsWorld.Running)Close();}
        void Grab(SelectEnterEventArgs args){
            if(busy||Holding)return;
            var members=ids.Select(id=>new TransformMember {target=id,revision=editor.ObjectRevision(id)}).ToArray();
            if(!editor.GroupSources(members,out var source,out var error)){Close();Report(error);return;}
            var claims=ids.Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
            if(!editor.Ownership.TryAcquire("construction-handle","Your construction grip",RoomActorRole.Grab,claims,_=>Close(),out owner,out error,preservePlacement:true)){Close();Report(error);return;}
            busy=true;
            try{
                before=source;preview=new RoomLayout {placements=source.placements.Select(p=>new ObjectPlacement {target=p.target}).ToArray()};
                snapping=new ConstructionSnapPreview(editor,members,source,editor.ObserveConstructionSnapping());
                request=new RoomGroupTransform {members=members,position=source.placements[0].position,rotation=source.placements[0].rotation,scale=1};
                lastPosition=Handle.transform.localPosition;lastRotation=Handle.transform.localRotation;lastScale=Handle.transform.localScale;
                foreach(var id in ids){var item=editor.Find(id);item.GetComponent<RigidRoomItem>()?.SetAnimationOwner(this,true);item.Grab.enabled=false;}
                Error="";label.text="Release to place\nTwo hands resize";
            }finally{busy=false;}
            editor.ReportStatus("Arranging selected pieces — release the handle to save");
        }
        void Release(SelectExitEventArgs args){if(!busy&&Holding)End(!args.isCanceled,false);}
        internal bool Preview(out string error){
            error="There is no held construction";if(!Holding||!Handle)return false;
            request.rotation=Handle.transform.localRotation.normalized;request.scale=Handle.transform.localScale.x;request.position=Handle.transform.localPosition-request.rotation*(offset*request.scale);
            if(!request.Project(before,preview,out error)){Handle.transform.SetLocalPositionAndRotation(lastPosition,lastRotation);Handle.transform.localScale=lastScale;return false;}
            foreach(var p in preview.placements){var item=editor.Find(p.target);if(!item){error="A construction member is unavailable";return false;}}
            var result=snapping.Update(preview,request.scale)?snapping.Layout:preview;
            snapMarker.SetActive(snapping.Request!=null);
            if(snapping.Request!=null){snapMarker.transform.localPosition=snapping.Marker;label.text=snapping.Marking;}else label.text="Release to place\nTwo hands resize";
            foreach(var p in result.placements){var item=editor.Find(p.target);item.transform.SetLocalPositionAndRotation(p.position,p.rotation);item.transform.localScale=Vector3.one*p.scale;item.GetComponent<RigidRoomItem>()?.Teleported();}
            lastPosition=Handle.transform.localPosition;lastRotation=Handle.transform.localRotation;lastScale=Handle.transform.localScale;error=null;return true;
        }
        void LateUpdate(){if(!Visible||busy)return;if(editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen||editor.PhysicsWorld&&editor.PhysicsWorld.Running){Close();return;}if(Holding){if(owner?.Held!=true){Close();return;}Preview(out _);}else Follow();}
        void End(bool commit,bool close){
            if(busy)return;busy=true;string status=null;bool failed=false;
            try{
                var seenSnap=snapping?.Request;string seenSource=seenSnap?.members[0].target,seenPoint=seenSnap?.point,seenTarget=seenSnap?.destination.target,seenDestinationPoint=seenSnap?.destination.point;int seenRevision=seenSnap?.destination.revision??0;
                bool valid=Holding;if(commit&&valid){valid=Preview(out status);if(!valid)valid=Preview(out status);if(!valid)failed=true;}
                var proposed=request;var proposedSnap=snapping?.Request;
                if(commit&&valid&&seenSnap!=null&&(proposedSnap==null||proposedSnap.members[0].target!=seenSource||proposedSnap.point!=seenPoint||proposedSnap.destination.target!=seenTarget||proposedSnap.destination.point!=seenDestinationPoint||proposedSnap.destination.revision!=seenRevision)){valid=false;failed=true;status="Snap target changed before release; all pieces restored";}
                if(before!=null)foreach(var p in before.placements){var item=editor.Find(p.target);if(item&&editor.ObjectRevision(p.target)==request.members.First(m=>m.target==p.target).revision){item.transform.SetLocalPositionAndRotation(p.position,p.rotation);item.transform.localScale=Vector3.one*p.scale;item.GetComponent<RigidRoomItem>()?.Teleported();}}
                foreach(string id in ids){var item=editor.Find(id);if(item){item.GetComponent<RigidRoomItem>()?.SetAnimationOwner(this,false);item.Grab.enabled=true;}}
                before=null;preview=null;request=null;snapping=null;if(snapMarker)snapMarker.SetActive(false);owner?.Dispose();owner=null;
                if(commit&&valid){bool saved=proposedSnap!=null?SnapConstructionCapability.RunManual(editor,proposedSnap,out status):GroupTransformCapability.RunManual(editor,proposed,out status);if(!saved){failed=true;status="Move not saved; all pieces restored. "+status;}else Error="";}
                if(close){Visible=false;if(Handle)Handle.gameObject.SetActive(false);}
                if(label)label.text="Move pieces\nGrip · two hands resize";
            }finally{busy=false;}
            if(status!=null){if(failed)Report(status);else editor?.ReportStatus(status);}else if(editor)editor.ReportStatus(close?"Construction preview cancelled; starting placement restored":"Construction placed");
            Follow();
        }
        public void Close(){if(busy)return;if(Holding)End(false,true);else{Visible=false;if(Handle)Handle.gameObject.SetActive(false);}}
        void OnDisable()=>Close();
        void OnDestroy(){Close();if(editor){editor.Changed-=Changed;editor.Editing-=Close;editor.RuntimeGate.Changed-=GateChanged;if(editor.PhysicsWorld)editor.PhysicsWorld.Changed-=GateChanged;}if(room&&Handle)room.Unregister(Handle);if(Handle)ArtResources.Release(Handle.gameObject);if(snapMarker)ArtResources.Release(snapMarker);if(material)ArtResources.Release(material);}
    }
}
