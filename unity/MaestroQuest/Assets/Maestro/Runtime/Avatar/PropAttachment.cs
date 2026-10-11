// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    /// <summary>Immutable fitted prop data, independent of program or tray serialization.</summary>
    public sealed class PropAttachment
    {
        public readonly string ObjectId, AvatarHash;
        public readonly PropHand Hand;
        public readonly PropRelease Release;
        public readonly Vector3 Offset;
        public readonly Quaternion Rotation;
        public readonly float ReleaseAt;
        public PropAttachment(string objectId,string avatarHash,PropHand hand,PropRelease release,Vector3 offset,Quaternion rotation,float releaseAt)
        { ObjectId=objectId;AvatarHash=avatarHash;Hand=hand;Release=release;Offset=offset;Rotation=rotation;ReleaseAt=releaseAt; }
    }
}
