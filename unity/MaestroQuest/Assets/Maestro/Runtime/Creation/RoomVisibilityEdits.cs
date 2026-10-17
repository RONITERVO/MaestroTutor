// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        readonly Dictionary<string,LayerPresentation> visibleLayers=new(StringComparer.Ordinal);
        public RoomVisibilityLayer[] VisibilityLayers()=>journal.VisibilitySnapshot();
        public int VisibilityRevision(string id)=>journal.VisibilityRevision(id);
        public RoomVisibilityLayer ReadVisibility(string id)=>journal.ReadVisibility(id);
        internal string[] VisibilityMembers(string id)=>journal.VisibilityMembers(id);
        bool CanChangeVisibility(string target,out string error) {
            if(!CanEditStructures(out error))return false;
            var item=Find(target);error="Choose a current, released object outside an authoring gesture";
            if(!item||Read(target)==null||item.Grab&&item.Grab.isSelected||GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true)return false;
            error=null;return true;
        }
        internal bool CanEditVisibility(RoomVisibilityLayer definition,string id,int revision,string[] members,out string error) {
            if(!CanEditStructures(out error))return false;
            var current=ReadVisibility(id);error="The visual layer changed; read its current revision and complete member list";
            if(VisibilityRevision(id)!=revision||current==null&&revision!=0||definition==null&&current==null)return false;
            if(definition!=null&&(definition.id!=id||!definition.Validate(out error)))return false;
            var expected=current==null?Array.Empty<string>():VisibilityMembers(id);
            error="Include every bound object exactly once before changing a shared visual layer";
            if(members==null||members.Length!=members.Distinct().Count()||!expected.ToHashSet().SetEquals(members))return false;
            if(definition==null&&members.Length>0){error="Unbind this visual layer before removing it";return false;}
            foreach(string member in members)if(!CanChangeVisibility(member,out error))return false;
            if(current==null&&definition!=null&&journal.VisibilityCount>=RoomVisibilityLayer.MaximumLayers){error="A room supports at most 16 visual layers";return false;}
            error=null;return true;
        }
        internal bool PrepareVisibility(RoomVisibilityLayer definition,string id,int revision,string[] members,out VisibilityLayerEdits edits,out string error) {
            edits=null;if(!CanEditVisibility(definition,id,revision,members,out error))return false;
            edits=definition==null?new VisibilityLayerEdits{Removals=new[]{id}}:new VisibilityLayerEdits{Replacements=new[]{definition.Copy()}};
            return edits.Validate(out error);
        }
        internal bool BeginVisibility(RoomVisibilityLayer definition,string id,int revision,string[] members,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null) {
            var captured=definition?.Copy();var affected=members?.ToArray();
            return BeginPreparedEdit(()=>CanEditVisibility(captured,id,revision,affected,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{edit=null;return PrepareVisibility(captured,id,revision,affected,out var edits,out issue)&&journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out edit,out issue,visibilityEdits:edits,scope:RoomEditScope.Visual);},
                captured==null?"Visual layer removed":"Visual layer saved",out pending,out error,publicationCheckpoint);
        }
        internal bool EditVisibility(RoomVisibilityLayer definition,string id,int revision,string[] members,out string error) {
            if(!PrepareVisibility(definition,id,revision,members,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),definition==null?"Visual layer removed":"Visual layer saved",false,out error,visibilityEdits:edits,scope:RoomEditScope.Visual);
        }
        internal bool CanBindVisibility(string target,int revision,string id,int layerRevision,out string error,bool checkTargetRevision=true) {
            var definition=string.IsNullOrEmpty(id)?null:ReadVisibility(id);
            error="Read the current object and selected visual layer revisions before assigning it";
            if(checkTargetRevision&&ObjectRevision(target)!=revision||id==null||id.Length==0&&layerRevision!=0||id.Length>0&&(definition==null||VisibilityRevision(id)!=layerRevision))return false;
            return CanChangeVisibility(target,out error);
        }
        internal bool PrepareVisibilityBinding(string target,int revision,string id,int layerRevision,out RoomObjectData data,out string error) {
            data=null;if(!CanBindVisibility(target,revision,id,layerRevision,out error))return false;
            data=Read(target);data.visibilityLayer=id;return true;
        }
        internal bool BeginVisibilityBinding(string target,int revision,string id,int layerRevision,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null) {
            return BeginPreparedEdit(()=>CanBindVisibility(target,revision,id,layerRevision,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{edit=null;return PrepareVisibilityBinding(target,revision,id,layerRevision,out var data,out issue)&&journal.Prepare(new[]{data},Array.Empty<string>(),out edit,out issue,scope:RoomEditScope.Visual);},
                "Visual layer assigned",out pending,out error,publicationCheckpoint,
                // The prepared journal separately proves that *only* runtime
                // placement progressed. Authored changes still invalidate it.
                ()=>CanBindVisibility(target,revision,id,layerRevision,out var issue,checkTargetRevision:false)?null:issue);
        }
        internal bool BindVisibility(string target,int revision,string id,int layerRevision,out string error) {
            if(!PrepareVisibilityBinding(target,revision,id,layerRevision,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Visual layer assigned",false,out error,scope:RoomEditScope.Visual);
        }
        void SynchronizeVisibility(RoomDocument document) {
            SynchronizeLights(document);
            var present=document.visibilityLayers.Select(v=>v.id).ToHashSet();
            foreach(var id in visibleLayers.Keys.Where(id=>!present.Contains(id)).ToArray())visibleLayers.Remove(id);
            foreach(var definition in document.visibilityLayers) {
                if(!visibleLayers.TryGetValue(definition.id,out var state)){state=new LayerPresentation();visibleLayers.Add(definition.id,state);}
                state.Synchronize(VisibilityRevision(definition.id),definition.opacity,definition.realDepth);
            }
        }
        void ApplyVisibility(RoomObjectData data,RoomItem item,RoomDocument document,RoomEditPreparation preparation) {
            // Visual-only edits must adopt prepared anchors before their windows.
            // Ordinary object configuration may already have consumed the same plan.
            ApplySurfaces(data,item,preparation);ApplyLights(data,item,preparation);
            using var candidate=preparation?.TakeWindows(data);var windows=item.GetComponent<RoomWindowView>();var layer=data.visibilityLayer==""?null:visibleLayers[data.visibilityLayer].Visual;
            if(candidate!=null){if(!windows)windows=item.gameObject.AddComponent<RoomWindowView>();windows.Apply(data,GetComponentInParent<VirtualRoomView>(),layer,candidate);}
            else windows?.ConfigureVisibility(layer); // Missing peers never allocate during unrelated reconciliation.
            var view=item.GetComponent<RoomAppearanceView>();
            if(!view&&(data.appearanceBindings.Length>0||data.visibilityLayer!=""))view=item.gameObject.AddComponent<RoomAppearanceView>();
            if(view){view.ConfigureImages(ImageTextures,new RoomResourceOwner(WorldIdentity,data.id,"appearance"));view.ConfigureLayer(data,document.appearances,data.visibilityLayer==""?null:visibleLayers[data.visibilityLayer].Visual);}
        }
        void ReconcileVisibility(RoomEditPreparation preparation) {
            var document=journal.Snapshot();SynchronizeVisibility(document);
            foreach(var data in document.objects){var item=Find(data.id);if(item)ApplyVisibility(data,item,document,preparation);}
        }
        internal JObject ObserveVisibility(string id) {
            var p=ReadVisibility(id);return p==null?null:new JObject{["id"]=id,["revision"]=VisibilityRevision(id),["name"]=p.name,["opacity"]=p.opacity,["realDepth"]=p.realDepth,["memberCount"]=VisibilityMembers(id).Length,["temporary"]=TemporaryRoom};
        }
        internal JObject ObserveVisibilityMembers(string id,int offset) {
            if(ReadVisibility(id)==null||offset<0||offset>RoomDocument.MaximumObjects+2)return null;var members=VisibilityMembers(id);
            return new JObject{["id"]=id,["revision"]=VisibilityRevision(id),["offset"]=offset,["total"]=members.Length,["pageSize"]=16,["members"]=new JArray(members.Skip(offset).Take(16))};
        }
        internal JObject ObserveVisibilityLayers(int offset) {
            if(offset<0||offset>RoomVisibilityLayer.MaximumLayers)return null;var layers=VisibilityLayers();
            return new JObject{["offset"]=offset,["total"]=layers.Length,["pageSize"]=3,["entries"]=new JArray(layers.Skip(offset).Take(3).Select(p=>new JObject{["id"]=p.id,["revision"]=VisibilityRevision(p.id),["name"]=p.name}))};
        }
        internal JObject ObserveVisibilityBinding(string target) {
            var data=Read(target);if(data==null)return null;var p=ReadVisibility(data.visibilityLayer);
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["layerId"]=data.visibilityLayer,["layerRevision"]=VisibilityRevision(data.visibilityLayer),["opacity"]=p?.opacity??1,["realDepth"]=p?.realDepth??true,["temporary"]=TemporaryRoom};
        }
    }
}
