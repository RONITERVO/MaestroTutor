// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class ConstructionSelectionView {
        public string stateId;public bool collecting;public string[] members;
    }
    public sealed partial class RoomEditor {
        string constructionSelectionId=Guid.NewGuid().ToString("N");
        string[] constructionMembers=Array.Empty<string>();
        bool collectingConstruction;
        public ConstructionSelectionView ObserveConstructionSelection()=>new(){stateId=constructionSelectionId,collecting=collectingConstruction,members=(string[])constructionMembers.Clone()};
        public bool CanSetConstructionSelection(string stateId,string[] members,bool collecting,out string error) {
            error=null;
            if(RuntimeGate.Held||Ownership.Suspended||WriteGate.Frozen){error="Room interaction is paused";return false;}
            if(stateId!=constructionSelectionId){error="Construction selection changed; read it again";return false;}
            if(members==null||members.Length>16||members.Distinct().Count()!=members.Length){error="Choose at most 16 distinct creations";return false;}
            foreach(string id in members)if(string.IsNullOrEmpty(id)||journal?.Read(id)?.IsBuiltIn!=false||!Find(id)||!Find(id).isActiveAndEnabled){error="Choose existing creations, without the book or Maestro";return false;}
            if(collecting&&(DrawingMode||DrawingInProgress)){error="Finish drawing and put the pencil away before collecting pieces";return false;}
            return true;
        }
        public bool SetConstructionSelection(string stateId,string[] members,bool collecting,out string error) {
            if(!CanSetConstructionSelection(stateId,members,collecting,out error))return false;
            if(collectingConstruction!=collecting||!constructionMembers.SequenceEqual(members)){
                constructionMembers=(string[])members.Clone();collectingConstruction=collecting;constructionSelectionId=Guid.NewGuid().ToString("N");UpdateSelection();
            }
            SetStatus(collecting?"Collect pieces: tap creations to add/remove; Finish keeps the selection":$"{members.Length} construction pieces selected");return true;
        }
        internal void ClearConstructionSelection() {
            constructionMembers=Array.Empty<string>();collectingConstruction=false;constructionSelectionId=Guid.NewGuid().ToString("N");UpdateSelection();
        }
        void ReconcileConstructionSelection() {
            var current=constructionMembers.Where(id=>objects.ContainsKey(id)&&journal.Read(id)?.IsBuiltIn==false).ToArray();
            if(!constructionMembers.SequenceEqual(current)){constructionMembers=current;constructionSelectionId=Guid.NewGuid().ToString("N");}
        }
        void SuspendConstructionPicking() {
            if(!collectingConstruction)return;collectingConstruction=false;constructionSelectionId=Guid.NewGuid().ToString("N");Changed?.Invoke();
        }
        internal bool ConstructionTap(RoomItem item) {
            if(!collectingConstruction)return false;
            string id=Identity(item);if(id==null||journal.Read(id)?.IsBuiltIn!=false){SetStatus("Collect creations; the book and Maestro stay separate");return true;}
            var members=constructionMembers.Contains(id)?constructionMembers.Where(x=>x!=id).ToArray():constructionMembers.Append(id).ToArray();
            ConstructionSelectionCapability.RunManual(this,members,true,out var status);SetStatus(status);return true;
        }
    }
}
