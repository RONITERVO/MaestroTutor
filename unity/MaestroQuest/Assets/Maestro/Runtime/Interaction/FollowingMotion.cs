// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Goal selection never displaces an actor. Each native motor retains its
    // own body, habitat, collision admission, animation and movement lifetime.
    internal interface IFollowingMotion {
        Vector3 FollowPoint {get;}
        bool Failed {get;}
        bool Blocked {get;}
        bool Arrived {get;}
        string Status {get;}
        string Reason {get;}
        JObject Result {get;}
        bool Begin(out string error);
        void Fail(string reason);
        void Retarget(Vector3 point);
        void KeepDistance();
        void Tick();
        bool Remember(out string error);
        void Stop(bool preservePlacement);
    }
}
