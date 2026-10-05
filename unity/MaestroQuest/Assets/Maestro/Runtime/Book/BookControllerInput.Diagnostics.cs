// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem.Controls;

namespace Maestro.Quest.Book
{
    // Read-only development evidence. Production builds contain neither the tool nor its readout.
    public sealed partial class BookControllerInput
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD
        IEnumerator Start()
        {
            // OVRManager registers its tools during initialization; the XR instance must exist first.
            yield return null;
            Meta.XR.MetaXROperatorExternalTool.RegisterAgenticTool(
                "maestro_get_input_state",
                "Read Maestro's actual input bindings, gesture ownership, ray hits and XRI selections. " +
                "Development-only; does not change room state or read chat, credentials or camera frames.",
                null, _ => this ? ReadInputDiagnostics().ToString(Newtonsoft.Json.Formatting.None) : "{\"available\":false}");
        }
#endif
        internal JObject ReadInputDiagnostics()
        {
            var result = new JObject { ["frame"] = Time.frameCount, ["paused"] = paused,
                ["focused"] = focused, ["enabled"] = isActiveAndEnabled, ["hands"] = new JArray() };
            if (hands == null) return result;
            foreach (var input in hands)
            {
                var ray = new Ray(input.Root.transform.position, input.Root.transform.forward);
                string hitName = null;
                if (Router && Physics.Raycast(ray, out var target, Router.MaximumDistance, Router.InteractionLayers, QueryTriggerInteraction.Ignore))
                    hitName = Path(target.collider.transform);
                var device = input.Tracked.activeControl?.device;
                var gripAxis = device?.TryGetChildControl<AxisControl>("grip");
                var triggerAxis = device?.TryGetChildControl<AxisControl>("trigger");
                ((JArray)result["hands"]).Add(new JObject {
                    ["name"] = input.Root.name, ["active"] = input.Root.activeSelf,
                    ["tracked"] = input.Tracked.ReadValue<float>(), ["usingHand"] = input.UsingHand,
                    ["device"] = device?.layout, ["gripBinding"] = input.Grip.activeControl?.path,
                    ["triggerBinding"] = input.Press.activeControl?.path,
                    ["gripPressed"] = input.Grip.IsPressed(), ["triggerPressed"] = input.Press.IsPressed(),
                    ["gripAxis"] = gripAxis == null ? JValue.CreateNull() : new JValue(gripAxis.ReadValue()),
                    ["triggerAxis"] = triggerAxis == null ? JValue.CreateNull() : new JValue(triggerAxis.ReadValue()),
                    ["gestureGrip"] = input.Squeeze.Target.ToString(), ["gestureTrigger"] = input.Trigger.Target.ToString(),
                    ["selectPerformed"] = input.Select.ReadIsPerformed(), ["selectStarted"] = input.Select.ReadWasPerformedThisFrame(),
                    ["pageHeld"] = input.PageHeld, ["drawingHeld"] = input.DrawingHeld,
                    ["position"] = Vector(input.Root.transform.position), ["forward"] = Vector(input.Root.transform.forward),
                    ["rayHit"] = hitName,
                    ["hovered"] = new JArray(input.Interactor.interactablesHovered.Select(item => Path(item.transform))),
                    ["selected"] = new JArray(input.Interactor.interactablesSelected.Select(item => Path(item.transform)))
                });
            }
            return result;
        }
        static JObject Vector(Vector3 value) => new JObject { ["x"] = value.x, ["y"] = value.y, ["z"] = value.z };
        static string Path(Transform target)
        {
            string path = target.name;
            while (target.parent) { target = target.parent; path = target.name + "/" + path; }
            return path;
        }
    }
}
#endif
