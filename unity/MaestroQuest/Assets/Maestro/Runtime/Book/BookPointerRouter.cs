// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;

namespace Maestro.Quest.Book
{
    /// <summary>One pointer owner for both pages. Input adapters feed rays and gestures.</summary>
    public sealed class BookPointerRouter : MonoBehaviour
    {
        public NativeBookBrowser Browser;
        public RoomEditor Editor;
        public PhysicsTools Placement;
        bool capturedPlacement;
        public float MaximumDistance = 2;
        public LayerMask InteractionLayers = RoomPhysicsLayers.InteractionMask;
        int owner = -1;
        BookPageTarget capturedPage;
        PhysicalAction capturedAction;
        RoomItem capturedItem;
        Vector2Int lastPixel;

        public bool Begin(int pointerId, Ray ray)
        {
            if (owner == -1 && Placement && Placement.Placing) { owner = pointerId; capturedPlacement = true; return true; }
            if (owner != -1 || !Physics.Raycast(ray, out var hit, MaximumDistance, InteractionLayers, QueryTriggerInteraction.Ignore)) return false;
            if (IsMoving(hit.collider)) return false;
            var action = hit.collider.GetComponentInParent<PhysicalAction>();
            if (action != null && !action.CanActivatePointer(pointerId)) return false;
            var page = hit.collider.GetComponent<BookPageTarget>();
            var item = hit.collider.GetComponentInParent<RoomItem>();
            if (action == null && (page == null || Browser == null || !Browser.IsReady) && (item == null || Editor == null)) return false;
            owner = pointerId;
            capturedPage = action == null && page != null && Browser != null && Browser.IsReady ? page : null;
            capturedAction = action; capturedItem = action == null && page == null ? item : null;
            if (capturedPage != null) Send(hit, BrowserPointerPhase.Down);
            return true;
        }

        public void Move(int pointerId, Ray ray)
        {
            if (pointerId != owner || capturedPage == null) return;
            if (Physics.Raycast(ray, out var hit, MaximumDistance, InteractionLayers, QueryTriggerInteraction.Ignore) && !IsMoving(hit.collider) && hit.collider.GetComponent<BookPageTarget>() == capturedPage) Send(hit, BrowserPointerPhase.Move);
            else Cancel(pointerId);
        }

        public void End(int pointerId, Ray ray)
        {
            if (pointerId != owner) return;
            if (capturedPlacement) { Placement.Place(ray); Clear(); return; }
            bool hits = Physics.Raycast(ray, out var hit, MaximumDistance, InteractionLayers, QueryTriggerInteraction.Ignore);
            if (hits && IsMoving(hit.collider)) { Cancel(pointerId); return; }
            if (capturedAction != null && hits && hit.collider.GetComponentInParent<PhysicalAction>() == capturedAction) capturedAction.Activate(pointerId);
            if (capturedItem != null && hits && hit.collider.GetComponentInParent<RoomItem>() == capturedItem) { Editor.Select(capturedItem); Editor.Tapped(capturedItem); }
            if (capturedPage != null)
            {
                if (hits && hit.collider.GetComponent<BookPageTarget>() == capturedPage) Send(hit, BrowserPointerPhase.Up);
                else Browser.Pointer(lastPixel.x, lastPixel.y, BrowserPointerPhase.Cancel);
            }
            Clear();
        }

        public void Cancel(int pointerId)
        {
            if (pointerId != owner) return;
            if (capturedPlacement && Placement) Placement.CancelPlacement();
            if (capturedPage != null && Browser != null) Browser.Pointer(lastPixel.x, lastPixel.y, BrowserPointerPhase.Cancel);
            Clear();
        }

        void Send(RaycastHit hit, BrowserPointerPhase phase)
        {
            var uv = capturedPage.PageUV(hit.textureCoord);
            var pixel = BookCoordinates.ToBrowserPixel(capturedPage.Side, uv.x, uv.y, Browser.Resolution.x, Browser.Resolution.y);
            lastPixel = new Vector2Int(pixel.x, pixel.y);
            Browser.Pointer(pixel.x, pixel.y, phase);
        }

        void Clear() { owner = -1; capturedPage = null; capturedAction = null; capturedItem = null; capturedPlacement = false; }
        static bool IsMoving(Collider collider)
        {
            var item = collider.GetComponentInParent<RoomItem>();
            return item && item.Grab && item.Grab.isSelected;
        }
        void OnDisable() => Cancel(owner);
    }
}
