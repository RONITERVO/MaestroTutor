// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class StructureSlot
    {
        public string slot;
        public ObjectPlacement placement;
        public StructureSlot Copy()=>new() {slot=slot,placement=placement==null?null:new ObjectPlacement {target=placement.target,position=placement.position,rotation=placement.rotation,scale=placement.scale}};
    }
    // Membership survives a member's deletion. Geometry and current poses stay on the objects.
    [Serializable] public sealed class RoomStructure
    {
        public const int MaximumStructures=16,MaximumSlots=16;
        public int version=1;
        public string id,name;
        public float positionTolerance=.05f,rotationTolerance=15,scaleTolerance=.05f;
        public StructureSlot[] slots=Array.Empty<StructureSlot>();
        public RoomStructure Copy()=>new() {version=version,id=id,name=name,positionTolerance=positionTolerance,rotationTolerance=rotationTolerance,scaleTolerance=scaleTolerance,slots=slots?.Select(x=>x?.Copy()).ToArray()};
        public bool Validate(out string error)
        {
            error="A structure needs version 1, a stable ID, a readable name and 1–16 distinct slots and objects";
            if(version!=1||!Guid.TryParseExact(id,"N",out _)||name==null||name.Length>80||name.Any(char.IsControl)||slots==null||slots.Length<1||slots.Length>MaximumSlots)return false;
            if(!float.IsFinite(positionTolerance)||positionTolerance<.005f||positionTolerance>1||!float.IsFinite(rotationTolerance)||rotationTolerance<.5f||rotationTolerance>90||!float.IsFinite(scaleTolerance)||scaleTolerance<.005f||scaleTolerance>1){error="Structure tolerances must be within their supported ranges";return false;}
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var slot in slots)if(slot==null||slot.slot==null||!Regex.IsMatch(slot.slot,"^[a-zA-Z][a-zA-Z0-9_]{0,23}$")||!keys.Add(slot.slot)||slot.placement==null)return false;
            return new RoomLayout {placements=slots.Select(x=>x.placement).ToArray()}.Validate(out error);
        }
        internal static bool ValidateCollection(RoomStructure[] structures,IEnumerable<string> objectIds,out string error)
        {
            error="The room has invalid, duplicate or excessive structures";
            if(structures==null||structures.Length>MaximumStructures)return false;
            var ids=new HashSet<string>(objectIds,StringComparer.Ordinal);
            foreach(var structure in structures){error="The room has invalid or duplicate structures";if(structure==null||!ids.Add(structure.id)||!structure.Validate(out error))return false;}
            error=null;return true;
        }
    }
    public sealed class StructureEdits
    {
        public RoomStructure[] Replacements=Array.Empty<RoomStructure>();
        public string[] Removals=Array.Empty<string>();
        public bool Validate(out string error)
        {
            error="Structure edits need distinct valid definitions and removals";
            if(Replacements==null||Removals==null||Replacements.Length>16||Removals.Length>16)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var value in Replacements){error="Structure edits need distinct valid definitions";if(value==null||!ids.Add(value.id)||!value.Validate(out error))return false;}
            foreach(var id in Removals){error="Structure removals must be distinct and separate from replacements";if(!Guid.TryParseExact(id,"N",out _)||!ids.Add(id))return false;}
            error=null;return true;
        }
        internal RoomStructure[] Apply(RoomStructure[] current)
        {
            var values=current.ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            foreach(var id in Removals)values.Remove(id);
            foreach(var value in Replacements)values[value.id]=value.Copy();
            return values.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
        }
    }
    public sealed partial class RoomJournal
    {
        readonly Dictionary<string,RoomStructure> structures=new(StringComparer.Ordinal);
        readonly Dictionary<string,int> structureRevisions=new(StringComparer.Ordinal);
        public int StructureRevision(string id)=>id!=null&&structureRevisions.TryGetValue(id,out var value)?value:0;
        public RoomStructure ReadStructure(string id)=>id!=null&&structures.TryGetValue(id,out var value)?value.Copy():null;
        public RoomStructure[] StructureSnapshot()=>structures.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray();
        void SetStructures(RoomStructure[] before,RoomStructure[] after)
        {
            foreach(var s in before){structures.Remove(s.id);structureRevisions.Remove(s.id);}
            foreach(var s in after){structures[s.id]=s.Copy();structureRevisions[s.id]=clock.Next++;}
        }
        [Serializable] sealed class StructureDelta { public RoomStructure[] structures; }
        static bool EquivalentStructures(RoomStructure[] a,RoomStructure[] b)=>JsonUtility.ToJson(new StructureDelta {structures=a.OrderBy(x=>x.id).ToArray()})==JsonUtility.ToJson(new StructureDelta {structures=b.OrderBy(x=>x.id).ToArray()});
    }
}
