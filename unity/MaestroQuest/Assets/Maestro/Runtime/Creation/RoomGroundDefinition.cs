// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Creation {
    // One saved identity may supply multiple independent ground providers. This
    // value describes intent only, never native readiness or collider ownership.
    internal readonly struct RoomGroundDefinition {
        internal readonly string target;
        internal readonly bool terrain,imported,ordinary;
        internal RoomGroundDefinition(string target,bool terrain,bool imported,bool ordinary=false){
            this.target=target;this.terrain=terrain;this.imported=imported;this.ordinary=ordinary;
        }
    }
}
