// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    public static class RoomPhysicsLayers
    {
        public const int Environment = 8, Controller = 9, Item = 10, Scanned = 11;
        // A saved room is a physics boundary, not a selectable interface. Keeping
        // it out of tool rays lets users recover tools placed beyond a surface.
        public const int InteractionMask = ~((1 << Scanned) | (1 << Controller));
        public static void Configure()
        {
            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(Environment,layer,layer != Item);
                Physics.IgnoreLayerCollision(Scanned,layer,layer != Item);
                Physics.IgnoreLayerCollision(Controller,layer,layer != Item);
                Physics.IgnoreLayerCollision(Item,layer,layer != Environment && layer != Scanned && layer != Controller && layer != Item);
            }
        }
    }
}
