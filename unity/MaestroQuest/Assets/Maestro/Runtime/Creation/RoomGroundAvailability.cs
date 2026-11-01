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
        (string target,bool terrain,bool imported)[] groundCandidates;
        int groundCandidatesRevision=-1,groundCandidatesCount=-1;
        internal bool GroundCandidate(string target,out (string target,bool terrain,bool imported) candidate)
        {
            candidate=default;
            if(target==null||!items.TryGetValue(target,out var value)||value.IsBuiltIn||value.physics!=ItemPhysics.Fixed)return false;
            bool terrain=(value.heightFields?.Length??0)>0;
            bool imported=value.kind==RoomObjectKind.ImportedModel&&value.modelGeometry?.walkable==true&&value.modelGeometry.meshCollision;
            if(!terrain&&!imported)return false;
            candidate=(target,terrain,imported);return true;
        }
        internal (string target,bool terrain,bool imported)[] GroundCandidates()
        {
            if(groundCandidates!=null&&groundCandidatesRevision==spatialCatalogRevision&&groundCandidatesCount==items.Count)return groundCandidates;
            groundCandidatesRevision=spatialCatalogRevision;groundCandidatesCount=items.Count;
            return groundCandidates=items.Keys.OrderBy(id=>id,StringComparer.Ordinal)
                .Select(id=>GroundCandidate(id,out var candidate)?((string target,bool terrain,bool imported)?)candidate:null)
                .Where(candidate=>candidate.HasValue).Select(candidate=>candidate.Value).ToArray();
        }
    }
    public sealed partial class RoomEditor
    {
        internal JArray GroundDependencies(Vector3 position,float radius,float above,float below)
        {
            return GroundDependencies(RoomGroundQuery.SupportEnvelope(position,radius,above,below));
        }
        internal JArray GroundDependencies(Bounds envelope)
        {
            var result=new JArray();if(journal==null)return result;
            SelectGroundCandidates(envelope);
            foreach(var selected in groundMatches){
                var candidate=selected.Value;
                if(MissingGround(candidate,envelope,out var state,out var known))
                    result.Add(new JObject{["target"]=candidate.Definition.target,["state"]=state,["boundsKnown"]=known});
            }
            return result;
        }
        // The presentation path uses the same completeness rule without building
        // JSON or activating dormant content. Highest resident floor is not proof
        // when a potentially higher accepted surface is missing.
        internal bool GroundComplete(Bounds envelope)
        {
            if(journal==null)return false;
            SelectGroundCandidates(envelope);
            foreach(var selected in groundMatches)
                if(MissingGround(selected.Value,envelope,out _,out _))return false;
            return true;
        }
        bool MissingGround(GroundCandidate selected,Bounds envelope,out string state,out bool known)
        {
            var candidate=selected.Definition;state=null;known=false;
            if(dormantNative.ContainsKey(candidate.target))state=NativeContentPending(candidate.target)?"pending":"unavailable";
            else{
                var item=Find(candidate.target);
                if(!item||!item.isActiveAndEnabled)state="unavailable";
                else{
                    var body=item.GetComponent<Rigidbody>();
                    if(item.Grab&&item.Grab.isSelected||body&&!body.isKinematic||
                        item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)return false;
                    var created=item.GetComponent<CreatedRoomObject>();
                    if(candidate.imported&&(!created||!created.ModelGeometryReady))state=created&&created.ModelLoading?"pending":"unavailable";
                    else if(candidate.terrain&&!item.GetComponent<HeightFieldView>()?.Collision)state="unavailable";
                }
            }
            if(state==null)return false;
            var extent=selected.Cached?selected.Extent:ReadGroundExtent(candidate.target);
            known=extent.Known&&extent.HasBounds;
            return !known||extent.Bounds.Intersects(envelope);
        }
    }
}
