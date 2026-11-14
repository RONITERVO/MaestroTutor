// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Authored shape identity plus a private generated piece. Never infer ownership
    // from names or the aggregate collider list (which also contains terrain).
    internal readonly struct RoomSupportPiece {
        internal readonly Collider Collision;
        internal readonly string Source;
        internal readonly int Piece;
        internal RoomSupportPiece(Collider collision,string source,int piece=0){Collision=collision;Source=source;Piece=piece;}
    }
}
