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
        internal (string target,bool terrain,bool imported)[] GroundCandidates()
        {
            if(groundCandidates!=null&&groundCandidatesRevision==clock.Next&&groundCandidatesCount==items.Count)return groundCandidates;
            groundCandidatesRevision=clock.Next;groundCandidatesCount=items.Count;
            return groundCandidates=items.Values
                .Where(x=>!x.IsBuiltIn&&x.physics==ItemPhysics.Fixed&&
                    ((x.heightFields?.Length??0)>0||x.kind==RoomObjectKind.ImportedModel&&x.modelGeometry?.walkable==true&&x.modelGeometry.meshCollision))
                .OrderBy(x=>x.id,StringComparer.Ordinal)
                .Select(x=>(x.id,(x.heightFields?.Length??0)>0,x.kind==RoomObjectKind.ImportedModel&&x.modelGeometry?.walkable==true&&x.modelGeometry.meshCollision)).ToArray();
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
            foreach(var candidate in journal.GroundCandidates())
                if(MissingGround(candidate,envelope,out var state,out var known))
                    result.Add(new JObject{["target"]=candidate.target,["state"]=state,["boundsKnown"]=known});
            return result;
        }
        // The presentation path uses the same completeness rule without building
        // JSON or activating dormant content. Highest resident floor is not proof
        // when a potentially higher accepted surface is missing.
        internal bool GroundComplete(Bounds envelope)
        {
            if(journal==null)return false;
            foreach(var candidate in journal.GroundCandidates())
                if(MissingGround(candidate,envelope,out _,out _))return false;
            return true;
        }
        bool MissingGround((string target,bool terrain,bool imported) candidate,Bounds envelope,out string state,out bool known)
        {
            state=null;known=false;
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
            var spatial=ReadSpatialBounds(candidate.target,out var source);var extent=spatial.Visual;
            if(source!="authored"){
                if(!spatial.Collision.Known)extent.Known=false;
                else if(spatial.Collision.HasBounds)extent.Include(spatial.Collision.Bounds,Matrix4x4.identity);
            }
            known=extent.Known&&extent.HasBounds;
            return !known||extent.Bounds.Intersects(envelope);
        }
    }
}
