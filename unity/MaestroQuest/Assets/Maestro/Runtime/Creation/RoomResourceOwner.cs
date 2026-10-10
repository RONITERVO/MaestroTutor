// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Copied authored scope for a runtime resource lease. Releasing a
    /// resource never deletes or transfers the entity identified by this value.</summary>
    internal sealed class RoomResourceOwner
    {
        internal readonly string World,Region,Target,Role;
        internal RoomResourceOwner(RoomWorldIdentity world,string target,string role)
        {
            if(world!=null&&!world.Valid)throw new ArgumentException("Invalid resource world identity");
            if(role is not ("object" or "avatar" or "preview" or "appearance" or "unscoped"))throw new ArgumentException("Invalid resource owner role");
            if(target!=null&&target!=""&&(target.Length>128||target.Any(char.IsControl)))throw new ArgumentException("Invalid resource owner target");
            World=world?.worldId??"";Region=world?.regionId??"";Target=target??"";Role=role;
        }
    }
}
