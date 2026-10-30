// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Maestro.Quest.Persistence;
using Maestro.Quest.Book;

namespace Maestro.Quest.Interaction
{
    /// <summary>Movable room content; loose creations can opt into RigidRoomItem physics.</summary>
    public sealed class RoomItem : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        public XRGrabInteractable Grab { get; private set; }
        internal bool PoseLocked {get;set;}
        internal Creation.RoomWaterTraversal WaterTraversal {get;set;}=new(){mode="ignore"};
        WorkspaceWriteGate writes;
        public bool canProcess=>isActiveAndEnabled;
        internal bool PointerVisible=>!GetComponent<Creation.RoomAppearanceView>()||GetComponent<Creation.RoomAppearanceView>().PointerVisible;
        public bool Process(IXRSelectInteractor interactor,IXRSelectInteractable interactable)=>PointerVisible&&!PoseLocked&&writes?.Frozen!=true;
        bool IXRHoverFilter.Process(IXRHoverInteractor interactor,IXRHoverInteractable interactable)=>PointerVisible;
        internal void ConfigureWrites(WorkspaceWriteGate gate){writes=gate;}
        internal void DetachWrites(WorkspaceWriteGate gate){if(ReferenceEquals(writes,gate))writes=null;}
        public event Action<RoomItem> GrabStarted, GrabFinished;
        Vector3 homePosition, homeScale;
        Quaternion homeRotation;

        public void Configure(Collider[] handles, float minimumScale = .5f, float maximumScale = 2f)=>ConfigurePrepared(handles,minimumScale,maximumScale,null);
        internal void ConfigurePrepared(Collider[] handles,float minimumScale,float maximumScale,Creation.NativePhysicsResources.Lease reservation)
        {
            if (Grab) return;
            homePosition = transform.localPosition; homeRotation = transform.localRotation; homeScale = transform.localScale;
            var body = gameObject.AddComponent<Rigidbody>();reservation?.BindBody(body);
            body.isKinematic = true; body.useGravity = false;
            Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.enabled = false;
            Grab.colliders.Clear(); Grab.colliders.AddRange(handles);
            Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            Grab.selectMode = InteractableSelectMode.Multiple;
            Grab.useDynamicAttach = true;
            Grab.throwOnDetach = false;
            Grab.addDefaultGrabTransformers = false;
            var transformer = gameObject.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowOneHandedScaling = false;
            transformer.allowTwoHandedScaling = true;
            transformer.clampScaling = true;
            transformer.minimumScaleRatio = minimumScale;
            transformer.maximumScaleRatio = maximumScale;
            transformer.allowTwoHandedRotation = XRGeneralGrabTransformer.TwoHandedRotationMode.TwoHandedAverage;
            Grab.AddSingleGrabTransformer(transformer);
            Grab.AddMultipleGrabTransformer(transformer);
            Grab.firstSelectEntered.AddListener(OnGrabStarted);
            Grab.lastSelectExited.AddListener(OnGrabFinished);
            Grab.selectFilters.Add(this);Grab.hoverFilters.Add(this);
            Grab.enabled = true;
        }

        // XRI needs page/button targets in the grab registration, but their
        // selection volumes are not physical geometry. Only an explicit NEW
        // registration gets this role; configured handles always remain solid.
        readonly HashSet<Collider> pointerSurfaces = new();
        internal bool IsPointerSurface(Collider surface)=>pointerSurfaces.Contains(surface)&&
            (!surface||surface.GetComponentInParent<RoomItem>(true)==this);
        internal static bool IsPointerOnly(Collider surface)=>surface&&
            surface.GetComponentInParent<RoomItem>(true) is RoomItem owner&&owner.IsPointerSurface(surface);
        internal SolidColliderView SolidColliders=>new(this);
        internal int SolidColliderCount=>SolidColliders.Count;
        // A value enumerator keeps physical sampling allocation-free. The
        // IEnumerable interface exists only for occasional LINQ/copy callers.
        internal readonly struct SolidColliderView:IEnumerable<Collider> {
            readonly RoomItem owner;
            internal SolidColliderView(RoomItem owner){this.owner=owner;}
            internal int Count {get{int count=0;foreach(var _ in this)count++;return count;}}
            public Enumerator GetEnumerator()=>new(owner);
            IEnumerator<Collider> IEnumerable<Collider>.GetEnumerator()=>GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
            internal struct Enumerator:IEnumerator<Collider> {
                readonly RoomItem owner;readonly bool available;
                List<Collider>.Enumerator entries;
                internal Enumerator(RoomItem owner){this.owner=owner;available=owner&&owner.Grab;entries=available?owner.Grab.colliders.GetEnumerator():default;}
                public Collider Current=>entries.Current;
                object System.Collections.IEnumerator.Current=>Current;
                public bool MoveNext(){
                    if(!available)return false;
                    // Null/destroyed SOLIDS remain unavailable rather than empty.
                    // Destroyed pointers keep their explicit role until mutation.
                    while(entries.MoveNext())if(!owner.IsPointerSurface(entries.Current))return true;
                    return false;
                }
                public void Dispose()=>entries.Dispose();
                public void Reset()=>throw new NotSupportedException();
            }
        }
        internal bool RegisterPointerSurface(Collider surface) {
            if(!Grab||Grab.isSelected||!surface||surface.GetComponentInParent<RoomItem>(true)!=this||Grab.colliders.Contains(surface))return false;
            bool enabled=Grab.enabled;Grab.enabled=false;
            pointerSurfaces.Add(surface);Grab.colliders.Add(surface);Grab.enabled=enabled;return true;
        }
        internal bool UnregisterPointerSurface(Collider surface) {
            if(!Grab||Grab.isSelected||!pointerSurfaces.Contains(surface))return false;
            bool enabled=Grab.enabled;Grab.enabled=false;
            pointerSurfaces.Remove(surface);Grab.colliders.Remove(surface);Grab.enabled=enabled;return true;
        }
        // Called by accepted authored geometry replacement with XRI disabled.
        // Existing pointer targets survive; explicitly supplied handles become
        // solid even if they previously had a pointer role.
        internal void ReplaceSolidColliders(Collider[] handles) {
            var retained=new List<Collider>();
            foreach(var surface in Grab.colliders)if(surface&&IsPointerSurface(surface))retained.Add(surface);
            Grab.colliders.Clear();pointerSurfaces.Clear();Grab.colliders.AddRange(handles);
            foreach(var surface in retained)if(!Grab.colliders.Contains(surface)){
                pointerSurfaces.Add(surface);Grab.colliders.Add(surface);
            }
        }
        void Start()
        {
            if (!Grab||Grab.isSelected) return;
            // Pages and tray buttons are built after Configure. Nested movable
            // items keep their own registration. Preserve the original layers
            // and XRI targets; no ancestor marker can exempt a configured solid.
            bool enabled=Grab.enabled;Grab.enabled=false;
            foreach (var surface in GetComponentsInChildren<Collider>(true)) {
                if (!surface.GetComponentInParent<BookPageTarget>(true) && !surface.GetComponentInParent<PhysicalAction>(true)) continue;
                RegisterPointerSurface(surface);
            }
            Grab.enabled=enabled;
        }

        void OnGrabStarted(SelectEnterEventArgs _) => GrabStarted?.Invoke(this);
        void OnGrabFinished(SelectExitEventArgs _) => GrabFinished?.Invoke(this);

        public void SetHome(Vector3 position, Quaternion rotation, Vector3 scale)
        { homePosition = position; homeRotation = rotation; homeScale = scale; }

        public void RestoreHome()
        {
            if(PoseLocked)return;
            // Disabling first lets XRI cancel all hands before changing the pose.
            if (Grab) Grab.enabled = false;
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
            transform.localScale = homeScale;
            GetComponent<RigidRoomItem>()?.Teleported();
            if (Grab) Grab.enabled = true;
        }
    }
}
