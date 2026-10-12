// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomJournal
    {
        // Project identities and ground kinds only. A passive query must not copy
        // every recipe, animation, stroke and texture in the saved world.
        internal (string target,bool terrain,bool imported)[] GroundCandidates()=>items.Values
            .Where(x=>!x.IsBuiltIn&&x.physics==ItemPhysics.Fixed&&
                ((x.heightFields?.Length??0)>0||x.kind==RoomObjectKind.ImportedModel&&x.modelGeometry?.walkable==true&&x.modelGeometry.meshCollision))
            .OrderBy(x=>x.id,StringComparer.Ordinal)
            .Select(x=>(x.id,(x.heightFields?.Length??0)>0,x.kind==RoomObjectKind.ImportedModel&&x.modelGeometry?.walkable==true&&x.modelGeometry.meshCollision)).ToArray();
    }
    public sealed partial class RoomEditor
    {
        internal JArray GroundDependencies(Vector3 position,float radius,float above,float below)
        {
            var result=new JArray();if(journal==null)return result;
            var envelope=RoomGroundQuery.SupportEnvelope(position,radius,above,below);
            foreach(var candidate in journal.GroundCandidates()){
                string state=null;
                if(dormantNative.TryGetValue(candidate.target,out var dormant)){
                    state=dormant.Phase==NativeEntityPhase.Preparing||startupTargets.Contains(candidate.target)?"pending":"unavailable";
                }else{
                    var item=Find(candidate.target);
                    if(!item||!item.isActiveAndEnabled)state="unavailable";
                    else{
                        // These are intentionally excluded from authored support,
                        // not absent geometry that a loader should recreate.
                        var body=item.GetComponent<Rigidbody>();
                        if(item.Grab&&item.Grab.isSelected||body&&!body.isKinematic||
                            item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)continue;
                        var created=item.GetComponent<CreatedRoomObject>();
                        if(candidate.imported&&(!created||!created.ModelGeometryReady)){
                            state=created&&created.ModelLoading?"pending":"unavailable";
                        }else if(candidate.terrain&&!item.GetComponent<HeightFieldView>()?.Collision){
                            state="unavailable";
                        }
                    }
                }
                if(state==null)continue;
                // Canonical visual bounds include saved terrain/import meshes.
                // Retained native bounds also need collision: a sculpt preview can
                // be smaller than the accepted terrain. Neither disabled geometry
                // nor an unknown extent may prove ground absent. These envelopes
                // only exclude distant candidates; they never supply a floor.
                var spatial=ReadSpatialBounds(candidate.target,out var source);var extent=spatial.Visual;
                if(source!="authored"){
                    if(!spatial.Collision.Known)extent.Known=false;
                    else if(spatial.Collision.HasBounds)extent.Include(spatial.Collision.Bounds,Matrix4x4.identity);
                }
                bool known=extent.Known&&extent.HasBounds;
                if(known&&!extent.Bounds.Intersects(envelope))continue;
                result.Add(new JObject{["target"]=candidate.target,["state"]=state,["boundsKnown"]=known});
            }
            return result;
        }
    }
}
