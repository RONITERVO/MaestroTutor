// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    [DefaultExecutionOrder(185)]
    internal sealed class ObjectAttentionPresentation:MonoBehaviour
    {
        internal ObjectJointAttention Owner;
        void LateUpdate(){if(Owner&&Owner.isActiveAndEnabled)Owner.Present(Time.unscaledDeltaTime);}
    }
}
