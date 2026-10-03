// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Maestro.Quest.Interaction
{
    /// <summary>A solid recovery control carried by either tracked palm, independent of room content.</summary>
    [DefaultExecutionOrder(-220)]
    public sealed class PalmRecoveryButton : PhysicalAction
    {
        readonly List<XRHandSubsystem> subsystems = new();
        XRHandSubsystem hands;
        RoomInteraction room;
        RoomPhysicsWorld world;
        Transform trackingSpace, viewer, visual;
        Collider target;
        Material material;
        int handIndex;
        bool tracked, focused = true, paused;

        public void Build(int hand, RoomInteraction content, RoomPhysicsWorld physics, Transform tracking, Transform head)
        {
            handIndex = hand; room = content; world = physics; trackingSpace = tracking; viewer = head;
            AccessibleName = "Bring book and tools back";
            var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere); shape.name = "Palm recall pebble";
            shape.transform.SetParent(transform,false); shape.transform.localScale = new Vector3(.085f,.06f,.035f);
            shape.GetComponent<Collider>().enabled = false; ArtResources.Release(shape.GetComponent<Collider>());
            material = IllustratedMaterials.Create(IllustratedMaterials.Hex("2B8D88")); shape.GetComponent<Renderer>().sharedMaterial = material;
            visual = shape.transform;
            var collider = gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.09f,.065f,.04f); target = collider;
            var label = new GameObject("Recall marking",typeof(TextMesh)); label.transform.SetParent(visual,false);
            label.transform.localScale = new Vector3(1/.085f,1/.06f,1/.035f); label.transform.localPosition = new Vector3(0,0,-.55f);
            var text = label.GetComponent<TextMesh>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 48;
            text.characterSize = .0048f; text.anchor = TextAnchor.MiddleCenter; text.text = "Recall";
            text.color = IllustratedMaterials.TextColor(IllustratedMaterials.Paper); label.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(text.font);
            SetPalmPose(null);
        }

        public override bool CanActivatePointer(int pointerId) => tracked && pointerId >= 0 && pointerId != handIndex;
        protected override void OnActivate() { world?.PausePhysics(); room?.RestoreInFrontOfViewer(); }

        // Input supplies a pose in tracking-space coordinates. A missing pose hides
        // both the visual and target, so tracking loss cannot leave a stale button.
        public void SetPalmPose(Pose? pose)
        {
            tracked = pose.HasValue && trackingSpace && viewer && focused && !paused;
            if (visual) visual.gameObject.SetActive(tracked);
            if (target) target.enabled = tracked;
            if (!tracked) return;
            var point = trackingSpace.TransformPoint(pose.Value.position);
            var towardViewer = viewer.position - point;
            if (towardViewer.sqrMagnitude < .001f) { tracked = false; visual.gameObject.SetActive(false); target.enabled = false; return; }
            point += towardViewer.normalized*.08f;
            transform.SetPositionAndRotation(point,Quaternion.LookRotation(-towardViewer.normalized,viewer.up));
            Physics.SyncTransforms();
        }

        void Update()
        {
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(subsystems); hands = null;
                foreach (var candidate in subsystems) if (candidate.running) { hands = candidate; break; }
            }
            if (hands != null && hands.running)
            {
                var hand = handIndex == 0 ? hands.leftHand : hands.rightHand;
                if (hand.isTracked && hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose)) { SetPalmPose(pose); return; }
            }
            SetPalmPose(null);
        }
        void OnApplicationFocus(bool value) { focused = value; if (!focused) SetPalmPose(null); }
        void OnApplicationPause(bool value) { paused = value; if (paused) SetPalmPose(null); }
        void OnDisable() => SetPalmPose(null);
        void OnDestroy() => ArtResources.Release(material);
    }
}
