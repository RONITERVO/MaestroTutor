// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        bool CurrentSettings(string id,int revision,bool creationOnly,out string error)
        {
            if(!CanEditObject(id,creationOnly,out error))return false;
            error="Object settings changed; inspect their current revision before editing";
            if(ObjectRevision(id)!=revision)return false;error=null;return true;
        }
        internal bool CanConfigurePhysics(string id,int revision,ObjectPhysicsSettings settings,out string error)=>
            CurrentSettings(id,revision,true,out error)&&RoomControls.SetPhysics(Read(id),settings,out error);
        internal bool ConfigurePhysics(string id,int revision,ObjectPhysicsSettings settings,out string error)
        {
            if(!CanConfigurePhysics(id,revision,settings,out error))return false;
            return EditObject(id,true,data=>RoomControls.SetPhysics(data,settings,out _),"Physics settings saved",false,out error);
        }
        internal bool CanConfigureMovement(int revision,AvatarMovementSettings settings,out string error)=>
            CurrentSettings("maestro",revision,false,out error)&&RoomControls.SetMovement(Read("maestro"),settings,out error);
        internal bool ConfigureMovement(int revision,AvatarMovementSettings settings,out string error)
        {
            if(!CanConfigureMovement(revision,settings,out error))return false;
            return EditObject("maestro",false,data=>RoomControls.SetMovement(data,settings,out _),"Maestro movement preferences saved",false,out error);
        }
        internal bool CanConfigureWalk(int revision,string source,string motionId,string modelHash,int clipIndex,out string error)
        {
            if(!CurrentSettings("maestro",revision,false,out error))return false;
            var data=Read("maestro");
            return source=="embedded"?AvatarWalkSelection.ApplyEmbedded(this,data,modelHash,clipIndex,out error):AvatarWalkSelection.Apply(this,data,source=="included"?"":motionId,out error);
        }
        internal bool ConfigureWalk(int revision,string source,string motionId,string modelHash,int clipIndex,out string error)
        {
            if(!CanConfigureWalk(revision,source,motionId,modelHash,clipIndex,out error))return false;
            return EditObject("maestro",false,data=>{
                if(source=="embedded")AvatarWalkSelection.ApplyEmbedded(this,data,modelHash,clipIndex,out _);
                else AvatarWalkSelection.Apply(this,data,source=="included"?"":motionId,out _);
            },"Walking animation saved",false,out error);
        }
        internal JObject ObservePhysicsSettings(string id)
        {
            var data=Read(id);if(data==null||data.IsBuiltIn)return null;var value=RoomControls.Physics(data);
            return new JObject {["target"]=id,["revision"]=ObjectRevision(id),["mode"]=value.mode,["shape"]=value.shape,["mass"]=System.Math.Round(value.mass,6),["temporary"]=TemporaryRoom};
        }
        internal JObject ObserveMovementSettings()
        {
            var value=RoomControls.ObserveAvatar(this);if(value==null)return null;
            return new JObject {["revision"]=ObjectRevision("maestro"),["distance"]=System.Math.Round(value.distance,6),["speed"]=System.Math.Round(value.speed,6),["temporary"]=TemporaryRoom,
                ["live"]=new JObject {["active"]=value.active,["mode"]=value.mode,["status"]=ImportObservation.Text(value.status),["canLook"]=value.canLook,["lookReason"]=ImportObservation.Text(value.lookReason),["canFollow"]=value.canFollow,["followReason"]=ImportObservation.Text(value.followReason)}};
        }
        internal JObject ObserveWalkClips(string hash,int offset,string target="maestro")
        {
            var data=Read(target);
            if(!ModelLibrary.ValidHash(hash)||offset<0||offset>32||data?.modelHash!=hash||!Models.TryReadClips(hash,out var clips))return null;
            // Metadata preparation is owned by import/spatial preparation. This
            // passive read cannot load a native model or reserve a motion channel.
            var entries=new JArray();for(int index=offset;index<System.Math.Min(clips.Count,offset+3);index++){
                double duration=System.Math.Round(clips[index].Duration,6);
                entries.Add(new JObject {["index"]=index,["name"]=ImportObservation.Text(clips[index].Name),["duration"]=duration,["selectable"]=clips[index].Duration>=.1f});
            }
            return new JObject {["modelHash"]=hash,["offset"]=offset,["total"]=clips.Count,["pageSize"]=3,["entries"]=entries};
        }
        internal JObject ObserveWalkSettings()
        {
            var value=AvatarWalkSelection.Observe(this);if(value==null)return null;
            return new JObject {["revision"]=ObjectRevision("maestro"),["temporary"]=TemporaryRoom,["selection"]=new JObject {["source"]=value.source,["motionId"]=value.motionId,["modelHash"]=value.modelHash,["clipIndex"]=value.clipIndex,["available"]=value.available,["name"]=ImportObservation.Text(value.name),["status"]=ImportObservation.Text(value.status),["playbackStatus"]=ImportObservation.Text(value.playbackStatus)}};
        }
    }
}
