// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
namespace Maestro.Quest.Interaction {
    // Capability actions share capacity even when their movement solver differs.
    // Admission is passive; a successful Begin owns one slot until Stop.
    internal static class RoomLocomotionBudget {
        internal const int MaximumActive=4;
        static readonly HashSet<object> owners=new();
        internal static int Count=>owners.Count;
        internal static bool Available(out string error){
            error=owners.Count<MaximumActive?null:"Wait for another movement action to finish (four active actions maximum)";
            return error==null;
        }
        internal static bool Acquire(object owner,out string error){
            error=null;if(owners.Contains(owner))return true;
            if(!Available(out error))return false;owners.Add(owner);return true;
        }
        internal static void Release(object owner)=>owners.Remove(owner);
    }
}
