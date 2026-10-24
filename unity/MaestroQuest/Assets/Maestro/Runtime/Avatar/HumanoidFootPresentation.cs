// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    // Retargeting ends at 150; pose handles observe at 200. Restoration has a
    // separate early phase, before AnimationWorkshop (-100) can record a pose.
    [DefaultExecutionOrder(175)]
    internal sealed class HumanoidFootPresentation:MonoBehaviour
    {
        internal HumanoidFootPlacement Owner;
        void LateUpdate(){if(Owner&&Owner.isActiveAndEnabled)Owner.Present(Time.deltaTime);}
    }
}
