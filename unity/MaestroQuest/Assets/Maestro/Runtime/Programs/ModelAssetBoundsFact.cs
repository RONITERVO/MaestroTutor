// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class ModelAssetBoundsFact
    {
        internal static BehaviourCatalog.FactDefinition Definition(bool rest=false)
        {
            JObject Triple()=>Object(new JObject{["x"]=Number(-float.MaxValue,float.MaxValue),["y"]=Number(-float.MaxValue,float.MaxValue),["z"]=Number(-float.MaxValue,float.MaxValue)});
            var output=Object(new JObject{
                ["target"]=Text(null,32),["revision"]=Number(0,int.MaxValue,true),["modelHash"]=Text(null,64),["derivationVersion"]=Number(1,int.MaxValue,true),
                ["coordinates"]=Text(null,16),["inspected"]=new JObject{["type"]="boolean"},["known"]=new JObject{["type"]="boolean"},["hasBounds"]=new JObject{["type"]="boolean"},
                ["min"]=Triple(),["max"]=Triple(),["reason"]=Text(null,256)});
            var type=OutputType(output);
            return new(rest?"object.model.restBounds":"object.model.assetBounds",type,rest?"Imported resting bounds":"Imported asset bounds",
                rest?"Read the validated GLB resting envelope with saved model scale/pivot, in object-local metres. Dense vertices, selected-scene TRS, up to four normalized skin influences, inverse binds and node-over-mesh default morph weights determine this geometry. Native GLB fitting and Stop use the same rest definition. This is the unloaded/reloaded resting shape, never current pose or an all-animation envelope. VRM, extended/matrix nodes, shared skin bindposes and unsupported influences remain unknown. Reads use the existing bounded content-hash cache and never start I/O, load objects, edit the room or acquire ownership. known=false is not empty; known=true with hasBounds=false means an empty selected scene. Excludes decorations, selection, collision and root motion. Playback and other active dependencies independently retain native objects; this is not automatic streaming.":
                "Read the validated static GLB asset envelope with the object's saved scale/pivot settings, in object-local metres before root placement/scale. Null for non-model objects. Values are derived from actual dense POSITION vertices, selected scene and supported TRS transforms; exporter min/max is not trusted. Existing library reads/saves populate a bounded content-hash cache. inspected=false means no cached inspection; known=false is never empty, while known=true with hasBounds=false means no mesh in the selected scene. Animated, skinned, VRM, deformable and matrix/extended-node geometry remain unknown. Reading never opens files, loads models, changes the room or acquires ownership. This is asset geometry only, with a small numerical margin: it excludes authored decorations, selection outlines, colliders, future motion and root pose. It does not prove current file availability, native presence, collision/navigation readiness, whole-object spatial bounds or automatic streaming. Geometry settings are read fresh at the reported revision.",
                Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},
                (c,a)=>c.Editor&&Observe(c.Editor,(string)a["target"],rest) is JObject value?ProgramValue.Literal(value,type):null);
        }
        static JObject Observe(RoomEditor editor,string target,bool rest)
        {
            var data=editor.Read(target);if(data==null||data.kind!=RoomObjectKind.ImportedModel)return null;
            var metadata=default(ModelAssetBounds);
            bool inspected=editor.Models!=null&&editor.Models.TryReadBounds(data.modelHash,out metadata,rest);
            bool known=inspected&&metadata.Known,hasBounds=known&&metadata.HasBounds;
            string reason=inspected?metadata.Reason:"Read or import the model through the library before inspecting its asset bounds.";
            Bounds bounds=default;
            if(hasBounds){
                if(!ModelGeometryLayout.TryCreate(metadata.SourceBounds,Quaternion.identity,data.modelGeometry,out var layout,out reason)){known=false;hasBounds=false;}
                else {
                    bounds=layout.Bounds;bounds.Expand(metadata.Margin*layout.Factor*2);
                    // Cover float arithmetic differences between decoded TRS and
                    // native Transform evaluation; not a motion/skin envelope.
                    var extent=bounds.extents;var centre=bounds.center;
                    float magnitude=Mathf.Max(Mathf.Abs(centre.x)+extent.x,Mathf.Abs(centre.y)+extent.y,Mathf.Abs(centre.z)+extent.z);
                    bounds.Expand(2*Mathf.Max(.00001f,magnitude*.00001f));
                    if(!RoomRecipe.Finite(bounds.center)||!RoomRecipe.Finite(bounds.size)){known=hasBounds=false;reason="The derived envelope exceeds the supported coordinate range.";}
                }
            }
            return new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["modelHash"]=data.modelHash,["derivationVersion"]=ModelAssetBounds.DerivationVersion,
                ["coordinates"]="object",["inspected"]=inspected,["known"]=known,["hasBounds"]=hasBounds,["min"]=ScannedRoom.Triple(hasBounds?bounds.min:Vector3.zero),["max"]=ScannedRoom.Triple(hasBounds?bounds.max:Vector3.zero),["reason"]=reason??""};
        }
    }
}
