// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Run after fixed-step hand/prop/catch ownership changes and before PhysX.
    // Ordinary body preparation is too early to capture a prop released later.
    [DefaultExecutionOrder(32000)]
    internal sealed class RoomPhysicsStepRecorder:MonoBehaviour {
        RoomEditor owner;
        void FixedUpdate(){if(!owner)owner=GetComponent<RoomEditor>();if(owner&&owner.isActiveAndEnabled)owner.RememberPhysicsStep();}
    }
}
