// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using System;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Maestro.Quest.Rules
{
    /// <summary>A solid mounted button can only be activated/grabbed by the other controller.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class RuleButton : PhysicalAction, IXRSelectFilter
    {
        RuleButtonData data;
        RoomRules rules;
        RuleWorkshop workshop;
        Func<int,Transform> anchorSource;
        RoomItem item;
        Transform visual;
        TextMesh label;
        Material material;
        Collider hit;
        public bool canProcess => isActiveAndEnabled;
        public bool IsHeld => item && item.Grab.isSelected;
        int Owner => data == null || data.mount == ButtonMount.Room ? -1 : data.mount == ButtonMount.LeftController ? 0 : 1;
        public override bool CanActivatePointer(int pointerId) => Owner == -1 || pointerId != Owner;
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            var identity = interactor.transform.GetComponent<ControllerIdentity>();
            return !identity || CanActivatePointer(identity.PointerId);
        }
        public void Build(RoomRules runtime, RuleWorkshop source, Func<int,Transform> controllerAnchors)
        {
            rules = runtime; workshop = source; anchorSource = controllerAnchors;
            material = IllustratedMaterials.Create(IllustratedMaterials.Hex("2B8D88"));
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube); shape.transform.SetParent(transform,false); shape.transform.localScale = new Vector3(.065f,.055f,.025f);
            shape.GetComponent<Collider>().enabled = false; ArtResources.Release(shape.GetComponent<Collider>()); shape.GetComponent<Renderer>().sharedMaterial = material; visual = shape.transform;
            var collider = gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.075f,.065f,.035f); hit = collider;
            item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider },1,1); item.Grab.selectFilters.Add(this); item.GrabFinished += Placed;
            var marking = new GameObject("Action name",typeof(TextMesh)); marking.transform.SetParent(shape.transform,false); marking.transform.localPosition = new Vector3(0,0,-.55f);
            // Compensate for the geometry's scale so the marking uses physical meters.
            marking.transform.localScale = new Vector3(1/.065f,1/.055f,1/.025f);
            label = marking.GetComponent<TextMesh>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 48; label.characterSize = .0038f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = IllustratedMaterials.TextColor(IllustratedMaterials.Paper); marking.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(label.font);
        }
        public void Configure(RuleButtonData value, string name)
        {
            data = value.Copy(); AccessibleName = name; label.text = name.Length > 12 ? name.Substring(0,12) : name;
            if (data.mount == ButtonMount.Room && !item.Grab.isSelected) transform.SetLocalPositionAndRotation(data.position,data.rotation);
        }
        protected override void OnActivate() { if (data != null) rules.Trigger(data.sequenceId); }
        void Placed(RoomItem _)
        {
            if (data == null) return;
            var parent = Owner == -1 ? transform.parent : anchorSource?.Invoke(Owner);
            if (!parent) return;
            if (!workshop.PlaceButton(data.id,parent.InverseTransformPoint(transform.position),Quaternion.Inverse(parent.rotation)*transform.rotation) && Owner == -1)
                transform.SetLocalPositionAndRotation(data.position,data.rotation);
        }
        void LateUpdate()
        {
            if (data == null || !item) return;
            var anchor = Owner == -1 ? transform.parent : anchorSource?.Invoke(Owner);
            bool available = anchor;
            visual.gameObject.SetActive(available); hit.enabled = available;
            if (!available) { if (item.Grab.enabled) item.Grab.enabled = false; return; }
            if (!item.Grab.enabled) item.Grab.enabled = true;
            if (Owner != -1 && !item.Grab.isSelected) transform.SetPositionAndRotation(anchor.TransformPoint(data.position),anchor.rotation*data.rotation);
        }
        void OnDestroy() { if (item) { item.GrabFinished -= Placed; item.Grab.selectFilters.Remove(this); } ArtResources.Release(material); }
    }
}
