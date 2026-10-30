// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    /// <summary>Opt-in batch evidence of the actual displayed skin; never samples or drives a clip.</summary>
    internal sealed class QuestAvatarProbe
    {
        readonly JArray frames=new();
        string lastIdentity;
        int discarded;
        public JObject Observe(GameObject root,string probeId,RoomAgentState room)
        {
            var avatar=root.GetComponentInChildren<MaestroAvatar>();
            var model=avatar?avatar.CustomModel:null;
            bool ready=model&&model.Ready,playing=avatar&&avatar.IsImportedClipPlaying;
            string motion=avatar?avatar.LibraryMotionId:null,modelHash=avatar?avatar.ModelHash:null;
            string identity=ready+"|"+playing+"|"+motion+"|"+modelHash;
            if(!playing&&identity==lastIdentity)return null;
            lastIdentity=identity;
            var joints=new JArray();bool truncated=false;
            if(ready){
                var bones=model.Instance.SkinnedMeshRenderers.SelectMany(skin=>skin.bones).Where(bone=>bone).Distinct().ToArray();
                truncated=bones.Length>512;
                foreach(var bone in bones.Take(512)){
                    var p=bone.localPosition;var q=bone.localRotation;
                    joints.Add(new JObject {["path"]=AnimationUtility.CalculateTransformPath(bone,model.Instance.transform),
                        ["position"]=new JArray(p.x,p.y,p.z),["rotation"]=new JArray(q.x,q.y,q.z,q.w)});
                }
            }
            frames.Add(new JObject {["time"]=Time.realtimeSinceStartupAsDouble,["frame"]=Time.frameCount,
                ["ready"]=ready,["playing"]=playing,["motionId"]=motion??"",["modelHash"]=modelHash??"",
                ["rigHash"]=ready?model.MotionRigHash??"":"",["error"]=avatar?avatar.ImportedPlaybackError:null,
                ["truncatedBones"]=truncated,["joints"]=joints,
                ["runs"]=new JArray((room.rules?.running??System.Array.Empty<Rules.RuleRunView>()).Select(run=>run.id))});
            if(frames.Count>512){frames.RemoveAt(0);discarded++;}
            return new JObject {["version"]=1,["id"]=probeId,["boundary"]="Editor-only actual skin transforms; no provider substitution, clip sampling, headset or frame-time proof",
                ["discarded"]=discarded,["frames"]=frames.DeepClone()};
        }
    }
}
