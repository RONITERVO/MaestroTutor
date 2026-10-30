// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Creation
{
    public sealed partial class LiquidPouring
    {
        // Internal ticks also support deterministic desktop stepping while the
        // automatic MonoBehaviour loop is disabled. Inactive roots cannot step.
        internal bool ContactWorkEligible=>gameObject.activeInHierarchy&&ContactReady;
        internal RoomResourceOwner ContactWorkOwner=>new(editor.WorldIdentity,null,"liquid");
        internal int ContactWorkDemand {
            get {
                int demand=ContactWorkResources.BaseDemand;
                // Include unavailable/fixed/held objects and empty receivers.
                // Becoming visible or changing ownership must not bypass admission.
                foreach(var b in mediumBodies.Values)demand+=ContactWorkResources.BodyDemand(b.Rigid?b.Rigid.PhysicsWorkColliders:0);
                return demand;
            }
        }
        long automaticContactStep=-1;
        internal void TickAutomaticContacts(){
            long step=SimulationWorkSchedule.Step;if(automaticContactStep==step)return;
            automaticContactStep=step;TickContacts(SimulationWorkSchedule.StepSeconds);
        }
        void WaitForContactWork(string reason,bool scheduled=false)
        {
            contactEpoch++;contactEvents.Clear();
            foreach(var state in contacts.Values)Forget(state,reason);
            if(scheduled)return; // Existing ripples finish naturally; a missed sample creates no new ripple.
            foreach(var vessel in vessels.Values)if(vessel.Item)vessel.Item.GetComponent<ContainerFillView>()?.ClearRipples();
        }
    }
}
