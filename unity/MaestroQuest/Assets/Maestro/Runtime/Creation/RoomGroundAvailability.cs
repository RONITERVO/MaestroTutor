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
        RoomGroundDefinition[] groundCandidates;
        int groundCandidatesRevision=-1,groundCandidatesCount=-1;
        internal bool GroundCandidate(string target,out RoomGroundDefinition candidate)
        {
            candidate=default;
            if(target==null||!items.TryGetValue(target,out var value)||value.IsBuiltIn||value.physics!=ItemPhysics.Fixed)return false;
            bool terrain=(value.heightFields?.Length??0)>0;
            bool imported=value.kind==RoomObjectKind.ImportedModel&&value.modelGeometry?.walkable==true&&value.modelGeometry.meshCollision;
            bool ordinary=value.staticSupport?.enabled==true;
            if(!terrain&&!imported&&!ordinary)return false;
            candidate=new(target,terrain,imported,ordinary);return true;
        }
        // A bounded metadata projection of one accepted source; no saved object
        // copy, native allocation, asset read or geometry activation is needed.
        internal RoomSpatialExtent GroundSupportExtent(string target,Matrix4x4? placement=null){
            if(target==null||!items.TryGetValue(target,out var data))return default;
            var local=RoomGroundBounds.Ordinary(data);
            return local.Transform(placement??Matrix4x4.TRS(data.position,data.rotation.normalized,Vector3.one*data.scale));
        }
        internal RoomGroundDefinition[] GroundCandidates()
        {
            if(groundCandidates!=null&&groundCandidatesRevision==spatialCatalogRevision&&groundCandidatesCount==items.Count)return groundCandidates;
            groundCandidatesRevision=spatialCatalogRevision;groundCandidatesCount=items.Count;
            return groundCandidates=items.Keys.OrderBy(id=>id,StringComparer.Ordinal)
                .Select(id=>GroundCandidate(id,out var candidate)?(RoomGroundDefinition?)candidate:null)
                .Where(candidate=>candidate.HasValue).Select(candidate=>candidate.Value).ToArray();
        }
    }
    public sealed partial class RoomEditor
    {
        internal JArray GroundDependencies(Vector3 position,float radius,float above,float below,string actor=null)
        {
            return GroundDependencies(RoomGroundQuery.SupportEnvelope(position,radius,above,below),actor);
        }
        internal JArray GroundDependencies(Bounds envelope,string actor=null)
        {
            var result=new JArray();if(journal==null)return result;
            SelectGroundCandidates(envelope);
            foreach(var selected in groundMatches){
                var candidate=selected.Value;
                if(MissingGround(candidate,envelope,out var state,out var known,actor))
                    result.Add(new JObject{["target"]=candidate.Definition.target,["state"]=state,["boundsKnown"]=known});
            }
            return result;
        }
        // The presentation path uses the same completeness rule without building
        // JSON or activating dormant content. Highest resident floor is not proof
        // when a potentially higher accepted surface is missing.
        internal bool GroundComplete(Bounds envelope,string actor=null)
        {
            if(journal==null)return false;
            SelectGroundCandidates(envelope);
            foreach(var selected in groundMatches)
                if(MissingGround(selected.Value,envelope,out _,out _,actor))return false;
            return true;
        }
        bool MissingGround(GroundCandidate selected,Bounds envelope,out string state,out bool known,string actor=null)
        {
            var candidate=selected.Definition;state=null;known=false;
            // A moving participant cannot support itself. Suspend only its own
            // ordinary missing-source obligation; this grants no collider/floor.
            // Other identities and legacy terrain/import roles remain required.
            if(candidate.target==actor&&candidate.ordinary&&!candidate.terrain&&!candidate.imported)return false;
            if(dormantNative.ContainsKey(candidate.target))state=NativeContentPending(candidate.target)?"pending":"unavailable";
            else{
                var item=Find(candidate.target);
                if(!item||!item.isActiveAndEnabled)state="unavailable";
                else{
                    var created=item.GetComponent<CreatedRoomObject>();
                    if(candidate.ordinary&&(!created||!created.StaticSupportReady))
                        state=created&&created.ModelLoading?"pending":"unavailable";
                    // Preserve legacy terrain/import exclusion. New ordinary
                    // support retains its intent while held/owned/unavailable,
                    // so a lower floor cannot stand in for that missing source.
                    if(!candidate.ordinary){
                        var body=item.GetComponent<Rigidbody>();
                        if(item.Grab&&item.Grab.isSelected||body&&!body.isKinematic||
                            item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)return false;
                    }
                    if(state==null&&candidate.imported&&(!created||!created.ModelGeometryReady))state=created&&created.ModelLoading?"pending":"unavailable";
                    else if(state==null&&candidate.terrain&&!item.GetComponent<HeightFieldView>()?.Collision)state="unavailable";
                }
            }
            if(state==null)return false;
            var extent=selected.Cached?selected.Extent:ReadGroundExtent(candidate);
            known=extent.Known&&extent.HasBounds;
            return !known||extent.Bounds.Intersects(envelope);
        }
    }
}
