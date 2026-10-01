// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class ObjectAttachmentFacts
    {
        static JObject VectorType(bool rotation=false){var f=new JObject{["x"]="number",["y"]="number",["z"]="number"};if(rotation)f["w"]="number";return new JObject{["record"]=f};}
        public static BehaviourCatalog.FactDefinition Anchor()=>new("object.anchor",ProgramDataType.Read(new JObject{["record"]=new JObject{["holder"]="text",["kind"]="text",["part"]="text",["position"]=VectorType(),["rotation"]=VectorType(true),["scale"]="number"}}),"Live attachment point","Read the exact selected anchor in world coordinates using the same admission checks as object.hold. Part is a recipe ID, left/right hand, or empty for a root. Offset units scale with the holder root; returned scale is that root's uniform scale. Matching revision/hash is required; held/missing/replaced anchors fail. This does not reserve an object, fit a prop, start motion or grant mutation authority.",Object(new JObject{["holder"]=HoldObjectCapability.AnchorSchema()}),new JObject{["holder"]=new JObject{["kind"]="object",["objectId"]="book",["revision"]=1}},(context,args)=>{
            var anchor=HoldObjectCapability.Anchor((JObject)args["holder"]);if(!anchor.Resolve(context.Editor,out var holder,out var socket,out _))return null;
            return ProgramValue.Literal(new JObject{["holder"]=anchor.HolderId,["kind"]=anchor.Kind,["part"]=anchor.Part,["position"]=JObject.Parse(JsonUtility.ToJson(socket.position)),["rotation"]=JObject.Parse(JsonUtility.ToJson(socket.rotation)),["scale"]=holder.transform.lossyScale.y});
        });
        public static BehaviourCatalog.FactDefinition Attachment()=>new("object.attachment",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"holder\":\"text\",\"kind\":\"text\",\"part\":\"text\",\"phase\":\"text\",\"release\":\"text\",\"error\":\"text\"}}")),"Current prop attachment","Observe the current shared hold component for one loaded creation: holding, released, failed or idle. It covers animation-fitted avatar props and object.hold. Released means physical handoff actually occurred, not just a planned throw. Once the component retires, idle does not erase its historical receipt; inspect that receipt for the completed/interrupted outcome. No automatic recovery or replay.",Object(new JObject{["target"]=RecipeEditCapability.Target()}),new JObject{["target"]=new string('0',32)},(context,args)=>{
            string id=(string)args["target"];var item=context.Editor?context.Editor.Find(id):null;if(!item||!item.isActiveAndEnabled)return null;
            var prop=item.GetComponents<HeldRoomProp>().LastOrDefault(p=>p.enabled);string error=prop?.Error??"";if(error.Length>256)error=error.Substring(0,256);
            return ProgramValue.Literal(new JObject{["target"]=id,["holder"]=prop?.HolderId??"",["kind"]=prop?.AnchorKind??"",["part"]=prop?.AnchorPart??"",["phase"]=prop?(prop.Error!=null?"failed":prop.Released?"released":prop.Holding?"holding":"idle"):"idle",["release"]=prop?.ReleaseMode??"",["error"]=error});
        });
    }
}
